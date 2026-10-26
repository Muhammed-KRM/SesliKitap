"""ChunkPlanner birim testleri (Bölüm 4.15.8)."""

from __future__ import annotations

import pytest

from models.synthesis_models import Sentence
from pipeline.text.chunk_planner import ChunkPlanner


def test_chunk_planner_merges_short_sentences_in_same_paragraph_with_weights() -> None:
    planner = ChunkPlanner(max_chars=280, min_chars=25)
    sentences = [
        Sentence(index=0, paragraph=0, display="Evet.", spoken="Evet."),
        Sentence(index=1, paragraph=0, display="Hemen gidelim.", spoken="Hemen gidelim."),
        Sentence(
            index=2,
            paragraph=1,
            display="Sonraki paragraf yeterince uzun bir cümledir.",
            spoken="Sonraki paragraf yeterince uzun bir cümledir.",
        ),
    ]

    chunks = planner.plan(sentences, chapter_id="01930000-0000-7000-8000-000000000004")

    assert len(chunks) == 2
    assert chunks[0].id == "c-01930000-00001"
    assert chunks[0].text == "Evet. Hemen gidelim."
    assert len(chunks[0].sentence_weights) == 2
    weight_sum = sum(w for _, w in chunks[0].sentence_weights)
    assert weight_sum == pytest.approx(1.0, abs=1e-5)
    assert chunks[0].is_paragraph_end is True


def test_chunk_planner_splits_long_sentence_and_preserves_total_weight_one() -> None:
    planner = ChunkPlanner(max_chars=80, min_chars=15)
    long_spoken = (
        "Bu cümle oldukça uzundur; içinde noktalı virgül, virgüller ve birçok kelime "
        "bulunduğu için alt parçalara bölünmesi gerekir, böylece motor sınırı aşılmaz."
    )
    sentences = [
        Sentence(index=0, paragraph=0, display=long_spoken, spoken=long_spoken),
    ]

    chunks = planner.plan(sentences, chapter_id="abcd1234")

    assert len(chunks) >= 2
    for ch in chunks:
        assert len(ch.text) <= 85
        assert ch.sentence_weights[0][0] == 0

    total_weight = sum(ch.sentence_weights[0][1] for ch in chunks)
    assert total_weight == pytest.approx(1.0, abs=1e-4)
    assert chunks[-1].is_subchunk is False
    assert chunks[0].is_subchunk is True


def test_chunk_planner_keeps_heading_isolated() -> None:
    planner = ChunkPlanner(max_chars=280, min_chars=25)
    sentences = [
        Sentence(
            index=0,
            paragraph=-1,
            display="Birinci Bölüm",
            spoken="Birinci Bölüm.",
            is_heading=True,
        ),
        Sentence(
            index=1,
            paragraph=0,
            display="Kısa.",
            spoken="Kısa.",
        ),
    ]

    chunks = planner.plan(sentences, chapter_id="chap01")

    assert len(chunks) == 2
    assert chunks[0].is_heading is True
    assert chunks[0].sentence_weights == [(0, 1.0)]


def test_chunk_planner_keeps_entire_paragraph_together_until_end() -> None:
    planner = ChunkPlanner(max_chars=580, min_chars=50)
    # 4 cümle içeren normal bir kitap paragrafı
    sentences = [
        Sentence(index=0, paragraph=0, display="Sosyal kuramcı Clark C. Abt güçlü bir iddiada bulunur.", spoken="Sosyal kuramcı Clark C. Abt güçlü bir iddiada bulunur."),
        Sentence(index=1, paragraph=0, display="Oyunların bir şeye belirli bir açıdan bakmak olduğunu belirtir.", spoken="Oyunların bir şeye belirli bir açıdan bakmak olduğunu belirtir."),
        Sentence(index=2, paragraph=0, display="Estetik ve kültür dünyamızın oyunlarla iç içe geçtiği alanları anlamak önemlidir.", spoken="Estetik ve kültür dünyamızın oyunlarla iç içe geçtiği alanları anlamak önemlidir."),
        Sentence(index=3, paragraph=0, display="Bu yaklaşım tasarım uygulamalarımızı zenginleştirir.", spoken="Bu yaklaşım tasarım uygulamalarımızı zenginleştirir."),
    ]

    chunks = planner.plan(sentences, chapter_id="chap02")

    # Paragraf bitimine kadar durmamalı, tüm cümleler tek bir Chunk olmalı
    assert len(chunks) == 1
    assert chunks[0].paragraph == 0
    assert chunks[0].is_paragraph_end is True
    assert len(chunks[0].sentence_weights) == 4
    total_weights = sum(w for _, w in chunks[0].sentence_weights)
    assert total_weights == pytest.approx(1.0, abs=1e-4)


def test_chunk_planner_isolates_bullet_items_as_individual_chunks() -> None:
    planner = ChunkPlanner(max_chars=580, min_chars=50)
    # Liste öncesi başlık cümlesi + 3 ayrı madde
    sentences = [
        Sentence(index=0, paragraph=0, display="Ana şemalarımız:", spoken="Ana şemalarımız:"),
        Sentence(index=1, paragraph=1, display="KURALLAR, oyunların temel mantığına odaklanır.", spoken="KURALLAR, oyunların temel mantığına odaklanır."),
        Sentence(index=2, paragraph=2, display="OYNAM, oyuncu deneyimini temsil eder.", spoken="OYNAM, oyuncu deneyimini temsil eder."),
        Sentence(index=3, paragraph=3, display="KÜLTÜR, geniş bağlamları inceler.", spoken="KÜLTÜR, geniş bağlamları inceler."),
    ]

    chunks = planner.plan(sentences, chapter_id="chap03")

    # Her bir madde bitene kadar durmaz, madde bitiminde sonraki maddeye geçer (4 ayrı Chunk)
    assert len(chunks) == 4
    assert chunks[0].is_paragraph_end is True
    assert chunks[1].is_paragraph_end is True
    assert chunks[2].is_paragraph_end is True
    assert chunks[3].is_paragraph_end is True

