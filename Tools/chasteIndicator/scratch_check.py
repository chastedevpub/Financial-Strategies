# scratch_check.py
from chasteIndicaCalc.data_client import get_ohlcv

df = get_ohlcv("ES", "5m", "2025-12-01", "2025-12-02")
print(df.head())
print(df.columns)
