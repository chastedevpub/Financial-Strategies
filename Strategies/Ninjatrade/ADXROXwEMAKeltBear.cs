using System;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    // Strategy: ADX‑ROC EMA Keltner Bear with custom decimal Keltner bands
    public class ADXROXwEMAKeltBear : Strategy
    {
        private double entryPrice;
        private double lowerKeltner;

        // Core indicators
        private EMA  ema;
        private ROC  roc;
        private ADX  adx;
        private ATR  atr;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "ADX‑ROC EMA Keltner Bear Strategy (custom decimal Keltner bands)";
                Name        = "ADXROXwEMAKeltBear";
                Calculate   = Calculate.OnEachTick;
                IsOverlay   = true;
                IncludeCommission = true;

                // Indicator periods (editable in UI)
                AdxPeriod          = 14;
                RocPeriod          = 9;
                AtrPeriod          = 14;
                EmaPeriod          = 20;
                KeltnerPeriod      = 20;
                KeltnerMultiplier  = 1.5;   // now truly decimal‑friendly

                // Thresholds
                AdxLowThreshold    = 20;
                AdxHighThreshold   = 40;
                RocEntryThreshold  = -0.02;
                AtrThreshold       = 1.0;

                // Plots for custom Keltner channels
                AddPlot(Brushes.DodgerBlue,  "UpperKC"); // index 0
                AddPlot(Brushes.DodgerBlue,  "LowerKC"); // index 1
            }
            else if (State == State.DataLoaded)
            {
                ema = EMA(EmaPeriod);
                roc = ROC(Close, RocPeriod);
                adx = ADX(AdxPeriod);
                atr = ATR(AtrPeriod);

                // Add main indicators to chart for visual reference
                AddChartIndicator(ema);
                AddChartIndicator(VOL());
                AddChartIndicator(roc);
                AddChartIndicator(atr);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(Math.Max(AdxPeriod, RocPeriod), KeltnerPeriod))
                return;

            double emaVal   = ema[0];
            double atrVal   = atr[0];
            double adxVal   = adx[0];
            double rocVal   = roc[0];

            // --- Custom decimal Keltner bands ---
            double offset      = KeltnerMultiplier * atrVal;
            double upperKC     = emaVal + offset;
            lowerKeltner       = emaVal - offset;

            // Push to plots
            Values[0][0] = upperKC;
            Values[1][0] = lowerKeltner;

            // -------- Entry --------
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                bool shortSetup = CrossBelow(Close, lowerKeltner, 1)
                                   && rocVal < RocEntryThreshold
                                   && atrVal > AtrThreshold
                                   && adxVal >= AdxLowThreshold && adxVal <= AdxHighThreshold;

                if (shortSetup)
                {
                    entryPrice = Close[0];
                    EnterShort("EnterShortSignal");
                    SetStopLoss(CalculationMode.Ticks, 16);
					//SetTrailStop("EnterShortSignal", CalculationMode.Ticks, 32, false); // 4‑point trailing stop
                }
            }

            // -------- Exit --------
            if (Position.MarketPosition == MarketPosition.Short)
            {
                if (Close[0] >= lowerKeltner)
                    ExitShort("ExitKeltner", "EnterShortSignal");
            }
        }

        #region User‑configurable properties
        [NinjaScriptProperty] public int    AdxPeriod         { get; set; }
        [NinjaScriptProperty] public int    RocPeriod         { get; set; }
        [NinjaScriptProperty] public int    AtrPeriod         { get; set; }
        [NinjaScriptProperty] public int    EmaPeriod         { get; set; }
        [NinjaScriptProperty] public int    KeltnerPeriod     { get; set; }
        [NinjaScriptProperty] public double KeltnerMultiplier { get; set; }
        [NinjaScriptProperty] public double AdxLowThreshold   { get; set; }
        [NinjaScriptProperty] public double AdxHighThreshold  { get; set; }
        [NinjaScriptProperty] public double RocEntryThreshold { get; set; }
        [NinjaScriptProperty] public double AtrThreshold      { get; set; }
        #endregion
    }
}
