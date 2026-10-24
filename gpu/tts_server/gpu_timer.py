"""GPU süre ölçümü ve VRAM sorgulama yardımcıları (CUDA yoksa CPU zamanlayıcısına düşer)."""

from __future__ import annotations

import time
from collections.abc import Iterator
from contextlib import contextmanager


class GpuStopwatch:
    """Milisaniye cinsinden geçen hesaplama süresini tutar."""

    def __init__(self) -> None:
        self.elapsed_ms: int = 0


@contextmanager
def measure_gpu_ms() -> Iterator[GpuStopwatch]:
    """CUDA mevcutsa torch.cuda.Event ile, değilse perf_counter ile süreyi ms olarak ölçer."""
    watch = GpuStopwatch()
    start_wall = time.perf_counter()
    cuda_events = None
    try:
        import torch

        if torch.cuda.is_available():
            start_ev = torch.cuda.Event(enable_timing=True)
            end_ev = torch.cuda.Event(enable_timing=True)
            start_ev.record()
            cuda_events = (torch, start_ev, end_ev)
    except (ImportError, RuntimeError):
        cuda_events = None

    try:
        yield watch
    finally:
        measured = False
        if cuda_events is not None:
            torch_mod, start_ev, end_ev = cuda_events
            try:
                end_ev.record()
                torch_mod.cuda.synchronize()
                watch.elapsed_ms = max(1, round(start_ev.elapsed_time(end_ev)))
                measured = True
            except RuntimeError:
                measured = False
        if not measured:
            watch.elapsed_ms = max(
                1, round((time.perf_counter() - start_wall) * 1000.0)
            )


def query_gpu_status() -> tuple[str, int]:
    """(gpu_adi, bos_vram_mb) döndürür. CUDA yoksa ('CPU / Mock', 0) döner."""
    try:
        import torch

        if torch.cuda.is_available():
            name = torch.cuda.get_device_name(0)
            free_bytes, _total_bytes = torch.cuda.mem_get_info(0)
            return str(name), int(free_bytes // (1024 * 1024))
    except (ImportError, RuntimeError):
        return "CPU (Mock / No-CUDA)", 0
    return "CPU (Mock / No-CUDA)", 0
