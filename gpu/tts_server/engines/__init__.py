"""TTS sunucusu motor soyutlamaları ve somut motorlar."""

from __future__ import annotations

from tts_server.engines.base import BaseTtsServerEngine
from tts_server.engines.mock_engine import MockServerEngine
from tts_server.engines.voxcpm_engine import VoxCpmEngine
from tts_server.engines.xtts_engine import XttsEngine

__all__ = [
    "BaseTtsServerEngine",
    "MockServerEngine",
    "VoxCpmEngine",
    "XttsEngine",
]
