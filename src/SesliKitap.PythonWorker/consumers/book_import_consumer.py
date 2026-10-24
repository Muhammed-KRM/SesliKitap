from __future__ import annotations

from typing import Any

import fitz

from consumers.base_consumer import BaseConsumer
from discovery.safe_downloader import SafeDownloader
from models.events import (
    BookImportRequestedEvent,
    BookProcessedEvent,
    BookSourceStoredEvent,
    DosyaFormati,
    ProcessedBookMeta,
    ProcessedBookStats,
    ProcessedChapterItem,
    ProcessedChaptersPayload,
    ProcessingError,
)
from pipeline.cleaning.scanned_pdf_detector import ScannedPdfDetector
from pipeline.cleaning.text_cleaner import TextCleaner
from pipeline.extraction import get_extractor
from pipeline.extraction.ocr_processor import OcrProcessor
from pipeline.structure.chapter_detector import ChapterDetector
from pipeline.structure.chapter_post_processor import ChapterPostProcessor
from pipeline.structure.metadata_resolver import MetadataResolver
from services.minio_client import MinioStorageClient
from services.pipeline_logger import PipelineLogger
from services.rabbitmq_publisher import RabbitMQPublisher
from utils.constants import Exchanges, Queues, Stage
from utils.janitor import cleanup_job, create_job_temp_dir
from utils.logger import get_logger
from utils.timing import Stopwatch
from utils.turkish import count_words

logger = get_logger(__name__)


class BookImportConsumer(BaseConsumer[BookImportRequestedEvent]):
    """Kitap yükleme/içe aktarma olaylarını dinleyen ve metin çıkarma/temizleme/bölümleme yapan tüketici."""

    queue_name = Queues.BOOK_IMPORT
    exchange_names = [
        Exchanges.BOOK_IMPORT_REQUESTED,
        f"{Exchanges.PREFIX}{Exchanges.BOOK_IMPORT_REQUESTED}",
    ]
    message_model = BookImportRequestedEvent
    default_stage = Stage.METIN_CIKARMA

    def __init__(
        self,
        minio_client: MinioStorageClient | None = None,
        publisher: RabbitMQPublisher | None = None,
        pipeline_logger: PipelineLogger | None = None,
        downloader: SafeDownloader | None = None,
        ocr_processor: OcrProcessor | None = None,
    ) -> None:
        super().__init__(publisher=publisher, pipeline_logger=pipeline_logger)
        self.minio = minio_client or MinioStorageClient()
        self.scanned_detector = ScannedPdfDetector()
        self.metadata_resolver = MetadataResolver()
        self.downloader = downloader or SafeDownloader()
        self.ocr_processor = ocr_processor or OcrProcessor()

    def handle(self, message: BookImportRequestedEvent) -> None:
        book_id = message.book_id
        trace_id = message.trace_id
        sw = Stopwatch()
        job_dir = create_job_temp_dir(f"book_{book_id}")

        logger.info(
            "Kitap işleme başladı: bookId=%s, format=%s, kaynak=%s",
            book_id,
            message.beklenen_format,
            message.orijinal_dosya_yolu or message.indirme_url,
        )

        try:
            # 1) %2 - İndirme
            self.default_stage = Stage.INDIRME
            self._report_stage(
                book_id=book_id,
                stage=Stage.INDIRME,
                pct=2,
                msg="Kaynak kitap dosyası indiriliyor...",
                status="Basladi",
                trace_id=trace_id,
            )

            ext = (message.beklenen_format or self._format_to_ext(message.dosya_formati)).lower().lstrip(".")
            local_source = job_dir / f"original.{ext}"

            if message.orijinal_dosya_yolu:
                self.minio.download_file(message.orijinal_dosya_yolu, local_source)
            elif message.indirme_url:
                detected_fmt, size_bytes, file_hash = self.downloader.download(
                    message.indirme_url,
                    local_source,
                    ext,
                )
                source_key = f"books/{book_id}/source/original.{ext}"
                content_type = (
                    "application/epub+zip"
                    if ext == "epub"
                    else "application/pdf"
                    if ext == "pdf"
                    else "text/plain; charset=utf-8"
                )
                self.minio.upload_file(source_key, local_source, content_type=content_type)
                self.publisher.publish_book_source_stored(
                    BookSourceStoredEvent(
                        trace_id=trace_id,
                        book_id=book_id,
                        orijinal_dosya_yolu=source_key,
                        dosya_formati=detected_fmt,
                        dosya_boyutu=size_bytes,
                        dosya_hash=file_hash,
                    )
                )
            else:
                raise ProcessingError(
                    "MISSING_SOURCE_PATH",
                    "Kitap kaynak dosya yolu veya indirme adresi bulunamadı.",
                    recoverable=False,
                )

            # 1.5) %8 - Taranmış PDF için OCRmyPDF (Faz 8)
            if ext == "pdf" and message.ayarlar.ocr_uygula:
                self.default_stage = Stage.OCR
                self._report_stage(
                    book_id=book_id,
                    stage=Stage.OCR,
                    pct=8,
                    msg="Taranmış PDF sayfalarına Türkçe OCR uygulanıyor...",
                    trace_id=trace_id,
                )
                ocr_output = job_dir / "ocr_output.pdf"
                local_source = self.ocr_processor.process(local_source, ocr_output)

            # 2) %10 - Metin Çıkarma
            self.default_stage = Stage.METIN_CIKARMA
            self._report_stage(
                book_id=book_id,
                stage=Stage.METIN_CIKARMA,
                pct=10,
                msg="Dosyadan ham metin ve yapı çıkarılıyor...",
                trace_id=trace_id,
            )
            extractor = get_extractor(ext)
            doc = extractor.extract(local_source)

            # 3) %15 - Taranmış PDF Kontrolü
            self._report_stage(
                book_id=book_id,
                stage=Stage.METIN_CIKARMA,
                pct=15,
                msg="Taranmış sayfa kontrolü yapılıyor...",
                trace_id=trace_id,
            )
            scan_result = self.scanned_detector.analyze(doc)
            if scan_result.is_scanned and not message.ayarlar.ocr_uygula:
                logger.warning(
                    "Kitap taranmış PDF olarak tespit edildi: bookId=%s, oran=%.2f",
                    book_id,
                    scan_result.scanned_ratio,
                )
                ocr_report = {
                    "bookId": book_id,
                    "format": doc.format,
                    "ocrRequired": True,
                    "totalPages": scan_result.total_pages,
                    "lowTextImagePages": scan_result.low_text_image_pages,
                    "scannedRatio": round(scan_result.scanned_ratio, 4),
                }
                self.minio.write_json(f"books/{book_id}/processed/extraction-report.json", ocr_report)

                ocr_event = BookProcessedEvent(
                    trace_id=trace_id,
                    book_id=book_id,
                    bolumler_yolu=None,
                    kapak_yolu=None,
                    meta=ProcessedBookMeta(
                        baslik=doc.meta.title,
                        yazar=doc.meta.author,
                        dil=doc.meta.language or "tr",
                        yayin_yili=doc.meta.year,
                        sayfa_sayisi=scan_result.total_pages,
                    ),
                    istatistik=ProcessedBookStats(bolum_tespit_yontemi="ocr_required"),
                    ocr_gerekli=True,
                )
                self.publisher.publish_book_processed(ocr_event)
                self._report_stage(
                    book_id=book_id,
                    stage=Stage.TAMAMLANDI,
                    pct=100,
                    msg="PDF taranmış görüntü içeriyor, OCR gerekli.",
                    status="Uyari",
                    trace_id=trace_id,
                )
                return

            # 4) %20-60 - Temizleme
            self.default_stage = Stage.TEMIZLEME
            self._report_stage(
                book_id=book_id,
                stage=Stage.TEMIZLEME,
                pct=20,
                msg="Üst/alt bilgiler, sayfa numaraları ve dipnotlar temizleniyor...",
                trace_id=trace_id,
            )
            cleaner = TextCleaner(message.ayarlar)
            cleaned = cleaner.clean(
                doc,
                progress=lambda pct, msg: self._report_stage(
                    book_id=book_id,
                    stage=Stage.TEMIZLEME,
                    pct=pct,
                    msg=msg,
                    trace_id=trace_id,
                ),
            )

            if not cleaned.all_paragraphs:
                raise ProcessingError(
                    "EMPTY_BOOK_CONTENT",
                    "Kitaptan okunabilir metin çıkarılamadı.",
                    recoverable=False,
                )

            # 5) %65-80 - Bölüm Tespiti
            self.default_stage = Stage.BOLUM_TESPITI
            self._report_stage(
                book_id=book_id,
                stage=Stage.BOLUM_TESPITI,
                pct=65,
                msg="Bölüm sınırları ve başlık yapısı tespit ediliyor...",
                trace_id=trace_id,
            )
            detector = ChapterDetector(pseudo_chapter_words=message.ayarlar.sozde_bolum_kelime)
            raw_drafts, detection_method = detector.detect(doc, cleaned)

            self._report_stage(
                book_id=book_id,
                stage=Stage.BOLUM_TESPITI,
                pct=75,
                msg="Kısa bölümler birleştiriliyor ve hariç tutma kuralları uygulanıyor...",
                trace_id=trace_id,
            )
            post_processor = ChapterPostProcessor(min_chapter_words=message.ayarlar.min_bolum_kelime)
            final_drafts = post_processor.process(raw_drafts)

            if not final_drafts:
                raise ProcessingError(
                    "NO_CHAPTERS_DETECTED",
                    "Kitapta seslendirilebilecek bölüm oluşturulamadı.",
                    recoverable=False,
                )

            meta = self.metadata_resolver.resolve(doc, cleaned, source_path=local_source)

            # 6) %85 - Kapak Üretimi
            self.default_stage = Stage.KAPAK_URETIMI
            self._report_stage(
                book_id=book_id,
                stage=Stage.KAPAK_URETIMI,
                pct=85,
                msg="Kitap kapağı hazırlanıyor...",
                trace_id=trace_id,
            )
            cover_key: str | None = None
            if doc.cover_bytes:
                jpeg_bytes = self._prepare_cover_jpeg(doc.cover_bytes)
                if jpeg_bytes:
                    cover_key = f"books/{book_id}/cover.jpg"
                    self.minio.upload_bytes(cover_key, jpeg_bytes, content_type="image/jpeg")

            # 7) %90 - Yükleme (chapters.json + extraction-report.json)
            self.default_stage = Stage.YUKLEME
            self._report_stage(
                book_id=book_id,
                stage=Stage.YUKLEME,
                pct=90,
                msg="Bölümler ve çıkarım raporu depolamaya yükleniyor...",
                trace_id=trace_id,
            )

            chapter_items: list[ProcessedChapterItem] = []
            for idx, draft in enumerate(final_drafts, start=1):
                clean_text = draft.clean_text
                chapter_items.append(
                    ProcessedChapterItem(
                        sira_no=idx,
                        baslik=draft.title,
                        temiz_metin=clean_text,
                        kelime_sayisi=count_words(clean_text),
                        karakter_sayisi=len(clean_text),
                        dahil_mi=draft.included,
                        otomatik_haric_nedeni=draft.exclusion_reason,
                    )
                )

            chapters_payload = ProcessedChaptersPayload(book_id=book_id, chapters=chapter_items)
            chapters_key = f"books/{book_id}/processed/chapters.json"
            self.minio.write_json(chapters_key, chapters_payload)

            extraction_report: dict[str, Any] = {
                "bookId": book_id,
                "format": doc.format,
                "detectionMethod": detection_method,
                "pageCount": meta.sayfa_sayisi,
                "chapterCount": len(chapter_items),
                "includedChapterCount": sum(1 for c in chapter_items if c.dahil_mi),
                "totalWords": sum(c.kelime_sayisi for c in chapter_items if c.dahil_mi),
                "bodyFontSize": round(cleaned.body_font_size, 2),
                "deletedHeadersCount": cleaned.stats.deleted_headers,
                "deletedPageNumbersCount": cleaned.stats.deleted_page_numbers,
                "deletedFootnotesCount": cleaned.stats.deleted_footnotes,
                "hyphenMergesCount": cleaned.stats.merged_hyphens,
                "sampleDeletedLines": cleaned.stats.sample_deleted_lines,
                "scannedRatio": round(scan_result.scanned_ratio, 4),
                "elapsedMs": sw.elapsed_ms(),
            }
            report_key = f"books/{book_id}/processed/extraction-report.json"
            self.minio.write_json(report_key, extraction_report)

            # 8) %100 - Tamamlandı (BookProcessedEvent)
            self.default_stage = Stage.TAMAMLANDI
            stats = ProcessedBookStats(
                silinen_ust_bilgi=cleaned.stats.deleted_headers,
                silinen_sayfa_no=cleaned.stats.deleted_page_numbers,
                silinen_dipnot=cleaned.stats.deleted_footnotes,
                birlesen_tire=cleaned.stats.merged_hyphens,
                bolum_tespit_yontemi=detection_method,
            )
            processed_event = BookProcessedEvent(
                trace_id=trace_id,
                book_id=book_id,
                bolumler_yolu=chapters_key,
                kapak_yolu=cover_key,
                meta=meta,
                istatistik=stats,
                ocr_gerekli=False,
            )
            self.publisher.publish_book_processed(processed_event)

            self._report_stage(
                book_id=book_id,
                stage=Stage.TAMAMLANDI,
                pct=100,
                msg=f"Kitap işleme tamamlandı ({len(chapter_items)} bölüm).",
                status="Tamamlandi",
                detay={"bolumSayisi": len(chapter_items), "sureMs": sw.elapsed_ms()},
                trace_id=trace_id,
            )
            logger.info(
                "Kitap işleme tamamlandı: bookId=%s, bolum=%d, yontem=%s, sure=%d ms",
                book_id,
                len(chapter_items),
                detection_method,
                sw.elapsed_ms(),
            )
        finally:
            cleanup_job(job_dir)

    def _report_stage(
        self,
        *,
        book_id: str,
        stage: str,
        pct: int,
        msg: str,
        status: str = "Devam",
        detay: dict[str, Any] | None = None,
        trace_id: str | None = None,
    ) -> None:
        try:
            self.publisher.publish_stage_changed(
                book_id=book_id,
                asama=stage,
                yuzde=pct,
                mesaj=msg,
                detay=detay,
                trace_id=trace_id,
            )
        except Exception as exc:
            logger.warning("İlerleme olayı yayınlanamadı (%s %d%%): %s", stage, pct, exc)

        self.pipeline_logger.log_event(
            asama=stage,
            durum=status,
            yuzde=pct,
            mesaj=msg,
            book_id=book_id,
            cikti_metadata=detay,
            trace_id=trace_id,
        )

    @staticmethod
    def _format_to_ext(fmt: DosyaFormati) -> str:
        if fmt == DosyaFormati.EPUB:
            return "epub"
        if fmt == DosyaFormati.TXT:
            return "txt"
        return "pdf"

    @staticmethod
    def _prepare_cover_jpeg(cover_bytes: bytes) -> bytes | None:
        """Kapak baytlarını PyMuPDF Pixmap ile <= 600px genişlikte JPEG (q85) formatına dönüştürür."""
        try:
            pix = fitz.Pixmap(cover_bytes)
            if pix.alpha or (pix.colorspace and pix.colorspace.n != 3):
                pix = fitz.Pixmap(fitz.csRGB, pix)

            if pix.width > 600:
                scale = 600.0 / float(pix.width)
                target_w = 600
                target_h = max(1, int(round(pix.height * scale)))
                doc = fitz.open()
                try:
                    page = doc.new_page(width=target_w, height=target_h)
                    page.insert_image(fitz.Rect(0, 0, target_w, target_h), pixmap=pix)
                    scaled_pix = page.get_pixmap(dpi=72, alpha=False)
                    return scaled_pix.tobytes("jpeg", jpg_quality=85)
                finally:
                    doc.close()

            return pix.tobytes("jpeg", jpg_quality=85)
        except Exception as exc:
            logger.warning("Kapak görüntüsü JPEG'e dönüştürülemedi: %s", exc)
            return None
