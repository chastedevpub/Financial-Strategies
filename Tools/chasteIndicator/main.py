from fastapi import FastAPI
from pydantic import BaseModel
from typing import List
import pandas as pd
import numpy as np

from chasteIndicaCalc.pipeline import run_pipeline


app = FastAPI()


class IndicatorConfig(BaseModel):
    sma_lengths: List[int]
    rsi_length: int
    roc_length: int
    adx_length: int
    adx_signal_length: int
    obv_enabled: bool
    atr_length: int
    bb_length: int
    bb_std: float
    kc_length: int
    kc_atr_length: int
    kc_multiplier: float


class AnomalyConfig(BaseModel):
    spike_threshold: float


class RunPipelineRequest(BaseModel):
    symbol: str
    interval: str
    start: str
    end: str
    indicator_config: IndicatorConfig
    anomaly_config: AnomalyConfig


@app.post("/api/run_pipeline")
def api_run_pipeline(req: RunPipelineRequest):
    indicator_cfg = {
        "sma_lengths": req.indicator_config.sma_lengths,
        "rsi_length": req.indicator_config.rsi_length,
        "roc_length": req.indicator_config.roc_length,
        "adx_length": req.indicator_config.adx_length,
        "adx_signal_length": req.indicator_config.adx_signal_length,
        "obv_enabled": req.indicator_config.obv_enabled,
        "atr_length": req.indicator_config.atr_length,
        "bb_length": req.indicator_config.bb_length,
        "bb_std": req.indicator_config.bb_std,
        "kc_length": req.indicator_config.kc_length,
        "kc_atr_length": req.indicator_config.kc_atr_length,
        "kc_multiplier": req.indicator_config.kc_multiplier,
    }
    anomaly_cfg = {"spike_threshold": req.anomaly_config.spike_threshold}

    # Match pipeline test call order: symbol, interval, start, end, indicator_cfg, anomaly_cfg
    df = run_pipeline(
        req.symbol,
        req.interval,
        req.start,
        req.end,
        indicator_cfg,
        anomaly_cfg,
    )

    # Drop internal helper columns
    for col in ["prev_index", "delta_minutes"]:
        if col in df.columns:
            df = df.drop(columns=[col])

    # Ensure DatetimeIndex
    if not isinstance(df.index, pd.DatetimeIndex):
        df.index = pd.to_datetime(df.index)

    # Add datetime column for API output
    index_name = df.index.name or "index"
    df = df.reset_index().rename(columns={index_name: "datetime"})

    # Replace NaN with None so JSON is compliant[web:350][web:352]
    df = df.replace({np.nan: None})

    records = df.to_dict(orient="records")

    return {
        "symbol": req.symbol,
        "interval": req.interval,
        "start": req.start,
        "end": req.end,
        "bars": records,
        "meta": {"row_count": len(records)},
    }


@app.get("/health")
def health():
    return {"status": "ok"}
