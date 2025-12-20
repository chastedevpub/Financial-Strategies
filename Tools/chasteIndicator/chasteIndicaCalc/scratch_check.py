# scratch_check.py
from chasteIndicaCalc.data_client import get_ohlcv

df = get_ohlcv("ES", "1m", "2025-01-01", "2025-01-10")
print(df.head())
print(df.columns)
