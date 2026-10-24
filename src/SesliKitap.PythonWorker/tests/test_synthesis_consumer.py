"""SynthesisConsumer ve çapraz dil sözleşme (contract) birim testleri (Bölüm 4.15.8 & Adım 4.7)."""

from __future__ import annotations

import io
from pathlib import Path

import numpy as np
import soundfile as sf

from consumers.synthesis_consumer import SynthesisConsumer
from models.events import ChapterSynthesizedEvent, SynthesisRequestedEvent
from models.synthesis_models import SynthesisInput
from models.voice_models import BatchResult, EngineInfo, PreparedVoice, SynthChunkInput, VoiceParams
from pipeline.tts.base_engine import BaseTtsEngine, EngineUnavailableError
from pipeline.tts.chunk_cache import ChunkCache
from pipeline.tts.engine_chain import TtsEngineChain
from pipeline.tts.mock_engine import MockTtsEngine
from services.redis_client import CancelChecker
from tests.conftest import FakeMinioClient, FakePipelineLogger, FakeRabbitMQPublisher, FakeRedis
from utils.constants import RedisKeys


def _make_ref_wav_bytes() -> bytes:
    sr = 24000
    t = np.linspace(0.0, 2.0, sr * 2, endpoint=False, dtype=np.float32)
    samples = (0.25 * np.sin(2.0 * np.pi * 220.0 * t)).astype(np.float32)
    buf = io.BytesIO()
    sf.write(buf, samples, sr, format="WAV", subtype="PCM_16")
    return buf.getvalue()


def _seed_two_chapter_job(fake_minio: FakeMinioClient, job_id: str = "job-001") -> SynthesisRequestedEvent:
    ref_key = "voices/v1/reference.wav"
    fake_minio.upload_bytes(ref_key, _make_ref_wav_bytes(), content_type="audio/wav")

    input_payload = {
        "jobId": job_id,
        "book": {
            "id": "book-001",
            "baslik": "Kürk Mantolu Madonna",
            "yazar": "Sabahattin Ali",
            "kapakYolu": None,
        },
        "voiceProfile": {
            "id": "voice-001",
            "ad": "Anlatıcı",
            "referansSesYolu": ref_key,
            "referansHash": "1" * 64,
            "referansMetin": "Merhaba dünya.",
            "parametreler": {
                "cfgValue": 2.0,
                "inferenceTimesteps": 10,
                "konusmaHizi": 1.0,
                "tohum": 42,
            },
        },
        "motorSirasi": ["mock"],
        "ayarlar": {
            "cumleArasiMs": 420,
            "paragrafArasiMs": 950,
            "baslikSonrasiMs": 1400,
            "diyalogMs": 320,
            "bolumBasligiOku": True,
            "opusBitrateKbps": 48,
            "mp3BitrateKbps": 64,
            "hedefLufs": -18.0,
        },
        "telaffuzSozlugu": [],
        "chapters": [
            {
                "id": "chap-001",
                "siraNo": 1,
                "baslik": "Birinci Bölüm",
                "metinSurumu": 1,
                "temizMetin": "Raif Efendi sessiz bir adamdı. Berlin yıllarını kimseye anlatmazdı.",
                "sesSurumu": None,
            },
            {
                "id": "chap-002",
                "siraNo": 2,
                "baslik": "İkinci Bölüm",
                "metinSurumu": 1,
                "temizMetin": "Sergideki tabloya uzun uzun baktı. Maria Puder ile tanıştı.",
                "sesSurumu": None,
            },
        ],
    }

    input_key = f"synthesis/{job_id}/input.json"
    fake_minio.write_json(input_key, input_payload)
    return SynthesisRequestedEvent(job_id=job_id, book_id="book-001", girdi_yolu=input_key)


def test_synthesis_consumer_two_chapters_and_second_run_from_cache(
    fake_minio: FakeMinioClient,
    fake_publisher: FakeRabbitMQPublisher,
    fake_pipeline_logger: FakePipelineLogger,
    fake_redis: FakeRedis,
) -> None:
    req_event = _seed_two_chapter_job(fake_minio, job_id="job-cache-test")
    cache = ChunkCache(minio=fake_minio, redis_client=fake_redis)  # type: ignore[arg-type]
    cancel_checker = CancelChecker(redis_client=fake_redis, cache_ttl_sec=0.0)  # type: ignore[arg-type]

    def _chain_factory(order: list[str], on_fb: object) -> TtsEngineChain:
        return TtsEngineChain(
            engines={"mock": MockTtsEngine(allow_mock=True)},
            order=order,
        )

    consumer = SynthesisConsumer(
        publisher=fake_publisher,  # type: ignore[arg-type]
        pipeline_logger=fake_pipeline_logger,  # type: ignore[arg-type]
        minio_client=fake_minio,  # type: ignore[arg-type]
        cancel_checker=cancel_checker,
        chunk_cache=cache,
        chain_factory=_chain_factory,  # type: ignore[arg-type]
    )

    # 1. Çalıştırma: tüm parçalar üretilir ve önbelleğe yazılır
    consumer.handle(req_event)

    assert len(fake_publisher.chapter_synthesized_events) == 2
    assert len(fake_publisher.synthesis_completed_events) == 1
    first_ch_evt = fake_publisher.chapter_synthesized_events[0]
    assert first_ch_evt.uretilen_parca > 0
    assert first_ch_evt.onbellek_parca == 0
    assert fake_publisher.synthesis_completed_events[0].sonuc == "Tamamlandi"

    # 2. Çalıştırma: aynı girdi -> tüm parçalar önbellekten gelir (uretilen_parca == 0)
    fake_publisher.chapter_synthesized_events.clear()
    fake_publisher.synthesis_completed_events.clear()

    consumer.handle(req_event)

    assert len(fake_publisher.chapter_synthesized_events) == 2
    for ch_evt in fake_publisher.chapter_synthesized_events:
        assert ch_evt.uretilen_parca == 0
        assert ch_evt.onbellek_parca > 0


def test_synthesis_consumer_honors_cancel_flag(
    fake_minio: FakeMinioClient,
    fake_publisher: FakeRabbitMQPublisher,
    fake_pipeline_logger: FakePipelineLogger,
    fake_redis: FakeRedis,
) -> None:
    req_event = _seed_two_chapter_job(fake_minio, job_id="job-cancel-test")
    fake_redis.set(RedisKeys.job_cancel("job-cancel-test"), "1")

    cancel_checker = CancelChecker(redis_client=fake_redis, cache_ttl_sec=0.0)  # type: ignore[arg-type]
    consumer = SynthesisConsumer(
        publisher=fake_publisher,  # type: ignore[arg-type]
        pipeline_logger=fake_pipeline_logger,  # type: ignore[arg-type]
        minio_client=fake_minio,  # type: ignore[arg-type]
        cancel_checker=cancel_checker,
        chunk_cache=ChunkCache(minio=fake_minio, redis_client=fake_redis),  # type: ignore[arg-type]
        chain_factory=lambda order, _fb: TtsEngineChain(
            engines={"mock": MockTtsEngine(allow_mock=True)}, order=order
        ),
    )

    consumer.handle(req_event)

    assert len(fake_publisher.chapter_synthesized_events) == 0
    assert len(fake_publisher.synthesis_completed_events) == 1
    comp = fake_publisher.synthesis_completed_events[0]
    assert comp.iptal_edildi is True
    assert comp.sonuc == "IptalEdildi"


def test_synthesis_consumer_recovers_from_single_chapter_error(
    fake_minio: FakeMinioClient,
    fake_publisher: FakeRabbitMQPublisher,
    fake_pipeline_logger: FakePipelineLogger,
    fake_redis: FakeRedis,
) -> None:
    req_event = _seed_two_chapter_job(fake_minio, job_id="job-partial-err")
    # 1. bölümün metnini boşaltalım -> 1. bölüm hata verir, 2. bölüm tamamlanır
    raw_input = fake_minio.read_json(req_event.girdi_yolu)
    assert isinstance(raw_input, dict)
    raw_input["chapters"][0]["temizMetin"] = "   "
    raw_input["ayarlar"]["bolumBasligiOku"] = False
    fake_minio.write_json(req_event.girdi_yolu, raw_input)

    consumer = SynthesisConsumer(
        publisher=fake_publisher,  # type: ignore[arg-type]
        pipeline_logger=fake_pipeline_logger,  # type: ignore[arg-type]
        minio_client=fake_minio,  # type: ignore[arg-type]
        cancel_checker=CancelChecker(redis_client=fake_redis, cache_ttl_sec=0.0),  # type: ignore[arg-type]
        chunk_cache=ChunkCache(minio=fake_minio, redis_client=fake_redis),  # type: ignore[arg-type]
        chain_factory=lambda order, _fb: TtsEngineChain(
            engines={"mock": MockTtsEngine(allow_mock=True)}, order=order
        ),
    )

    consumer.handle(req_event)

    assert len(fake_publisher.error_events) == 1
    assert fake_publisher.error_events[0].chapter_id == "chap-001"
    assert len(fake_publisher.chapter_synthesized_events) == 1
    assert fake_publisher.chapter_synthesized_events[0].chapter_id == "chap-002"
    assert fake_publisher.synthesis_completed_events[0].sonuc == "KismenTamamlandi"


def test_synthesis_consumer_switches_family_when_pinned_family_exhausted(
    fake_minio: FakeMinioClient,
    fake_publisher: FakeRabbitMQPublisher,
    fake_pipeline_logger: FakePipelineLogger,
    fake_redis: FakeRedis,
) -> None:
    class FailingVoxEngine(BaseTtsEngine):
        engine_id = "voxcpm2_modal"
        family = "voxcpm2"

        def is_available(self) -> bool:
            return True

        def info(self) -> EngineInfo:
            return EngineInfo(
                engine="voxcpm2",
                model_id="openbmb/VoxCPM2",
                model_revision="rev1",
                sample_rate=24000,
            )

        def has_voice(self, voice_hash: str) -> bool:
            return True

        def register_voice(self, voice_hash: str, wav_bytes: bytes, prompt_text: str | None = None) -> None:
            pass

        def ensure_voice(self, voice: PreparedVoice) -> None:
            pass

        def synthesize(
            self,
            chunks: list[SynthChunkInput],
            voice: PreparedVoice,
            params: VoiceParams,
            *,
            request_id: str = "",
            language: str = "tr",
        ) -> BatchResult:
            raise EngineUnavailableError("voxcpm2_modal", "HTTP 429 quota exceeded", quota_exhausted=True)

    req_event = _seed_two_chapter_job(fake_minio, job_id="job-family-switch")
    raw_input = fake_minio.read_json(req_event.girdi_yolu)
    assert isinstance(raw_input, dict)
    raw_input["motorSirasi"] = ["voxcpm2_modal", "mock"]
    fake_minio.write_json(req_event.girdi_yolu, raw_input)

    consumer = SynthesisConsumer(
        publisher=fake_publisher,  # type: ignore[arg-type]
        pipeline_logger=fake_pipeline_logger,  # type: ignore[arg-type]
        minio_client=fake_minio,  # type: ignore[arg-type]
        cancel_checker=CancelChecker(redis_client=fake_redis, cache_ttl_sec=0.0),  # type: ignore[arg-type]
        chunk_cache=ChunkCache(minio=fake_minio, redis_client=fake_redis),  # type: ignore[arg-type]
        chain_factory=lambda order, on_fb: TtsEngineChain(
            engines={
                "voxcpm2_modal": FailingVoxEngine(),
                "mock": MockTtsEngine(allow_mock=True),
            },
            order=order,
            on_fallback=on_fb,
        ),
    )

    consumer.handle(req_event)

    assert len(fake_publisher.chapter_synthesized_events) == 2
    assert fake_publisher.chapter_synthesized_events[0].motor == "mock"
    assert fake_publisher.synthesis_completed_events[0].sonuc == "Tamamlandi"


def test_cross_language_contracts_with_dotnet_samples() -> None:
    repo_root = Path(__file__).resolve().parents[3]
    contracts_dir = repo_root / "tests" / "contracts"

    # 1. .NET SynthesisInputPayload -> Python SynthesisInput.model_validate_json
    input_sample_path = contracts_dir / "synthesis_input.sample.json"
    parsed_input = SynthesisInput.model_validate_json(
        input_sample_path.read_text(encoding="utf-8")
    )
    assert parsed_input.job_id == "01930000-0000-7000-8000-000000000001"
    assert parsed_input.book.baslik == "Kürk Mantolu Madonna"
    assert parsed_input.voice_profile.referans_hash == "1" * 64
    assert parsed_input.ayarlar.cumle_arasi_ms == 420
    assert len(parsed_input.chapters) == 1
    assert parsed_input.chapters[0].sira_no == 1

    # 2. Python ChapterSynthesizedEvent <-> .NET ChapterSynthesizedEvent sample
    ch_sample_path = contracts_dir / "chapter_synthesized.sample.json"
    parsed_ch = ChapterSynthesizedEvent.model_validate_json(
        ch_sample_path.read_text(encoding="utf-8")
    )
    assert parsed_ch.job_id == "01930000-0000-7000-8000-000000000001"
    assert parsed_ch.opus_boyutu == 124500
    assert parsed_ch.uretilen_parca == 11
    dumped = parsed_ch.model_dump(by_alias=True, mode="json")
    assert dumped["opusYolu"].endswith("/v1/audio.ogg")
    assert dumped["zamanDamgasiYolu"].endswith("/v1/timestamps.json")

    # 3. Redis iptal anahtarı sözleşmesi
    sample_guid = "01930000-0000-7000-8000-000000000001"
    assert RedisKeys.job_cancel(sample_guid) == "sk:job:01930000-0000-7000-8000-000000000001:cancel"
