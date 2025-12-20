# chasteIndicaCalc/pipeline.py
from typing import Dict, Any

import pandas as pd

from .data_client import get_ohlcv, Interval
from .indicators import add_basic_indicators
from .anomalies import add_anomaly_flags, AnomalyConfig


def _flatten_columns(df: pd.DataFrame) -> pd.DataFrame:
    # If columns are MultiIndex like ('open', 'ES=F'), flatten to 'open' or 'open_es_f'
    if isinstance(df.columns, pd.MultiIndex):
        new_cols = []
        for col in df.columns:
            # col is a tuple; drop empty parts and lowercase
            parts = [str(c).strip() for c in col if str(c).strip()]
            if len(parts) == 0:
                new_cols.append("")
            else:
                # keep just the first part for OHLCV / indicators, e.g. 'open'
                new_cols.append(parts[0].lower())
        df = df.copy()
        df.columns = new_cols
    return df


def run_pipeline(
    symbol: str,
    interval: Interval,
    start: str,
    end: str,
    indicator_config: Dict[str, Any] | None = None,
    anomaly_config: Dict[str, Any] | None = None,
) -> pd.DataFrame:
    df = get_ohlcv(symbol, interval, start, end)

    # flatten any MultiIndex columns from yfinance
    df = _flatten_columns(df)

    df = add_basic_indicators(df, indicator_config)

    cfg = AnomalyConfig(**anomaly_config) if anomaly_config else None
    df = add_anomaly_flags(df, cfg)

    return df
