from __future__ import annotations

import ipaddress
import socket
from pathlib import Path
from urllib.parse import urljoin, urlparse

import httpx

from discovery.file_validator import FileValidator
from models.events import DosyaFormati, ProcessingError
from utils.hashing import sha256_file
from utils.logger import get_logger

logger = get_logger(__name__)

MAX_REDIRECTS = 5
DEFAULT_MAX_BYTES = 300 * 1024 * 1024  # 300 MB


class SafeDownloader:
    """SSRF korumalı, akışlı (streaming) ve dosya imzası doğrulamalı HTTP/HTTPS indirici."""

    def __init__(
        self,
        max_bytes: int = DEFAULT_MAX_BYTES,
        timeout_sec: float = 60.0,
        client: httpx.Client | None = None,
    ) -> None:
        self.max_bytes = max_bytes
        self.timeout_sec = timeout_sec
        self._external_client = client

    @staticmethod
    def assert_safe_url(url: str) -> None:
        """URL şemasını ve çözümlenen tüm DNS IP adreslerini SSRF'e karşı doğrular."""
        parsed = urlparse(url)
        if parsed.scheme not in ("http", "https"):
            raise ProcessingError(
                "SSRF_BLOCKED",
                f"Güvenli olmayan URL şeması: {parsed.scheme}",
                recoverable=False,
            )

        hostname = parsed.hostname
        if not hostname:
            raise ProcessingError("SSRF_BLOCKED", "URL içinde geçerli bir sunucu adı yok.", recoverable=False)

        host_lower = hostname.lower()
        if host_lower in {"localhost", "minio", "postgres", "redis", "rabbitmq", "searxng", "api"} or host_lower.endswith(
            (".local", ".internal")
        ):
            raise ProcessingError(
                "SSRF_BLOCKED",
                f"Güvenli olmayan yerel sunucu adresi engellendi: {hostname}",
                recoverable=False,
            )

        try:
            addr_info = socket.getaddrinfo(hostname, parsed.port or (443 if parsed.scheme == "https" else 80))
        except socket.gaierror as exc:
            raise ProcessingError(
                "DOWNLOAD_DNS_ERROR",
                f"Sunucu adresi çözümlenemedi ({hostname}): {exc}",
                recoverable=True,
            ) from exc

        for entry in addr_info:
            ip_str = entry[4][0]
            ip_obj = ipaddress.ip_address(ip_str)
            if (
                ip_obj.is_private
                or ip_obj.is_loopback
                or ip_obj.is_link_local
                or ip_obj.is_reserved
                or ip_obj.is_multicast
                or ip_obj.is_unspecified
            ):
                raise ProcessingError(
                    "SSRF_BLOCKED",
                    f"Güvenli olmayan iç ağ IP adresi engellendi: {ip_str}",
                    recoverable=False,
                )

    def download(
        self,
        url: str,
        dest_path: Path,
        expected_format: str,
    ) -> tuple[DosyaFormati, int, str]:
        """Dosyayı SSRF ve yönlendirme kontrolüyle indirir, doğrular ve (format, boyut, sha256) döner."""
        dest_path.parent.mkdir(parents=True, exist_ok=True)
        current_url = url

        client = self._external_client or httpx.Client(
            timeout=httpx.Timeout(self.timeout_sec, connect=15.0),
            follow_redirects=False,
            headers={"User-Agent": "SesliKitap/1.0 (kisisel kullanim)"},
        )

        try:
            for _redirect_idx in range(MAX_REDIRECTS + 1):
                self.assert_safe_url(current_url)

                with client.stream("GET", current_url) as response:
                    if response.status_code in (301, 302, 303, 307, 308):
                        location = response.headers.get("Location")
                        if not location:
                            raise ProcessingError(
                                "DOWNLOAD_REDIRECT_ERR",
                                "Yönlendirme yanıtında Location başlığı eksik.",
                                recoverable=False,
                            )
                        current_url = urljoin(current_url, location)
                        continue

                    if response.status_code >= 400:
                        raise ProcessingError(
                            "DOWNLOAD_HTTP_ERR",
                            f"Kaynak dosya indirilemedi (HTTP {response.status_code}).",
                            recoverable=response.status_code >= 500,
                        )

                    content_length_raw = response.headers.get("Content-Length")
                    if (
                        content_length_raw
                        and content_length_raw.isdigit()
                        and int(content_length_raw) > self.max_bytes
                    ):
                        raise ProcessingError(
                            "DOWNLOAD_TOO_LARGE",
                            f"İndirilecek dosya boyutu sınırı ({self.max_bytes // (1024 * 1024)} MB) aşıyor.",
                            recoverable=False,
                        )

                    total_bytes = 0
                    header_buf = bytearray()
                    validated_format: DosyaFormati | None = None

                    with dest_path.open("wb") as out_file:
                        for chunk in response.iter_bytes(chunk_size=65536):
                            if not chunk:
                                continue

                            total_bytes += len(chunk)
                            if total_bytes > self.max_bytes:
                                raise ProcessingError(
                                    "DOWNLOAD_TOO_LARGE",
                                    "İndirme sırasında maksimum dosya boyutu sınırı aşıldı.",
                                    recoverable=False,
                                )

                            if validated_format is None:
                                header_buf.extend(chunk)
                                if len(header_buf) >= 2048:
                                    validated_format = FileValidator.validate_prefix(
                                        bytes(header_buf[:2048]),
                                        expected_format,
                                    )

                            out_file.write(chunk)

                    if validated_format is None:
                        validated_format = FileValidator.validate_prefix(bytes(header_buf), expected_format)

                    file_hash = sha256_file(dest_path)
                    return validated_format, total_bytes, file_hash

            raise ProcessingError(
                "DOWNLOAD_TOO_MANY_REDIRECTS",
                f"Maksimum yönlendirme sayısı ({MAX_REDIRECTS}) aşıldı.",
                recoverable=False,
            )
        finally:
            if self._external_client is None:
                client.close()
