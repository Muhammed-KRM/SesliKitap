from __future__ import annotations

import socket
from pathlib import Path
from typing import Any

import httpx
import pytest
import respx

from discovery.file_validator import FileValidator
from discovery.safe_downloader import SafeDownloader
from models.events import DosyaFormati, ProcessingError


@pytest.mark.parametrize(
    "bad_url",
    [
        "ftp://archive.org/download/book/file.pdf",
        "http://localhost:5201/api/health",
        "http://minio:9000/seslikitap/book.pdf",
        "http://127.0.0.1:8080/secret",
        "http://169.254.169.254/latest/meta-data/",
        "http://10.0.0.12/internal.pdf",
        "http://192.168.1.10/router.pdf",
    ],
)
def test_safe_downloader_blocks_ssrf_urls(bad_url: str, monkeypatch: pytest.MonkeyPatch) -> None:
    def fake_getaddrinfo(host: str, port: int, *args: Any, **kwargs: Any) -> list[Any]:
        return [(socket.AF_INET, socket.SOCK_STREAM, 6, "", (host, port))]

    monkeypatch.setattr(socket, "getaddrinfo", fake_getaddrinfo)
    with pytest.raises(ProcessingError) as exc_info:
        SafeDownloader.assert_safe_url(bad_url)
    assert exc_info.value.code == "SSRF_BLOCKED"


def test_safe_downloader_blocks_redirect_to_private_ip(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def fake_getaddrinfo(host: str, port: int, *args: Any, **kwargs: Any) -> list[Any]:
        ip = "93.184.216.34" if host == "archive.org" else "127.0.0.1"
        return [(socket.AF_INET, socket.SOCK_STREAM, 6, "", (ip, port))]

    monkeypatch.setattr(socket, "getaddrinfo", fake_getaddrinfo)

    with respx.mock(assert_all_called=True) as router:
        router.get("https://archive.org/download/item/book.pdf").mock(
            return_value=httpx.Response(302, headers={"Location": "http://127.0.0.1:9010/secret.pdf"})
        )
        downloader = SafeDownloader()
        with pytest.raises(ProcessingError) as exc_info:
            downloader.download("https://archive.org/download/item/book.pdf", tmp_path / "out.pdf", "pdf")
        assert exc_info.value.code == "SSRF_BLOCKED"


def test_safe_downloader_validates_magic_bytes_and_size(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def fake_getaddrinfo(host: str, port: int, *args: Any, **kwargs: Any) -> list[Any]:
        return [(socket.AF_INET, socket.SOCK_STREAM, 6, "", ("93.184.216.34", port))]

    monkeypatch.setattr(socket, "getaddrinfo", fake_getaddrinfo)

    valid_pdf = b"%PDF-1.7\n1 0 obj\n<<>>\nendobj\n" + b"A" * 512
    with respx.mock(assert_all_called=True) as router:
        router.get("https://archive.org/download/item/valid.pdf").mock(
            return_value=httpx.Response(200, content=valid_pdf)
        )
        downloader = SafeDownloader()
        fmt, size, digest = downloader.download(
            "https://archive.org/download/item/valid.pdf",
            tmp_path / "valid.pdf",
            "pdf",
        )
        assert fmt == DosyaFormati.PDF
        assert size == len(valid_pdf)
        assert len(digest) == 64

    # Yanlış magic byte -> DOWNLOAD_INVALID_FILE
    with pytest.raises(ProcessingError) as exc_info:
        FileValidator.validate_prefix(b"<html>Not a PDF</html>", "pdf")
    assert exc_info.value.code == "DOWNLOAD_INVALID_FILE"
