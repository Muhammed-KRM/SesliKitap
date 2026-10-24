"""Referans sesin uzak TTS sunucusunda önbellekte olmasını garanti eden yardımcı (Bölüm 4.15.6)."""

from __future__ import annotations

import threading

from models.voice_models import PreparedVoice
from pipeline.tts.base_engine import BaseTtsEngine
from utils.logger import get_logger

logger = get_logger(__name__)


class VoiceSync:
    """İş süresince (engine_id, voice_hash) çiftlerini önbellekler; yoksa HEAD + PUT çalıştırır."""

    def __init__(self) -> None:
        self._synced: set[tuple[str, str]] = set()
        self._lock = threading.Lock()

    def ensure(self, engine: BaseTtsEngine, voice: PreparedVoice) -> None:
        """Referans sesin hedef motorda kayıtlı olduğundan emin olur."""
        key = (engine.engine_id, voice.voice_hash.lower())
        with self._lock:
            if key in self._synced:
                return

        if not engine.has_voice(voice.voice_hash):
            logger.info(
                "Referans ses motora yükleniyor: engine=%s, hash=%s",
                engine.engine_id,
                voice.voice_hash[:12],
            )
            engine.register_voice(voice)

        with self._lock:
            self._synced.add(key)

    def invalidate(self, engine_id: str, voice_hash: str) -> None:
        with self._lock:
            self._synced.discard((engine_id, voice_hash.lower()))
