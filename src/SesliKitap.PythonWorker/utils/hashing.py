from __future__ import annotations

import hashlib
from pathlib import Path


def sha256_file(path: Path, chunk_size: int = 1024 * 1024) -> str:
    """Dosyanın SHA-256 özetini akış halinde hesaplar."""
    digest = hashlib.sha256()
    with path.open("rb") as f:
        while chunk := f.read(chunk_size):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_bytes(data: bytes) -> str:
    """Bayt dizisinin SHA-256 özetini döner."""
    return hashlib.sha256(data).hexdigest()


def sha256_text(text: str) -> str:
    """UTF-8 metnin SHA-256 özetini döner."""
    return hashlib.sha256(text.encode("utf-8")).hexdigest()
