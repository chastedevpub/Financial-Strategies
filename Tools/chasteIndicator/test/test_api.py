from fastapi.testclient import TestClient
from main import app

client = TestClient(app)


def test_run_pipeline_endpoint_ok():
    payload = {
        "symbol": "ES",
        "interval": "1d",
        "start": "2025-11-01",
        "end": "2025-12-10",
        "indicator_config": {
            "sma_lengths": [20],
            "rsi_length": 14,
            "roc_length": 10,
            "adx_length": 14,
            "adx_signal_length": 14,
            "obv_enabled": True,
            "atr_length": 14,
            "bb_length": 20,
            "bb_std": 2.0,
            "kc_length": 20,
            "kc_atr_length": 10,
            "kc_multiplier": 2.0,
        },
        "anomaly_config": {"spike_threshold": 0.05},
    }

    resp = client.post("/api/run_pipeline", json=payload)
    print("STATUS:", resp.status_code)
    print("BODY:", resp.json())
    assert resp.status_code == 200

    data = resp.json()
    assert data["symbol"] == "ES"
    assert data["interval"] == "1d"
    assert "bars" in data
    assert len(data["bars"]) > 0

    bar0 = data["bars"][0]

    # core fields
    for field in ["datetime", "open", "high", "low", "close", "volume"]:
        assert field in bar0

    # anomaly fields
    assert "return" in bar0
    assert "is_spike" in bar0
    assert "is_gap" in bar0
    assert "is_volume_spike" in bar0

    # indicator fields are top-level, not nested
    for field in [
        "sma_20",
        "roc_10",
        "rsi_14",
        "adx_14",
        "adxr_14_2",
        "dmp_14",
        "dmn_14",
        "obv",
        "atr_14",
        "bbl_20_2.0_2.0",
        "bbm_20_2.0_2.0",
        "bbu_20_2.0_2.0",
        "bbb_20_2.0_2.0",
        "bbp_20_2.0_2.0",
        "kcle_20_2.0",
        "kcbe_20_2.0",
        "kcue_20_2.0",
    ]:
        assert field in bar0
