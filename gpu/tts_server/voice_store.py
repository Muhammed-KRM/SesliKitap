"""SHA-256 adresli referans ses önbelleği (disk + LRU + atomik yazım)."""

from __future__ import annotations

import hashlib
import json
import os
import re
import threading
import time
from dataclasses import dataclass
from pathlib import Path

from tts_server import audio_codec

_HASH_RE = re.compile(r"^[a-f0-9]{64}$")


class VoiceHashMismatchError(ValueError):
    """Gönderilen ses baytlarının SHA-256 özeti URL'deki voice_hash ile eşleşmedi."""


class InvalidVoiceAudioError(ValueError):
    """Referans ses çok kısa (<3 sn), çok uzun (>60 sn) veya bozuk."""


@dataclass(slots=True)
class VoiceEntry:
    voice_hash: str
    wav_path: Path
    prompt_text: str | None
    duration_sec: float
    sample_rate: int


class VoiceStore:
    """/cache/voices altında SHA-256 adresli referans sesleri atomik ve LRU olarak yönetir."""

    def __init__(self, voices_dir: Path, max_items: int = 50) -> None:
        self._dir = Path(voices_dir)
        self._dir.mkdir(parents=True, exist_ok=True)
        self._max_items = max(1, max_items)
        self._lock = threading.Lock()

    @staticmethod
    def normalize_hash(voice_hash: str) -> str:
        normalized = voice_hash.strip().lower()
        if not _HASH_RE.match(normalized):
            raise VoiceHashMismatchError("Geçersiz SHA-256 voice_hash formatı.")
        return normalized

    def _wav_path(self, voice_hash: str) -> Path:
        return self._dir / f"{voice_hash}.wav"

    def _meta_path(self, voice_hash: str) -> Path:
        return self._dir / f"{voice_hash}.json"

    def exists(self, voice_hash: str) -> bool:
        h = self.normalize_hash(voice_hash)
        return self._wav_path(h).is_file() and self._meta_path(h).is_file()

    def count(self) -> int:
        return sum(1 for p in self._dir.glob("*.wav") if p.with_suffix(".json").is_file())

    def get(self, voice_hash: str) -> VoiceEntry | None:
        h = self.normalize_hash(voice_hash)
        wav_path = self._wav_path(h)
        meta_path = self._meta_path(h)
        if not (wav_path.is_file() and meta_path.is_file()):
            return None

        now = time.time()
        try:
            os.utime(wav_path, (now, now))
            os.utime(meta_path, (now, now))
            meta = json.loads(meta_path.read_text(encoding="utf-8"))
        except OSError:
            return None

        return VoiceEntry(
            voice_hash=h,
            wav_path=wav_path,
            prompt_text=meta.get("prompt_text"),
            duration_sec=float(meta.get("duration_sec", 0.0)),
            sample_rate=int(meta.get("sample_rate", 24000)),
        )

    def put(
        self,
        voice_hash: str,
        raw_audio_bytes: bytes,
        prompt_text: str | None = None,
    ) -> VoiceEntry:
        """Referans sesi doğrular, atomik olarak diske yazar ve LRU sınırı uygular."""
        h = self.normalize_hash(voice_hash)
        actual_hash = hashlib.sha256(raw_audio_bytes).hexdigest().lower()
        if actual_hash != h:
            raise VoiceHashMismatchError(
                f"SHA-256 uyuşmazlığı: beklenen={h}, hesaplanan={actual_hash}"
            )

        try:
            samples, sr = audio_codec.decode_audio_bytes(raw_audio_bytes)
        except Exception as exc:
            raise InvalidVoiceAudioError(f"Ses dosyası çözümlenemedi: {exc}") from exc

        duration_sec = float(len(samples)) / float(sr) if sr > 0 else 0.0
        if duration_sec < 3.0 or duration_sec > 60.0:
            raise InvalidVoiceAudioError(
                f"Referans ses süresi 3–60 sn aralığında olmalıdır (ölçülen: {duration_sec:.2f} sn)."
            )

        with self._lock:
            wav_path = self._wav_path(h)
            meta_path = self._meta_path(h)
            tmp_wav = self._dir / f".{h}.{os.getpid()}.wav.tmp"
            tmp_meta = self._dir / f".{h}.{os.getpid()}.json.tmp"

            tmp_wav.write_bytes(raw_audio_bytes)
            meta_payload = {
                "voice_hash": h,
                "prompt_text": prompt_text,
                "duration_sec": round(duration_sec, 3),
                "sample_rate": sr,
                "updated_at": time.time(),
            }
            tmp_meta.write_text(
                json.dumps(meta_payload, ensure_ascii=False),
                encoding="utf-8",
            )

            os.replace(tmp_wav, wav_path)
            os.replace(tmp_meta, meta_path)
            self._evict_if_needed(keep_hash=h)

            return VoiceEntry(
                voice_hash=h,
                wav_path=wav_path,
                prompt_text=prompt_text,
                duration_sec=round(duration_sec, 3),
                sample_rate=sr,
            )

    def _evict_if_needed(self, keep_hash: str) -> None:
        wav_files = [
            p for p in self._dir.glob("*.wav") if p.with_suffix(".json").is_file()
        ]
        if len(wav_files) <= self._max_items:
            return

        wav_files.sort(key=lambda p: p.stat().st_mtime)
        excess = len(wav_files) - self._max_items
        removed = 0
        for wav_file in wav_files:
            if removed >= excess:
                break
            stem = wav_file.stem.lower()
            if stem == keep_hash:
                continue
            try:
                wav_file.unlink(missing_ok=True)
                wav_file.with_suffix(".json").unlink(missing_ok=True)
                removed += 1
            except OSError:
                continue
