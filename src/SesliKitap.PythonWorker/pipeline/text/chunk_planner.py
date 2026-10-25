"""Normalize edilmiş cümleleri TTS parçalarına dönüştüren ve cümle ağırlıklarını koruyan planlayıcı (Bölüm 4.15.6)."""

from __future__ import annotations

import re

from models.synthesis_models import Chunk, Sentence


class ChunkPlanner:
    """Normalize edilmiş cümleleri TTS parçalarına dönüştürür; zaman damgası eşlemesini korur."""

    def __init__(self, max_chars: int = 480, min_chars: int = 70) -> None:
        if max_chars < 50:
            raise ValueError("max_chars en az 50 olmalıdır.")
        if min_chars < 1 or min_chars >= max_chars:
            raise ValueError("min_chars 1 ile max_chars arasında olmalıdır.")
        self.max_chars = max_chars
        self.min_chars = min_chars

    def plan(self, sentences: list[Sentence], chapter_id: str = "chap0000") -> list[Chunk]:
        """Cümle listesini deterministik kimlikli ve ağırlık toplamı 1.0 olan parçalara (Chunk) dönüştürür."""
        valid_sentences = [s for s in sentences if s.spoken and s.spoken.strip()]
        if not valid_sentences:
            return []

        chapter_short = chapter_id.replace("-", "")[:8] or "chap0000"
        chunks: list[Chunk] = []
        chunk_counter = 1

        # Paragraf sonu ve diyalog geçişlerini belirlemek için ön indeksleme
        total = len(valid_sentences)
        i = 0

        while i < total:
            curr = valid_sentences[i]
            spoken = curr.spoken.strip()

            # Kural 0: Başlık cümlesi her zaman tek başına bir parçadır
            if curr.is_heading:
                chunks.append(
                    Chunk(
                        id=f"c-{chapter_short}-{chunk_counter:05d}",
                        text=spoken,
                        sentence_weights=[(curr.index, 1.0)],
                        paragraph=curr.paragraph,
                        is_paragraph_end=True,
                        is_heading=True,
                        is_dialogue_transition=False,
                        is_subchunk=False,
                    )
                )
                chunk_counter += 1
                i += 1
                continue

            # Kural 3: Çok uzun cümle (> max_chars) -> alt-parçalara bölünür
            if len(spoken) > self.max_chars:
                sub_texts = self._split_long_text(spoken, self.max_chars)
                total_sub_chars = sum(max(1, len(st)) for st in sub_texts)
                is_para_end = (i + 1 >= total) or (valid_sentences[i + 1].paragraph != curr.paragraph)
                is_diag_trans = (
                    (i + 1 < total)
                    and (valid_sentences[i + 1].paragraph == curr.paragraph)
                    and (valid_sentences[i + 1].is_dialogue != curr.is_dialogue)
                )

                running_weight = 0.0
                for sub_idx, st in enumerate(sub_texts):
                    is_last_sub = sub_idx == len(sub_texts) - 1
                    if is_last_sub:
                        w = round(max(0.0001, 1.0 - running_weight), 6)
                    else:
                        w = round(max(1, len(st)) / total_sub_chars, 6)
                        running_weight += w

                    chunks.append(
                        Chunk(
                            id=f"c-{chapter_short}-{chunk_counter:05d}",
                            text=st,
                            sentence_weights=[(curr.index, w)],
                            paragraph=curr.paragraph,
                            is_paragraph_end=is_para_end if is_last_sub else False,
                            is_heading=False,
                            is_dialogue_transition=is_diag_trans if is_last_sub else False,
                            is_subchunk=not is_last_sub,
                        )
                    )
                    chunk_counter += 1

                i += 1
                continue

            # Kural 2: Kısa cümle (< min_chars) -> aynı paragraftaki sonraki cümle(ler)le birleştir
            if len(spoken) < self.min_chars and (i + 1 < total):
                next_s = valid_sentences[i + 1]
                if (
                    not next_s.is_heading
                    and next_s.paragraph == curr.paragraph
                    and len(spoken) + 1 + len(next_s.spoken.strip()) <= self.max_chars
                ):
                    group: list[Sentence] = [curr]
                    combined_text = spoken
                    j = i + 1
                    while (
                        j < total
                        and len(combined_text) < self.min_chars
                        and not valid_sentences[j].is_heading
                        and valid_sentences[j].paragraph == curr.paragraph
                    ):
                        cand = valid_sentences[j]
                        cand_text = cand.spoken.strip()
                        if len(combined_text) + 1 + len(cand_text) > self.max_chars:
                            break
                        group.append(cand)
                        combined_text = f"{combined_text} {cand_text}"
                        j += 1

                    if len(group) > 1:
                        char_lengths = [max(1, len(s.spoken.strip())) for s in group]
                        sum_chars = sum(char_lengths)
                        weights: list[tuple[int, float]] = []
                        acc = 0.0
                        for g_idx, g_sent in enumerate(group):
                            if g_idx == len(group) - 1:
                                w = round(max(0.0001, 1.0 - acc), 6)
                            else:
                                w = round(char_lengths[g_idx] / sum_chars, 6)
                                acc += w
                            weights.append((g_sent.index, w))

                        last_in_group = group[-1]
                        is_para_end = (j >= total) or (valid_sentences[j].paragraph != last_in_group.paragraph)
                        is_diag_trans = (
                            (j < total)
                            and (valid_sentences[j].paragraph == last_in_group.paragraph)
                            and (valid_sentences[j].is_dialogue != last_in_group.is_dialogue)
                        )

                        chunks.append(
                            Chunk(
                                id=f"c-{chapter_short}-{chunk_counter:05d}",
                                text=combined_text,
                                sentence_weights=weights,
                                paragraph=curr.paragraph,
                                is_paragraph_end=is_para_end,
                                is_heading=False,
                                is_dialogue_transition=is_diag_trans,
                                is_subchunk=False,
                            )
                        )
                        chunk_counter += 1
                        i = j
                        continue

            # Kural 1: Varsayılan 1 cümle = 1 parça
            is_para_end = (i + 1 >= total) or (valid_sentences[i + 1].paragraph != curr.paragraph)
            is_diag_trans = (
                (i + 1 < total)
                and (valid_sentences[i + 1].paragraph == curr.paragraph)
                and (valid_sentences[i + 1].is_dialogue != curr.is_dialogue)
            )
            chunks.append(
                Chunk(
                    id=f"c-{chapter_short}-{chunk_counter:05d}",
                    text=spoken,
                    sentence_weights=[(curr.index, 1.0)],
                    paragraph=curr.paragraph,
                    is_paragraph_end=is_para_end,
                    is_heading=False,
                    is_dialogue_transition=is_diag_trans,
                    is_subchunk=False,
                )
            )
            chunk_counter += 1
            i += 1

        return chunks

    def _split_long_text(self, text: str, max_chars: int) -> list[str]:
        """Uzun bir cümleyi önce (; :), sonra (,), sonra boşluk sınırından max_chars altı parçalara böler."""
        remaining = text.strip()
        parts: list[str] = []

        while len(remaining) > max_chars:
            window = remaining[: max_chars + 1]
            split_pos = -1

            # 1) Noktalı virgül veya iki nokta
            for m in re.finditer(r"[;:]\s+", window):
                if m.end() >= max_chars // 3:
                    split_pos = m.end()

            # 2) Virgül
            if split_pos == -1:
                for m in re.finditer(r",\s+", window):
                    if m.end() >= max_chars // 3:
                        split_pos = m.end()

            # 3) Boşluk
            if split_pos == -1:
                last_space = window.rfind(" ")
                if last_space > 0:
                    split_pos = last_space + 1

            # 4) Hiç boşluk yoksa sert kesim
            if split_pos <= 0:
                split_pos = max_chars

            piece = remaining[:split_pos].strip()
            if piece:
                if piece[-1] not in ".!?…,:;":
                    piece = f"{piece},"
                parts.append(piece)
            remaining = remaining[split_pos:].strip()

        if remaining:
            parts.append(remaining)

        return parts
