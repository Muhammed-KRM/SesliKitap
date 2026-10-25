"""TTS motor yedeklilik zinciri (OtoEdit STTEngineChain uyarlaması — Bölüm 4.15.6 & F-34)."""

from __future__ import annotations

from collections.abc import Callable

from config import Config, settings
from models.voice_models import (
    BatchResult,
    PreparedVoice,
    SynthChunkInput,
    VoiceParams,
)
from pipeline.tts.base_engine import (
    BaseTtsEngine,
    EngineUnavailableError,
    FamilyExhaustedError,
)
from pipeline.tts.mock_engine import MockTtsEngine
from pipeline.tts.remote_http_engine import RemoteHttpTtsEngine
from pipeline.tts.voice_sync import VoiceSync
from utils.logger import get_logger

logger = get_logger(__name__)


class TtsEngineChain:
    """TTS motorlarını öncelik sırasıyla dener; bölüm içi model ailesi tutarlılığını (F-34) korur."""

    def __init__(
        self,
        engines: dict[str, BaseTtsEngine],
        order: list[str],
        voice_sync: VoiceSync | None = None,
        on_fallback: Callable[[str, str, str], None] | None = None,
    ) -> None:
        self._engines = engines
        self._order = [name for name in order if name in engines]
        self._voice_sync = voice_sync or VoiceSync()
        self._on_fallback = on_fallback
        self._exhausted: set[str] = set()
        self._used: list[str] = []
        self._pinned_family: str | None = None

    @property
    def used_engines(self) -> list[str]:
        return list(self._used)

    @property
    def pinned_family(self) -> str | None:
        return self._pinned_family

    def candidate_engines(self, *, ignore_pin: bool = False) -> list[BaseTtsEngine]:
        result: list[BaseTtsEngine] = []
        for name in self._order:
            if name in self._exhausted:
                continue
            eng = self._engines[name]
            if not eng.is_available():
                continue
            if not ignore_pin and self._pinned_family is not None and eng.family != self._pinned_family:
                continue
            result.append(eng)
        return result

    def pin_for_chapter(self) -> str:
        """Bölüm başında kullanılabilir ilk motorun model ailesini sabitler."""
        candidates = self.candidate_engines(ignore_pin=True)
        if not candidates:
            raise EngineUnavailableError(
                "chain",
                "Yapılandırılmış ve kullanılabilir hiçbir TTS motoru bulunamadı.",
            )
        self._pinned_family = candidates[0].family
        return self._pinned_family

    def advance_to_next_family(self) -> str:
        """Sabitlenen aile tükendiğinde mevcut ailenin motorlarını devre dışı bırakıp sonraki aileye geçer."""
        if self._pinned_family is not None:
            for name, eng in self._engines.items():
                if eng.family == self._pinned_family:
                    self._exhausted.add(name)
        self._pinned_family = None
        return self.pin_for_chapter()

    def synthesize(
        self,
        chunks: list[SynthChunkInput],
        voice: PreparedVoice,
        params: VoiceParams,
        *,
        request_id: str = "req-chain",
        language: str = "tr",
    ) -> BatchResult:
        """Sabitlenen ailedeki motorları sırayla dener; aile tükenirse FamilyExhaustedError fırlatır."""
        candidates = self.candidate_engines(ignore_pin=False)
        if not candidates:
            if self._pinned_family is not None:
                raise FamilyExhaustedError(
                    self._pinned_family,
                    f"'{self._pinned_family}' model ailesindeki tüm motorlar tükendi.",
                )
            raise EngineUnavailableError("chain", "Kullanılabilir TTS motoru kalmadı.")

        last_err: Exception | None = None
        for idx, engine in enumerate(candidates):
            try:
                self._voice_sync.ensure(engine, voice)
                res = engine.synthesize(
                    chunks,
                    voice,
                    params,
                    request_id=request_id,
                    language=language,
                )
                if engine.engine_id not in self._used:
                    self._used.append(engine.engine_id)
                return res
            except EngineUnavailableError as exc:
                last_err = exc
                if exc.quota_exhausted:
                    self._exhausted.add(engine.engine_id)
                next_eng = candidates[idx + 1].engine_id if idx + 1 < len(candidates) else "YOK"
                warn_msg = f"{engine.engine_id} kullanılamadı ({exc}), alternatif motor: {next_eng}"
                logger.warning(warn_msg)
                if self._on_fallback is not None:
                    self._on_fallback(engine.engine_id, next_eng, str(exc))

        if self._pinned_family is not None:
            raise FamilyExhaustedError(
                self._pinned_family,
                f"'{self._pinned_family}' ailesindeki motorlar başarısız oldu: {last_err}",
            ) from last_err

        raise EngineUnavailableError("chain", f"Tüm TTS motorları başarısız oldu: {last_err}") from last_err


def build_default_engine_chain(
    cfg: Config | None = None,
    order_override: list[str] | None = None,
    on_fallback: Callable[[str, str, str], None] | None = None,
) -> TtsEngineChain:
    """Yapılandırmadan standart TtsEngineChain örneği oluşturur."""
    c = cfg or settings
    token = c.tts_api_token.get_secret_value() if c.tts_api_token else None

    engines: dict[str, BaseTtsEngine] = {
        "voxcpm2_modal": RemoteHttpTtsEngine(
            engine_id="voxcpm2_modal",
            family="voxcpm2",
            base_url=c.tts_modal_url,
            api_token=token,
            request_timeout_sec=c.tts_request_timeout_sec,
            cold_start_timeout_sec=c.tts_cold_start_timeout_sec,
            max_retries=c.tts_max_retries,
        ),
        "voxcpm2_alt": RemoteHttpTtsEngine(
            engine_id="voxcpm2_alt",
            family="voxcpm2",
            base_url=c.tts_alt_url,
            api_token=token,
            request_timeout_sec=c.tts_request_timeout_sec,
            cold_start_timeout_sec=c.tts_cold_start_timeout_sec,
            max_retries=c.tts_max_retries,
        ),
        "xtts2_modal": RemoteHttpTtsEngine(
            engine_id="xtts2_modal",
            family="xtts2",
            base_url=c.tts_xtts_url,
            api_token=token,
            request_timeout_sec=c.tts_request_timeout_sec,
            cold_start_timeout_sec=c.tts_cold_start_timeout_sec,
            max_retries=c.tts_max_retries,
        ),
        "mock": MockTtsEngine(allow_mock=c.allow_mock_tts, use_speech=True),
    }

    order = list(order_override) if order_override else list(c.tts_engine_order)
    return TtsEngineChain(engines=engines, order=order, on_fallback=on_fallback)
