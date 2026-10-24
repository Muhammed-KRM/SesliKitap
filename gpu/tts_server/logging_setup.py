"""Yapılandırılmış JSON/konsol loglama (Kural 6.2: metin veya ses baytları ASLA loglanmaz)."""

from __future__ import annotations

import logging
import sys


def setup_server_logging(level: str = "INFO") -> None:
    """TTS sunucusu için standart stdout loglamasını yapılandırır."""
    numeric_level = getattr(logging, level.upper(), logging.INFO)
    logging.basicConfig(
        level=numeric_level,
        format="%(asctime)s | %(levelname)-7s | %(name)s | %(message)s",
        datefmt="%Y-%m-%dT%H:%M:%S",
        stream=sys.stdout,
        force=True,
    )
    logging.getLogger("uvicorn.access").setLevel(logging.WARNING)
    logging.getLogger("httpx").setLevel(logging.WARNING)
