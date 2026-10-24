"""VoiceStore birim testleri (SHA-256 doğrulama, süre kontrolü ve LRU tahliyesi)."""

from __future__ import annotations

import time

import pytest
from tests.conftest import make_test_wav_bytes
from tts_server.voice_store import (
    InvalidVoiceAudioError,
    VoiceHashMismatchError,
    VoiceStore,
)


def test_voice_store_rejects_short_or_corrupt_audio(voice_store: VoiceStore) -> None:
    raw_short, _b64, sha_short = make_test_wav_bytes(duration_sec=1.5)
    with pytest.raises(InvalidVoiceAudioError):
        voice_store.put(sha_short, raw_short)

    with pytest.raises(VoiceHashMismatchError):
        voice_store.put("a" * 64, raw_short)


def test_voice_store_evicts_least_recently_used(voice_store: VoiceStore) -> None:
    # max_items = 3 (conftest.py içinde)
    entries = []
    for i in range(4):
        raw, _b64, sha = make_test_wav_bytes(duration_sec=5.0 + i * 0.25)
        entry = voice_store.put(sha, raw, prompt_text=f"Ses {i}")
        entries.append(entry)
        time.sleep(0.02)

    assert voice_store.count() == 3
    assert not voice_store.exists(entries[0].voice_hash)
    assert voice_store.exists(entries[1].voice_hash)
    assert voice_store.exists(entries[2].voice_hash)
    assert voice_store.exists(entries[3].voice_hash)
