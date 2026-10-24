"""Sabit zamanlı Bearer token kimlik doğrulama bağımlılığı."""

from __future__ import annotations

import hmac
from typing import Annotated

from fastapi import Depends, HTTPException, Request, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

from tts_server.config import TtsServerSettings

_bearer_scheme = HTTPBearer(auto_error=False)


def get_settings(request: Request) -> TtsServerSettings:
    """FastAPI app.state üzerinden TtsServerSettings döndürür."""
    settings: TtsServerSettings | None = getattr(request.app.state, "settings", None)
    if settings is None:
        settings = TtsServerSettings()
        request.app.state.settings = settings
    return settings


def require_bearer_token(
    credentials: Annotated[HTTPAuthorizationCredentials | None, Depends(_bearer_scheme)],
    settings: Annotated[TtsServerSettings, Depends(get_settings)],
) -> str:
    """Authorization: Bearer <token> başlığını sabit zamanlı karşılaştırma ile doğrular."""
    if credentials is None or credentials.scheme.lower() != "bearer":
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail={
                "code": "unauthorized",
                "message": "Geçerli bir Bearer token gereklidir.",
                "retryable": False,
            },
        )

    provided = credentials.credentials.encode("utf-8")
    expected = settings.tts_auth_token.encode("utf-8")
    if not expected or not hmac.compare_digest(provided, expected):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail={
                "code": "unauthorized",
                "message": "Geçersiz Bearer token.",
                "retryable": False,
            },
        )
    return credentials.credentials
