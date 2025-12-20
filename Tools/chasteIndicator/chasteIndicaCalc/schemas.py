# chasteIndicaCalc/schemas.py
from typing import Any, Dict, List, Literal
from pydantic import BaseModel, Field, ConfigDict

Interval = Literal["1m", "5m", "15m", "1h", "1d"]


class IndicatorConfig(BaseModel):
    sma_lengths: list[int] = [20, 50, 200]
    rsi_length: int = 14
    roc_length: int = 10
    adx_length: int = 14
    adx_signal_length: int = 14
    adx_threshold: float = 20.0
    obv_enabled: bool = True
    atr_length: int = 14
    bb_length: int = 20
    bb_std: float = 2.0
    kc_length: int = 20
    kc_atr_length: int = 10
    kc_multiplier: float = 2.0


class AnomalyConfigModel(BaseModel):
    spike_threshold: float = 0.05


class DataRequest(BaseModel):
    symbol: str
    interval: Interval
    start: str
    end: str
    indicator_config: IndicatorConfig | None = None
    anomaly_config: AnomalyConfigModel | None = None


class BarWithIndicators(BaseModel):
    # Allow any extra indicator fields; use alias mapping for "return"
    model_config = ConfigDict(extra="allow", populate_by_name=True)

    # Core OHLCV
    open: float
    high: float
    low: float
    close: float
    volume: float

    # Anomalies (explicit)
    return_: float | None = Field(default=None, alias="return")
    is_spike: bool | None = None
    is_gap: bool | None = None
    is_volume_spike: bool | None = None
    # NOTE: prev_index and delta_minutes are *not* declared here,
    # so they will either be dropped (if you remove them before serialization)
    # or treated as extra keys if they are simple JSON types.


class PipelineResponse(BaseModel):
    symbol: str
    interval: Interval
    start: str
    end: str
    bars: List[BarWithIndicators]
    meta: Dict[str, Any] = {}
