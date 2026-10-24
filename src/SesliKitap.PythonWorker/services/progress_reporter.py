"""Sentez ilerleme olaylarını kısıtlayarak (throttle) ve kelime ağırlıklı hesaplayarak yayınlar (Bölüm 4.15.6)."""

from __future__ import annotations

import time
from typing import Any

from models.synthesis_models import SynthesisInput
from utils.constants import Stage


class ProgressReporter:
    """Bölüm ve iş genel yüzdesini kelime ağırlıklı hesaplar ve en fazla throttle_sec aralıkla yayınlar."""

    def __init__(
        self,
        publisher: Any,
        job: SynthesisInput,
        throttle_sec: float = 2.0,
    ) -> None:
        self._publisher = publisher
        self._job = job
        self._throttle_sec = throttle_sec
        self._last_sent_time: float = 0.0
        self._last_sent_pct: int = -1

        weights: list[int] = []
        for ch in job.chapters:
            w = max(1, len((ch.temiz_metin or "").split()))
            weights.append(w)
        self._chapter_weights = weights
        self._total_weight = max(1, sum(weights))

    def compute_overall_percent(self, chapter_index: int, chapter_ratio: float) -> int:
        """Tamamlanan bölümlerin ve mevcut bölüm içi oranın (0..1) kelime ağırlıklı genel yüzdesini döner."""
        if not self._chapter_weights:
            return 100

        clamped_ratio = max(0.0, min(1.0, chapter_ratio))
        done_weight = sum(self._chapter_weights[:chapter_index])
        curr_weight = (
            self._chapter_weights[chapter_index] * clamped_ratio
            if 0 <= chapter_index < len(self._chapter_weights)
            else 0.0
        )
        pct = int(round(((done_weight + curr_weight) / self._total_weight) * 100.0))
        return max(0, min(100, pct))

    def report(
        self,
        *,
        chapter_index: int,
        chapter_id: str,
        chapter_ratio: float,
        message: str,
        stage: str = Stage.SENTEZ,
        force: bool = False,
    ) -> None:
        """Kısıtlama (throttle) kurallarına uyarsa PipelineStageChangedEvent yayınlar."""
        overall_pct = self.compute_overall_percent(chapter_index, chapter_ratio)
        chapter_pct = max(0, min(100, int(round(max(0.0, min(1.0, chapter_ratio)) * 100.0))))

        now = time.monotonic()
        elapsed = now - self._last_sent_time
        pct_diff = abs(overall_pct - self._last_sent_pct)

        if not force and self._last_sent_pct >= 0 and elapsed < self._throttle_sec and pct_diff < 1:
            return

        self._last_sent_time = now
        self._last_sent_pct = overall_pct

        self._publisher.publish_stage_changed(
            book_id=self._job.book.id,
            job_id=self._job.job_id,
            chapter_id=chapter_id,
            asama=stage,
            yuzde=overall_pct,
            mesaj=message,
            detay={
                "bolumYuzde": chapter_pct,
                "bolumSira": chapter_index + 1,
                "toplamBolum": len(self._job.chapters),
            },
        )
