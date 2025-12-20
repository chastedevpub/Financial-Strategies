# trading_tool/indicators.py
from typing import Dict, Any

import pandas as pd
import pandas_ta as ta


def add_basic_indicators(
    df: pd.DataFrame,
    config: Dict[str, Any] | None = None,
) -> pd.DataFrame:
    """
    Adds SMAs, ROC, RSI, ADX (+DI/-DI), OBV, ATR,
    Bollinger Bands, and Keltner Channels to the input OHLCV DataFrame.

    Expects columns: datetime, open, high, low, close, volume.
    """
    cfg = config or {}

    close = df["close"]
    high = df["high"]
    low = df["low"]
    volume = df["volume"]

    # -------- SMAs (Simple Moving Averages) --------
    for length in cfg.get("sma_lengths", [20, 50, 200]):
        df[f"sma_{length}"] = ta.sma(close, length=length)

    # -------- ROC (Rate of Change) --------
    roc_len = cfg.get("roc_length", 10)
    df[f"roc_{roc_len}"] = ta.roc(close, length=roc_len)

    # -------- RSI (Relative Strength Index) --------
    rsi_len = cfg.get("rsi_length", 14)
    df[f"rsi_{rsi_len}"] = ta.rsi(close, length=rsi_len)

    # -------- ADX (+DI / -DI) --------
    adx_len = cfg.get("adx_length", 14)
    adx_sig_len = cfg.get("adx_signal_length", adx_len)
    adx_df = ta.adx(
        high=high,
        low=low,
        close=close,
        length=adx_len,
        lensig=adx_sig_len,
    )
    if adx_df is not None:
        for col in adx_df.columns:
            df[col.lower()] = adx_df[col]

    # -------- OBV (On-Balance Volume) --------
    if cfg.get("obv_enabled", True):
        df["obv"] = ta.obv(close=close, volume=volume)

    # -------- ATR (Average True Range) --------
    # this is the ATR you asked for; length is configurable
    atr_len = cfg.get("atr_length", 14)
    df[f"atr_{atr_len}"] = ta.atr(
        high=high,
        low=low,
        close=close,
        length=atr_len,
    )

    # -------- Bollinger Bands --------
    bb_len = cfg.get("bb_length", 20)
    bb_std = cfg.get("bb_std", 2.0)
    bb = ta.bbands(close=close, length=bb_len, std=bb_std)
    if bb is not None:
        for col in bb.columns:
            df[col.lower()] = bb[col]

    # -------- Keltner Channels --------
    kc_len = cfg.get("kc_length", 20)
    kc_atr_len = cfg.get("kc_atr_length", 10)
    kc_mult = cfg.get("kc_multiplier", 2.0)
    kc = ta.kc(
        high=high,
        low=low,
        close=close,
        length=kc_len,
        atr_length=kc_atr_len,
        scalar=kc_mult,
    )
    if kc is not None:
        for col in kc.columns:
            df[col.lower()] = kc[col]

    return df
