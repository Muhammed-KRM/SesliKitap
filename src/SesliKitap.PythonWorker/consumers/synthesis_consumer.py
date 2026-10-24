"""Kitap bölümlerini sırayla seslendiren, parça önbelleği, kalite kontrol ve motor yedekliliğini yöneten ana tüketici (Bölüm 4.15.6)."""

from __future__ import annotations

import re
from collections.abc import Callable
from pathlib import Path
from typing import ClassVar

from config import settings
from consumers.base_consumer import BaseConsumer
from models.events import (
    ChapterFailedError,
    ChapterSynthesizedEvent,
    ProcessingError,
    SynthesisCompletedEvent,
    SynthesisRequestedEvent,
)
from models.synthesis_models import (
    ChapterResult,
    Chunk,
    ChunkAudio,
    JobStats,
    Sentence,
    SynthesisInput,
    SynthesisInputChapterModel,
)
from models.voice_models import PreparedVoice, SynthChunkInput, VoiceParams
from pipeline.audio.chapter_assembler import AssemblyItem, ChapterAssembler, PauseSettings
from pipeline.audio.encoder import Encoder
from pipeline.audio.loudness_normalizer import LoudnessNormalizer
from pipeline.text.chunk_planner import ChunkPlanner
from pipeline.text.number_verbalizer import verbalize_ordinal
from pipeline.text.sentence_segmenter import SentenceSegmenter
from pipeline.text.turkish_normalizer import TurkishNormalizer
from pipeline.tts.base_engine import EngineUnavailableError, FamilyExhaustedError
from pipeline.tts.chunk_cache import ChunkCache
from pipeline.tts.engine_chain import TtsEngineChain, build_default_engine_chain
from pipeline.tts.quality_checker import QualityChecker
from services.minio_client import MinioStorageClient
from services.pipeline_logger import PipelineLogger
from services.progress_reporter import ProgressReporter
from services.rabbitmq_publisher import RabbitMQPublisher
from services.redis_client import CancelChecker
from utils.constants import Exchanges, Queues, Stage, StoragePaths
from utils.janitor import cleanup_job
from utils.logger import get_logger

logger = get_logger(__name__)

_ALREADY_HAS_CHAPTER_PREFIX_RE = re.compile(
    r"^\s*(?:bölüm|kısım|ünite|\d+\.?\s*bölüm|[a-zçğıöşü]+(?:inci|ıncı|üncü|uncu|ncı|nci)\s+bölüm)\b",
    re.IGNORECASE,
)


def _build_heading_display(sira_no: int, baslik: str) -> str:
    cleaned = (baslik or "").strip().rstrip(".")
    if not cleaned:
        return f"{verbalize_ordinal(max(1, sira_no)).capitalize()} Bölüm."
    if _ALREADY_HAS_CHAPTER_PREFIX_RE.match(cleaned):
        return f"{cleaned}."
    ordinal_word = verbalize_ordinal(max(1, sira_no)).capitalize()
    return f"{ordinal_word} Bölüm. {cleaned}."


class _JobCancelledError(Exception):
    """İş iptal edildiğinde bölüm sentez döngüsünden çıkmak için iç istisna."""


class SynthesisConsumer(BaseConsumer[SynthesisRequestedEvent]):
    """SynthesisRequestedEvent mesajlarını işleyerek bölümleri seslendirir."""

    queue_name: ClassVar[str] = Queues.SYNTHESIS
    exchange_names: ClassVar[list[str]] = [
        Exchanges.SYNTHESIS_REQUESTED,
        f"{Exchanges.PREFIX}{Exchanges.SYNTHESIS_REQUESTED}",
    ]
    message_model: ClassVar[type[SynthesisRequestedEvent]] = SynthesisRequestedEvent
    default_stage: ClassVar[str] = Stage.SENTEZ

    def __init__(
        self,
        publisher: RabbitMQPublisher | None = None,
        pipeline_logger: PipelineLogger | None = None,
        minio_client: MinioStorageClient | None = None,
        cancel_checker: CancelChecker | None = None,
        chunk_cache: ChunkCache | None = None,
        segmenter: SentenceSegmenter | None = None,
        planner: ChunkPlanner | None = None,
        quality_checker: QualityChecker | None = None,
        assembler: ChapterAssembler | None = None,
        loudness_normalizer: LoudnessNormalizer | None = None,
        encoder: Encoder | None = None,
        chain_factory: Callable[[list[str], Callable[[str, str, str], None]], TtsEngineChain] | None = None,
    ) -> None:
        super().__init__(publisher=publisher, pipeline_logger=pipeline_logger)
        self.minio = minio_client or MinioStorageClient()
        self.cancel_checker = cancel_checker or CancelChecker()
        self.chunk_cache = chunk_cache or ChunkCache(minio=self.minio)
        self.segmenter = segmenter or SentenceSegmenter()
        self.planner = planner or ChunkPlanner(
            max_chars=settings.tts_max_chunk_chars,
            min_chars=settings.tts_min_chunk_chars,
        )
        self.quality_checker = quality_checker or QualityChecker()
        self.assembler = assembler or ChapterAssembler()
        self.loudness_normalizer = loudness_normalizer or LoudnessNormalizer()
        self.encoder = encoder or Encoder()
        self.chain_factory = chain_factory or (
            lambda order, on_fb: build_default_engine_chain(order_override=order, on_fallback=on_fb)
        )

    def handle(self, message: SynthesisRequestedEvent) -> None:
        job_id = message.job_id
        book_id = message.book_id
        trace_id = message.trace_id
        job_dir = settings.temp_dir / f"synth-{job_id}"
        job_dir.mkdir(parents=True, exist_ok=True)

        stats = JobStats()
        cancelled = False

        try:
            job = self.minio.read_json(message.girdi_yolu, SynthesisInput)
            if not isinstance(job, SynthesisInput):
                job = SynthesisInput.model_validate(job)

            def _on_engine_fallback(failed_eng: str, next_eng: str, reason: str) -> None:
                warn_text = f"{failed_eng} kullanılamadı, {next_eng} motoruna geçildi ({reason})."
                self.pipeline_logger.log_event(
                    book_id=book_id,
                    job_id=job_id,
                    asama=Stage.SENTEZ,
                    durum="Uyari",
                    yuzde=0,
                    mesaj=warn_text,
                    trace_id=trace_id,
                )

            chain = self.chain_factory(job.motor_sirasi, _on_engine_fallback)

            # Referans sesi ve varsa kitap kapağını indir
            ref_local = job_dir / "reference.wav"
            self.minio.download_file(job.voice_profile.referans_ses_yolu, ref_local)
            voice = PreparedVoice(
                voice_id=job.voice_profile.id,
                voice_hash=job.voice_profile.referans_hash,
                wav_path=ref_local,
                prompt_text=job.voice_profile.referans_metin,
            )

            cover_local: Path | None = None
            if job.book.kapak_yolu and self.minio.exists(job.book.kapak_yolu):
                try:
                    cover_local = job_dir / "cover.jpg"
                    self.minio.download_file(job.book.kapak_yolu, cover_local)
                except Exception as exc:
                    logger.warning("Kapak resmi indirilemedi (jobId=%s): %s", job_id, exc)
                    cover_local = None

            normalizer = TurkishNormalizer(lexicon=job.telaffuz_sozlugu)
            reporter = ProgressReporter(self.publisher, job, throttle_sec=2.0)

            for idx, chapter in enumerate(job.chapters):
                if self.cancel_checker.is_cancelled(job.job_id):
                    cancelled = True
                    break

                chapter_dir = job_dir / f"ch-{chapter.sira_no:03d}-{chapter.id[:8]}"
                chapter_dir.mkdir(parents=True, exist_ok=True)

                try:
                    result = self._synthesize_chapter(
                        job=job,
                        chapter=chapter,
                        chapter_index=idx,
                        chain=chain,
                        voice=voice,
                        normalizer=normalizer,
                        reporter=reporter,
                        chapter_dir=chapter_dir,
                        cover_local=cover_local,
                        trace_id=trace_id,
                    )
                    self.publisher.publish_chapter_synthesized(
                        ChapterSynthesizedEvent(
                            trace_id=trace_id,
                            job_id=result.job_id,
                            book_id=result.book_id,
                            chapter_id=result.chapter_id,
                            metin_surumu=result.metin_surumu,
                            opus_yolu=result.opus_yolu,
                            mp3_yolu=result.mp3_yolu,
                            zaman_damgasi_yolu=result.zaman_damgasi_yolu,
                            opus_boyutu=result.opus_boyutu,
                            mp3_boyutu=result.mp3_boyutu,
                            sure_sn=result.sure_sn,
                            cumle_sayisi=result.cumle_sayisi,
                            motor=result.motor,
                            gpu_ms=result.gpu_ms,
                            onbellek_parca=result.onbellek_parca,
                            uretilen_parca=result.uretilen_parca,
                        )
                    )
                    stats.add(result)
                except _JobCancelledError:
                    cancelled = True
                    break
                except (ChapterFailedError, ProcessingError, EngineUnavailableError, FamilyExhaustedError) as err:
                    err_code = getattr(err, "code", "CHAPTER_SYNTH_FAILED")
                    err_msg = getattr(err, "message", str(err))
                    logger.error(
                        "Bölüm seslendirme hatası (jobId=%s, chapterId=%s): %s",
                        job.job_id,
                        chapter.id,
                        err_msg,
                    )
                    self.publisher.publish_pipeline_error(
                        book_id=job.book.id,
                        job_id=job.job_id,
                        chapter_id=chapter.id,
                        asama=Stage.SENTEZ,
                        hata_kodu=err_code,
                        hata_mesaji=err_msg,
                        kurtarilabilir=True,
                        trace_id=trace_id,
                    )
                    stats.failed += 1
                finally:
                    cleanup_job(chapter_dir)

            if not cancelled and self.cancel_checker.is_cancelled(job.job_id):
                cancelled = True

            if cancelled:
                sonuc = "IptalEdildi"
            elif stats.failed > 0 and stats.completed > 0:
                sonuc = "KismenTamamlandi"
            elif stats.completed == 0 and stats.failed > 0:
                sonuc = "Hata"
            else:
                sonuc = "Tamamlandi"

            used_engines_str = ",".join(stats.used_engines or chain.used_engines) or None
            self.publisher.publish_synthesis_completed(
                SynthesisCompletedEvent(
                    trace_id=trace_id,
                    job_id=job.job_id,
                    book_id=job.book.id,
                    sonuc=sonuc,
                    tamamlanan_bolum=stats.completed,
                    hatali_bolum=stats.failed,
                    iptal_edildi=cancelled,
                    toplam_gpu_ms=stats.total_gpu_ms,
                    uretilen_ses_sn=stats.total_audio_sec,
                    kullanilan_motorlar=used_engines_str,
                )
            )
        finally:
            cleanup_job(job_dir)

    def _synthesize_chapter(
        self,
        *,
        job: SynthesisInput,
        chapter: SynthesisInputChapterModel,
        chapter_index: int,
        chain: TtsEngineChain,
        voice: PreparedVoice,
        normalizer: TurkishNormalizer,
        reporter: ProgressReporter,
        chapter_dir: Path,
        cover_local: Path | None,
        trace_id: str = "",
    ) -> ChapterResult:
        """Bölümü sabitlenen model ailesiyle seslendirir; aile tükenirse sonraki aileyle baştan üretir (F-34)."""
        try:
            chain.pin_for_chapter()
        except EngineUnavailableError as exc:
            raise ChapterFailedError("TTS_ENGINE_UNAVAILABLE", str(exc)) from exc

        while True:
            try:
                return self._synthesize_chapter_with_pinned_family(
                    job=job,
                    chapter=chapter,
                    chapter_index=chapter_index,
                    chain=chain,
                    voice=voice,
                    normalizer=normalizer,
                    reporter=reporter,
                    chapter_dir=chapter_dir,
                    cover_local=cover_local,
                )
            except FamilyExhaustedError as fam_err:
                logger.warning(
                    "Bölüm ortasında '%s' model ailesi tükendi; sonraki aileyle bölüm baştan üretiliyor (chapterId=%s).",
                    fam_err.family,
                    chapter.id,
                )
                try:
                    next_family = chain.advance_to_next_family()
                    self.pipeline_logger.log_event(
                        book_id=job.book.id,
                        job_id=job.job_id,
                        chapter_id=chapter.id,
                        asama=Stage.SENTEZ,
                        durum="Uyari",
                        yuzde=0,
                        mesaj=f"'{fam_err.family}' motor ailesi tükendi, '{next_family}' ailesiyle bölüm yeniden üretiliyor.",
                        trace_id=trace_id,
                    )
                except EngineUnavailableError as exhausted_all:
                    raise ChapterFailedError(
                        "ALL_TTS_ENGINES_EXHAUSTED",
                        f"Tüm TTS motor aileleri tükendi: {exhausted_all}",
                    ) from exhausted_all

    def _synthesize_chapter_with_pinned_family(
        self,
        *,
        job: SynthesisInput,
        chapter: SynthesisInputChapterModel,
        chapter_index: int,
        chain: TtsEngineChain,
        voice: PreparedVoice,
        normalizer: TurkishNormalizer,
        reporter: ProgressReporter,
        chapter_dir: Path,
        cover_local: Path | None,
    ) -> ChapterResult:
        reporter.report(
            chapter_index=chapter_index,
            chapter_id=chapter.id,
            chapter_ratio=0.02,
            message=f"Bölüm {chapter.sira_no} metni cümlelere ayrılıyor ve normalize ediliyor...",
            stage=Stage.NORMALIZASYON,
            force=True,
        )

        # 1 & 2. Segmentasyon + Başlık cümlesi
        sentences: list[Sentence] = []
        next_idx = 0

        if job.ayarlar.bolum_basligi_oku:
            heading_display = _build_heading_display(chapter.sira_no, chapter.baslik)
            heading_spoken = normalizer.normalize(heading_display)
            if heading_spoken:
                sentences.append(
                    Sentence(
                        index=0,
                        paragraph=-1,
                        display=chapter.baslik.strip() or heading_display,
                        spoken=heading_spoken,
                        is_dialogue=False,
                        is_heading=True,
                    )
                )
                next_idx = 1

        paragraphs = self.segmenter.split_paragraphs(chapter.temiz_metin, start_index=next_idx)
        for para in paragraphs:
            for sent in para.sentences:
                spoken = normalizer.normalize(sent.display)
                if not spoken:
                    continue
                sent.index = len(sentences)
                sent.spoken = spoken
                sentences.append(sent)

        if not sentences:
            raise ChapterFailedError(
                "EMPTY_CHAPTER_TEXT",
                f"Bölüm {chapter.sira_no} seslendirilebilir cümle içermiyor.",
            )

        # 4. Parça planlama
        chunks = self.planner.plan(sentences, chapter_id=chapter.id)
        if not chunks:
            raise ChapterFailedError(
                "EMPTY_CHAPTER_CHUNKS",
                f"Bölüm {chapter.sira_no} için TTS parçası oluşturulamadı.",
            )

        # 5. Önbellek sorgulama
        candidates = chain.candidate_engines(ignore_pin=False)
        if not candidates:
            raise FamilyExhaustedError(chain.pinned_family or "unknown", "Kullanılabilir motor yok.")

        primary_engine = candidates[0]
        eng_info = primary_engine.info()
        pinned_family = chain.pinned_family or primary_engine.family
        model_rev = eng_info.model_revision
        sample_rate = eng_info.sample_rate if eng_info.sample_rate > 0 else 24000
        batch_size = max(1, min(eng_info.max_batch, 16))
        params = job.voice_profile.parametreler.to_voice_params()

        chunk_keys: dict[str, str] = {}
        key_to_chunk_id: dict[str, str] = {}
        for ch in chunks:
            ck = ChunkCache.key(pinned_family, model_rev, voice.voice_hash, params, ch.text)
            chunk_keys[ch.id] = ck
            key_to_chunk_id[ck] = ch.id

        cached_by_key = self.chunk_cache.get_many(list(key_to_chunk_id.keys()), key_to_chunk_id)
        audio_by_chunk_id: dict[str, ChunkAudio] = {}
        for ch in chunks:
            ck = chunk_keys[ch.id]
            if ck in cached_by_key:
                cached_audio = cached_by_key[ck]
                audio_by_chunk_id[ch.id] = ChunkAudio(
                    chunk_id=ch.id,
                    samples=cached_audio.samples,
                    sample_rate=cached_audio.sample_rate,
                    attempts=0,
                    engine_id="cache",
                    from_cache=True,
                )

        cached_count = len(audio_by_chunk_id)
        missing_chunks = [ch for ch in chunks if ch.id not in audio_by_chunk_id]

        # 6. Eksik parçaların partiler halinde sentezi + kalite kontrolü
        chapter_gpu_ms = 0
        generated_count = 0
        used_engine_id = primary_engine.engine_id
        total_chunks = len(chunks)

        for batch_start in range(0, len(missing_chunks), batch_size):
            if self.cancel_checker.is_cancelled(job.job_id):
                raise _JobCancelledError()

            batch_chunks = missing_chunks[batch_start : batch_start + batch_size]
            batch_audios, batch_gpu, eng_id = self._synthesize_batch_with_quality_retry(
                batch_chunks=batch_chunks,
                chain=chain,
                voice=voice,
                params=params,
                job_id=job.job_id,
            )
            chapter_gpu_ms += batch_gpu
            used_engine_id = eng_id

            for ch in batch_chunks:
                ch_audio = batch_audios[ch.id]
                audio_by_chunk_id[ch.id] = ch_audio
                generated_count += 1
                try:
                    self.chunk_cache.put(chunk_keys[ch.id], ch_audio)
                except Exception as cache_exc:
                    logger.warning("Parça önbelleğe yazılamadı (%s): %s", ch.id, cache_exc)

            done_chunks = cached_count + generated_count
            ratio = 0.05 + 0.80 * (done_chunks / max(1, total_chunks))
            reporter.report(
                chapter_index=chapter_index,
                chapter_id=chapter.id,
                chapter_ratio=ratio,
                message=f"Bölüm {chapter.sira_no}/{len(job.chapters)} seslendiriliyor ({done_chunks}/{total_chunks} parça)...",
                stage=Stage.SENTEZ,
            )

        if self.cancel_checker.is_cancelled(job.job_id):
            raise _JobCancelledError()

        # 7. Ses birleştirme ve kesin cümle zaman damgaları
        reporter.report(
            chapter_index=chapter_index,
            chapter_id=chapter.id,
            chapter_ratio=0.88,
            message=f"Bölüm {chapter.sira_no} ses parçaları birleştiriliyor...",
            stage=Stage.SES_BIRLESTIRME,
            force=True,
        )

        assembly_items = [
            AssemblyItem(chunk=ch, audio=audio_by_chunk_id[ch.id]) for ch in chunks
        ]
        pauses = PauseSettings.from_input_settings(job.ayarlar)
        assembled_wav = chapter_dir / "assembled.wav"
        timestamps = self.assembler.assemble(
            items=assembly_items,
            sentences=sentences,
            sample_rate=sample_rate,
            pauses=pauses,
            out_path=assembled_wav,
        )

        # 8. EBU R128 ses yüksekliği normalizasyonu ve hız ayarı
        norm_wav = chapter_dir / "normalized.wav"
        speaking_rate = max(0.5, min(3.0, float(params.konusma_hizi or 1.0)))
        self.loudness_normalizer.normalize(
            assembled_wav,
            norm_wav,
            target_lufs=job.ayarlar.hedef_lufs,
            speaking_rate=speaking_rate,
        )

        if abs(speaking_rate - 1.0) >= 1e-3:
            for ts in timestamps:
                ts.s = int(round(ts.s / speaking_rate))
                ts.e = max(ts.s, int(round(ts.e / speaking_rate)))

        # 9. Opus ve MP3 kodlama
        reporter.report(
            chapter_index=chapter_index,
            chapter_id=chapter.id,
            chapter_ratio=0.94,
            message=f"Bölüm {chapter.sira_no} Opus ve MP3 formatlarına kodlanıyor...",
            stage=Stage.KODLAMA,
            force=True,
        )

        tags = {
            "title": chapter.baslik,
            "artist": job.book.yazar or "SesliKitap",
            "album": job.book.baslik,
            "track": f"{chapter.sira_no}/{len(job.chapters)}",
        }
        opus_local = chapter_dir / "chapter.ogg"
        mp3_local = chapter_dir / "chapter.mp3"

        duration_sec = self.encoder.to_opus(
            norm_wav,
            opus_local,
            bitrate_kbps=job.ayarlar.opus_bitrate_kbps,
            tags=tags,
        )
        self.encoder.to_mp3(
            norm_wav,
            mp3_local,
            bitrate_kbps=job.ayarlar.mp3_bitrate_kbps,
            tags=tags,
            cover_path=cover_local,
        )

        # 10. MinIO'ya yükleme
        opus_key = StoragePaths.chapter_audio(job.book.id, chapter.id, chapter.metin_surumu, "ogg")
        mp3_key = StoragePaths.chapter_audio(job.book.id, chapter.id, chapter.metin_surumu, "mp3")
        ts_key = StoragePaths.chapter_timestamps(job.book.id, chapter.id, chapter.metin_surumu)

        self.minio.upload_file(opus_key, opus_local, content_type="audio/ogg")
        self.minio.upload_file(mp3_key, mp3_local, content_type="audio/mpeg")

        ts_payload = {
            "version": 1,
            "chapterId": chapter.id,
            "durationMs": int(round(duration_sec * 1000.0)),
            "sentences": [ts.to_dict() for ts in timestamps],
        }
        self.minio.write_json(ts_key, ts_payload)

        reporter.report(
            chapter_index=chapter_index,
            chapter_id=chapter.id,
            chapter_ratio=1.0,
            message=f"Bölüm {chapter.sira_no} tamamlandı.",
            stage=Stage.SENTEZ,
            force=True,
        )

        return ChapterResult(
            job_id=job.job_id,
            book_id=job.book.id,
            chapter_id=chapter.id,
            metin_surumu=chapter.metin_surumu,
            opus_yolu=opus_key,
            mp3_yolu=mp3_key,
            zaman_damgasi_yolu=ts_key,
            opus_boyutu=opus_local.stat().st_size,
            mp3_boyutu=mp3_local.stat().st_size,
            sure_sn=duration_sec,
            cumle_sayisi=len(timestamps),
            motor=used_engine_id,
            gpu_ms=chapter_gpu_ms,
            onbellek_parca=cached_count,
            uretilen_parca=generated_count,
        )

    def _synthesize_batch_with_quality_retry(
        self,
        *,
        batch_chunks: list[Chunk],
        chain: TtsEngineChain,
        voice: PreparedVoice,
        params: VoiceParams,
        job_id: str,
    ) -> tuple[dict[str, ChunkAudio], int, str]:
        """Partiyi sentezler, kalite kontrolünden geçmeyen parçaları farklı tohumla yeniden dener ve en yüksek puanlı adayı seçer."""
        max_attempts = max(1, settings.tts_quality_max_retries + 1)
        pending: dict[str, Chunk] = {ch.id: ch for ch in batch_chunks}
        best_candidates: dict[str, tuple[ float, ChunkAudio ]] = {}
        total_gpu_ms = 0
        last_engine_id = ""

        for attempt in range(1, max_attempts + 1):
            if not pending:
                break

            base_seed = params.seed if params.seed is not None else 42
            attempt_seed = base_seed if attempt == 1 else (base_seed + attempt * 1009)
            inputs = [
                SynthChunkInput(id=ch.id, text=ch.text, seed=attempt_seed)
                for ch in pending.values()
            ]
            batch_res = chain.synthesize(
                inputs,
                voice,
                params,
                request_id=f"synth-{job_id[:8]}-a{attempt}",
                language="tr",
            )
            total_gpu_ms += batch_res.gpu_ms
            last_engine_id = batch_res.engine_id

            next_pending: dict[str, Chunk] = {}
            out_map = {o.chunk_id: o for o in batch_res.outputs}

            for ch_id, ch_obj in pending.items():
                if ch_id not in out_map:
                    next_pending[ch_id] = ch_obj
                    continue

                raw_out = out_map[ch_id]
                cand_audio = ChunkAudio(
                    chunk_id=ch_id,
                    samples=raw_out.samples,
                    sample_rate=raw_out.sample_rate,
                    attempts=attempt,
                    engine_id=batch_res.engine_id,
                    from_cache=False,
                )
                report = self.quality_checker.check(ch_obj, cand_audio)
                prev_best = best_candidates.get(ch_id)
                if prev_best is None or report.score >= prev_best[0]:
                    best_candidates[ch_id] = (report.score, cand_audio)

                if not report.ok and attempt < max_attempts:
                    next_pending[ch_id] = ch_obj

            pending = next_pending

        final_map: dict[str, ChunkAudio] = {}
        for ch in batch_chunks:
            if ch.id not in best_candidates:
                raise ChapterFailedError(
                    "CHUNK_SYNTHESIS_FAILED",
                    f"Parça '{ch.id}' tüm denemelere rağmen üretilemedi.",
                )
            final_map[ch.id] = best_candidates[ch.id][1]

        return final_map, total_gpu_ms, last_engine_id
