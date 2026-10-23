from __future__ import annotations

from pathlib import Path

import pytest

from models.events import ProcessingError
from pipeline.extraction.txt_extractor import TxtExtractor

SAMPLE_TR_TEXT = (
    "Birinci paragraf: Çağdaş Türk edebiyatında ğ, ş, ı, İ, ç, ö, ü harfleri çok önemlidir.\n\n"
    "İkinci paragraf: İstanbul'un yağmurlu bir sonbahar gününde Boğaziçi kıyısında yürüyüş yaptık."
)


@pytest.mark.parametrize("encoding", ["utf-8", "utf-8-sig", "cp1254", "utf-16"])
def test_txt_extractor_decodes_turkish_encodings_accurately(tmp_path: Path, encoding: str) -> None:
    txt_path = tmp_path / f"kitap_{encoding}.txt"
    txt_path.write_bytes(SAMPLE_TR_TEXT.encode(encoding))

    extractor = TxtExtractor()
    doc = extractor.extract(txt_path)

    assert doc.format == "txt"
    assert len(doc.pages) == 1
    extracted_lines = [b.lines[0].text for b in doc.pages[0].blocks]
    combined = "\n".join(extracted_lines)

    assert "Çağdaş Türk edebiyatında ğ, ş, ı, İ, ç, ö, ü" in combined
    assert "İstanbul'un yağmurlu bir sonbahar gününde Boğaziçi" in combined


def test_txt_extractor_rejects_empty_file(tmp_path: Path) -> None:
    empty_path = tmp_path / "empty.txt"
    empty_path.write_text("   \n\n  ", encoding="utf-8")

    extractor = TxtExtractor()
    with pytest.raises(ProcessingError) as exc_info:
        extractor.extract(empty_path)

    assert exc_info.value.code == "TXT_EMPTY"
