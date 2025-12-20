from chasteIndicaCalc.pipeline import run_pipeline
import pandas as pd


def test_run_pipeline_basic():
    df = run_pipeline(
        symbol="ES",
        interval="1d",
        start="2025-11-01",
        end="2025-12-10",
        indicator_config={},
        anomaly_config={"spike_threshold": 0.05},
    )

    assert not df.empty
    assert isinstance(df.index, pd.DatetimeIndex)  # if you import pandas as pd

    for col in ["open", "high", "low", "close", "volume"]:
        assert col in df.columns

    assert "return" in df.columns
    assert "is_spike" in df.columns
    assert "is_gap" in df.columns
    assert "is_volume_spike" in df.columns
