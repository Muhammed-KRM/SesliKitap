"""RemoteHttpTtsEngine ve TtsEngineChain birim testleri (respx ile sahte HTTP sunucu)."""

from __future__ import annotations

from pathlib import Path

import httpx
import pytest
import respx

from models.voice_models import PreparedVoice, SynthChunkInput, VoiceParams
from pipeline.tts.base_engine import EngineUnavailableError
from pipeline.tts.engine_chain import TtsEngineChain
from pipeline.tts.mock_engine import MockTtsEngine
from pipeline.tts.remote_http_engine import RemoteHttpTtsEngine
from tests.factories.audio_factory import make_flac_b64, write_sine_wav


@respx.mock
def test_remote_engine_retries_voice_registration_on_404_voice_not_cached(tmp_path: Path) -> None:
    base_url = "https://mock-modal-tts.local"
    wav_path = write_sine_wav(tmp_path / "ref.wav", duration_sec=6.0)
    flac_b64, _ = make_flac_b64(duration_sec=1.2)
    voice_hash = "a" * 64

    synth_calls = {"count": 0}

    def synth_handler(_request: httpx.Request) -> httpx.Response:
        synth_calls["count"] += 1
        if synth_calls["count"] == 1:
            return httpx.Response(
                404,
                json={"error": {"code": "voice_not_cached", "message": "Miss", "retryable": True}},
            )
        return httpx.Response(
            200,
            json={
                "requestId": "req-1",
                "engine": "voxcpm2",
                "modelRevision": "rev-1",
                "sampleRate": 24000,
                "gpuMs": 310,
                "results": [
                    {
                        "id": "c-1",
                        "ok": True,
                        "audioB64": flac_b64,
                        "durationMs": 1200,
                        "attempts": 1,
                    }
                ],
            },
        )

    respx.post(f"{base_url}/v1/synthesize").mock(side_effect=synth_handler)
    put_route = respx.put(f"{base_url}/v1/voices/{voice_hash}").mock(
        return_value=httpx.Response(201, json={"voiceHash": voice_hash, "cached": True, "durationSec": 6.0})
    )

    engine = RemoteHttpTtsEngine(
        engine_id="voxcpm2_modal",
        family="voxcpm2",
        base_url=base_url,
        api_token="valid-secret-token",
        max_retries=2,
    )
    voice = PreparedVoice(voice_id="v1", voice_hash=voice_hash, wav_path=wav_path)
    res = engine.synthesize([SynthChunkInput(id="c-1", text="Merhaba.")], voice, VoiceParams())

    assert put_route.called
    assert synth_calls["count"] == 2
    assert res.gpu_ms == 310
    assert len(res.outputs) == 1
    assert res.outputs[0].chunk_id == "c-1"


@respx.mock
def test_engine_chain_falls_back_within_same_family_on_429(tmp_path: Path) -> None:
    url_primary = "https://primary-modal.local"
    wav_path = write_sine_wav(tmp_path / "ref.wav", duration_sec=6.0)
    voice_hash = "b" * 64

    respx.head(f"{url_primary}/v1/voices/{voice_hash}").mock(return_value=httpx.Response(200))
    respx.post(f"{url_primary}/v1/synthesize").mock(
        return_value=httpx.Response(429, json={"error": {"code": "rate_limit", "message": "Busy"}})
    )

    primary = RemoteHttpTtsEngine(
        engine_id="voxcpm2_modal",
        family="voxcpm2",
        base_url=url_primary,
        api_token="token-1",
        max_retries=1,
    )
    alt = MockTtsEngine(allow_mock=True)
    alt.engine_id = "voxcpm2_alt"
    alt.family = "voxcpm2"

    fallbacks: list[tuple[str, str]] = []
    chain = TtsEngineChain(
        engines={"voxcpm2_modal": primary, "voxcpm2_alt": alt},
        order=["voxcpm2_modal", "voxcpm2_alt"],
        on_fallback=lambda f_from, f_to, _reason: fallbacks.append((f_from, f_to)),
    )
    chain.pin_for_chapter()

    voice = PreparedVoice(voice_id="v1", voice_hash=voice_hash, wav_path=wav_path)
    result = chain.synthesize([SynthChunkInput(id="c-1", text="Deneme cümlesi.")], voice, VoiceParams())

    assert result.engine_id == "voxcpm2_alt"
    assert fallbacks == [("voxcpm2_modal", "voxcpm2_alt")]
    assert chain.used_engines == ["voxcpm2_alt"]


def test_unconfigured_engine_reports_unavailable() -> None:
    eng = RemoteHttpTtsEngine(
        engine_id="voxcpm2_modal",
        family="voxcpm2",
        base_url="https://<workspace>--seslikitap.modal.run",
        api_token="DEGISTIR_BENI",
    )
    assert eng.is_available() is False
    with pytest.raises(EngineUnavailableError):
        eng.info()
