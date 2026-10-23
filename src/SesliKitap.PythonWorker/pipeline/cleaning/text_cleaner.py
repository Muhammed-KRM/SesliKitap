from __future__ import annotations

from models.book_models import CleanedDocument, CleaningStats, ExtractedDocument
from models.events import ProcessingSettingsModel
from pipeline.cleaning.footnote_remover import FootnoteRemover
from pipeline.cleaning.header_footer_remover import HeaderFooterRemover
from pipeline.cleaning.line_joiner import LineJoiner
from pipeline.extraction.base_extractor import ProgressCallback


class TextCleaner:
    """Temizlik adımlarını (HeaderFooterRemover -> FootnoteRemover -> LineJoiner) sırayla çalıştıran orkestratör."""

    def __init__(self, settings: ProcessingSettingsModel | None = None) -> None:
        cfg = settings or ProcessingSettingsModel()
        self.header_footer_remover = HeaderFooterRemover(
            region_ratio=cfg.ust_alt_bolge_orani,
            repeat_threshold=cfg.tekrar_esigi,
        )
        self.footnote_remover = FootnoteRemover()
        self.line_joiner = LineJoiner()

    def clean(
        self,
        doc: ExtractedDocument,
        progress: ProgressCallback | None = None,
    ) -> CleanedDocument:
        stats = CleaningStats()

        if doc.format == "pdf":
            if progress is not None:
                progress(25, "Sayfa üst/alt bilgileri ve sayfa numaraları temizleniyor...")
            self.header_footer_remover.remove(doc, stats)

        if progress is not None:
            progress(40, "Dipnotlar ve referans işaretleri temizleniyor...")
        body_font_size = self.footnote_remover.remove(doc, stats)

        if progress is not None:
            progress(55, "Satırlar, tireli kelimeler ve paragraflar birleştiriliyor...")
        paragraphs_by_page = self.line_joiner.join_document(doc, body_font_size, stats)

        return CleanedDocument(
            paragraphs_by_page=paragraphs_by_page,
            body_font_size=body_font_size,
            stats=stats,
        )
