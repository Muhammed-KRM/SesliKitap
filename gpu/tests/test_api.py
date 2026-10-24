"""tts_server.api uç noktalarının birim testleri."""

from __future__ import annotations

from fastapi.testclient import TestClient
from tests.conftest import make_test_wav_bytes
from tts_server import audio_codec
from tts_server.engines.mock_engine import MockServerEngine


def test_health_endpoint_does_not_require_auth(client: TestClient) -> None:
    for path in ("/health", "/v1/health"):
        resp = client.get(path)
        assert resp.status_code == 200
        body = resp.json()
        assert body["status"] == "ok"
        assert body["engine"] == "mock"
        assert body["modelLoaded"] is True


def test_health_returns_503_when_model_not_loaded(
    client: TestClient, mock_engine: MockServerEngine
) -> None:
    mock_engine.is_loaded = False
    resp = client.get("/v1/health")
    assert resp.status_code == 503
    assert resp.json()["modelLoaded"] is False


def test_info_requires_valid_bearer_token(
    client: TestClient, auth_headers: dict[str, str]
) -> None:
    unauth = client.get("/v1/info")
    assert unauth.status_code == 401
    assert unauth.json()["error"]["code"] == "unauthorized"

    wrong = client.get("/v1/info", headers={"Authorization": "Bearer wrong-token"})
    assert wrong.status_code == 401

    ok = client.get("/v1/info", headers=auth_headers)
    assert ok.status_code == 200
    data = ok.json()
    assert data["engine"] == "mock"
    assert data["sampleRate"] == 24000
    assert data["maxBatchItems"] == 4


def test_voice_registration_and_head_and_synthesize_flow(
    client: TestClient, auth_headers: dict[str, str]
) -> None:
    raw, b64, sha = make_test_wav_bytes(duration_sec=6.0)

    # 1. Başlangıçta HEAD -> 404
    head_miss = client.head(f"/v1/voices/{sha}", headers=auth_headers)
    assert head_miss.status_code == 404

    # 2. Ses önbellekte yokken synthesize -> 404 voice_not_cached
    synth_miss = client.post(
        "/v1/synthesize",
        headers=auth_headers,
        json={
            "requestId": "req-1",
            "voiceHash": sha,
            "language": "tr",
            "outputFormat": "flac",
            "chunks": [{"id": "c-1", "text": "Merhaba dünya."}],
        },
    )
    assert synth_miss.status_code == 404
    assert synth_miss.json()["error"]["code"] == "voice_not_cached"
    assert synth_miss.json()["error"]["retryable"] is True

    # 3. Yanlış hash ile PUT -> 400 voice_hash_mismatch
    bad_hash = "0" * 64
    put_bad = client.put(
        f"/v1/voices/{bad_hash}",
        headers=auth_headers,
        json={"audioB64": b64, "promptText": "Deneme metni", "sampleRate": 24000},
    )
    assert put_bad.status_code == 400
    assert put_bad.json()["error"]["code"] == "voice_hash_mismatch"

    # 4. Doğru hash ile PUT (multipart/form-data ve JSON desteklenir) -> 201 Created
    put_ok = client.put(
        f"/v1/voices/{sha}",
        headers=auth_headers,
        files={"audio": ("reference.wav", raw, "audio/wav")},
        data={"promptText": "Deneme metni"},
    )
    assert put_ok.status_code == 201
    assert put_ok.json()["voiceHash"] == sha
    assert put_ok.json()["cached"] is True

    # 5. HEAD -> 200 OK
    head_hit = client.head(f"/v1/voices/{sha}", headers=auth_headers)
    assert head_hit.status_code == 200

    # 6. POST /v1/synthesize -> 200 OK ve geçerli FLAC çıktısı
    synth_ok = client.post(
        "/v1/synthesize",
        headers=auth_headers,
        json={
            "requestId": "req-2",
            "engine": "mock",
            "voiceHash": sha,
            "language": "tr",
            "output": {"codec": "flac", "sampleRate": 24000},
            "chunks": [
                {"id": "c-001", "text": "Birinci cümle başarıyla okundu.", "seed": 42},
                {"id": "c-002", "text": "İkinci cümle de aynı sesle seslendirildi.", "seed": 42},
            ],
        },
    )
    assert synth_ok.status_code == 200
    payload = synth_ok.json()
    assert payload["requestId"] == "req-2"
    assert payload["gpuMs"] >= 1
    assert len(payload["results"]) == 2

    first = payload["results"][0]
    assert first["id"] == "c-001"
    assert first["ok"] is True
    assert first["durationMs"] > 500
    assert first["rmsDbfs"] > -45.0
    samples, sr = audio_codec.decode_audio_bytes(
        audio_codec.decode_b64_bytes(first["audioB64"])
    )
    assert sr == 24000
    assert len(samples) > 10000


def test_synthesize_enforces_batch_text_and_engine_limits(
    client: TestClient, auth_headers: dict[str, str]
) -> None:
    _raw, b64, sha = make_test_wav_bytes(duration_sec=5.5)
    client.put(f"/v1/voices/{sha}", headers=auth_headers, json={"audioB64": b64})

    # Batch sınırı (max=4) aşımı -> 422 batch_too_large
    too_many = [{"id": f"c-{i}", "text": "Kısa metin."} for i in range(5)]
    resp_batch = client.post(
        "/v1/synthesize",
        headers=auth_headers,
        json={"requestId": "req-b", "voiceHash": sha, "chunks": too_many},
    )
    assert resp_batch.status_code == 422
    assert resp_batch.json()["error"]["code"] == "batch_too_large"

    # Metin uzunluğu (max=120) aşımı -> 422 text_too_long
    long_text = "Kelime " * 30
    resp_text = client.post(
        "/v1/synthesize",
        headers=auth_headers,
        json={
            "requestId": "req-t",
            "voiceHash": sha,
            "chunks": [{"id": "c-long", "text": long_text}],
        },
    )
    assert resp_text.status_code == 422
    assert resp_text.json()["error"]["code"] == "text_too_long"

    # Motor adı uyuşmazlığı -> 422 engine_mismatch
    resp_eng = client.post(
        "/v1/synthesize",
        headers=auth_headers,
        json={
            "requestId": "req-e",
            "engine": "voxcpm2",
            "voiceHash": sha,
            "chunks": [{"id": "c-1", "text": "Deneme"}],
        },
    )
    assert resp_eng.status_code == 422
    assert resp_eng.json()["error"]["code"] == "engine_mismatch"
