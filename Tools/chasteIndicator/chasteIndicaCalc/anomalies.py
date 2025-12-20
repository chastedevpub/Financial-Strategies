# chasteIndicaCalc/anomalies.py
from dataclasses import dataclass

import pandas as pd


@dataclass
class AnomalyConfig:
    spike_threshold: float = 0.05  # 5% bar-to-bar move


def add_anomaly_flags(
    df: pd.DataFrame,
    cfg: AnomalyConfig | None = None,
) -> pd.DataFrame:
    cfg = cfg or AnomalyConfig()
    df = df.copy()

    # Returns and spikes
    df["return"] = df["close"].pct_change()
    df["is_spike"] = df["return"].abs() > cfg.spike_threshold

    # Ensure datetime index
    if not isinstance(df.index, pd.DatetimeIndex):
        df.index = pd.to_datetime(df.index)

    # Use a Series for shifting instead of index.shift (no freq needed)
    idx_series = df.index.to_series()
    df["prev_index"] = idx_series.shift(1)
    df["delta_minutes"] = (
        idx_series - df["prev_index"]
    ).dt.total_seconds() / 60.0

    # Simple gap heuristic
    df["is_gap"] = df["delta_minutes"] > df["delta_minutes"].median() * 1.5

    # Volume anomalies
    vol = df["volume"]
    vol_ma = vol.rolling(20, min_periods=5).mean()
    df["is_volume_spike"] = (vol > vol_ma * 3) & vol_ma.notna()

    return df
