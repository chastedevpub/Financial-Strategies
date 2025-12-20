# chasteIndicaCalc/data_client.py
from typing import Literal

import pandas as pd
import yfinance as yf

Interval = Literal["1m", "5m", "15m", "1h", "1d"]

SYMBOL_MAP = {
    "ES": "ES=F",
    "NQ": "NQ=F",
}


def get_ohlcv(symbol: str, interval: Interval, start: str, end: str) -> pd.DataFrame:
    yf_symbol = SYMBOL_MAP.get(symbol, symbol)
    df = yf.download(
        yf_symbol,
        interval=interval,
        start=start,
        end=end,
        auto_adjust=False,
        progress=False,
    )
    if df.empty:
        raise ValueError(f"No data for {symbol} ({yf_symbol})")

    # Just make sure the time index is a proper datetime index.
    df.index = pd.to_datetime(df.index)

    # Standardize OHLCV column names only.
    df = df.rename(
        columns={
            "Open": "open",
            "High": "high",
            "Low": "low",
            "Close": "close",
            "Adj Close": "adj_close",
            "Volume": "volume",
        }
    )

    # Do NOT create a datetime column here; leave the index as datetime.
    return df[["open", "high", "low", "close", "volume"]]
