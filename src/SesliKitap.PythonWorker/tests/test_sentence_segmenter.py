"""SentenceSegmenter birim testleri (Bölüm 4.15.8)."""

from __future__ import annotations

from pipeline.text.sentence_segmenter import SentenceSegmenter


def test_segmenter_preserves_abbreviations_and_initials() -> None:
    segmenter = SentenceSegmenter()
    text = (
        "Prof. Dr. Ahmet Hamdi Tanpınar 19. yy. edebiyatını inceledi. "
        "A. Hamdi Tanpınar eserlerinde zaman kavramını işler (bkz. s. 42, vb. kaynaklar)."
    )

    sentences = segmenter.split_sentences(text)

    assert len(sentences) == 2
    assert sentences[0].display == "Prof. Dr. Ahmet Hamdi Tanpınar 19. yy. edebiyatını inceledi."
    assert (
        sentences[1].display
        == "A. Hamdi Tanpınar eserlerinde zaman kavramını işler (bkz. s. 42, vb. kaynaklar)."
    )


def test_segmenter_handles_decimals_dates_times_and_ordinals() -> None:
    segmenter = SentenceSegmenter()
    text = (
        "Pi sayısı 3.14 olarak bilinir ve 3. bölümde anlatılır. "
        "Toplantı 29.10.1923 tarihinde saat 14.30 civarında başladı."
    )

    sentences = segmenter.split_sentences(text)

    assert len(sentences) == 2
    assert "3.14" in sentences[0].display
    assert "3. bölümde" in sentences[0].display
    assert "29.10.1923" in sentences[1].display


def test_segmenter_handles_ellipsis_followed_by_lowercase_vs_uppercase() -> None:
    segmenter = SentenceSegmenter()
    text = "Kapıyı yavaşça açtı... ama içeride kimse yoktu. Sonra dışarı çıktı… Hava çok soğuktu."

    sentences = segmenter.split_sentences(text)

    assert len(sentences) == 3
    assert sentences[0].display == "Kapıyı yavaşça açtı... ama içeride kimse yoktu."
    assert sentences[1].display == "Sonra dışarı çıktı…"
    assert sentences[2].display == "Hava çok soğuktu."


def test_segmenter_handles_dialogue_and_quoted_questions() -> None:
    segmenter = SentenceSegmenter()
    text = (
        "Raif Efendi sordu:\n"
        "— Nereye gidiyorsun? Hemen dönecek misin?\n"
        "— Evet, birazdan dönerim.\n\n"
        "\"Gerçekten gelecek mi?\" diye düşündü. Emin olamadı."
    )

    paragraphs = segmenter.split_paragraphs(text)

    assert len(paragraphs) == 2
    all_sentences = [s for p in paragraphs for s in p.sentences]
    assert len(all_sentences) == 6
    assert all_sentences[1].is_dialogue is True
    assert all_sentences[2].is_dialogue is True
    assert all_sentences[3].is_dialogue is True
    assert all_sentences[4].display == '"Gerçekten gelecek mi?" diye düşündü.'
