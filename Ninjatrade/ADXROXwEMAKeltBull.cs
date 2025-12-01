using System;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    // Strategy: ADX‑ROC EMA Keltner Bull with custom decimal Keltner bands and configurable stop
    public class ADXROXwEMAKeltBull : Strategy
    {
        private double entryPrice;
        private double upperKeltner;

        // Core indicators
        private EMA ema;
        private ROC roc;
        private ADX adx;
        private ATR atr;

        #region User-configurable properties
        [NinjaScriptProperty]
        public int AdxPeriod { get; set; }
        [NinjaScriptProperty]
        public int RocPeriod { get; set; }
        [NinjaScriptProperty]
        public int AtrPeriod { get; set; }
        [NinjaScriptProperty]
        public int EmaPeriod { get; set; }
        [NinjaScriptProperty]
        public int KeltnerPeriod { get; set; }
        [NinjaScriptProperty]
        public double KeltnerMultiplier { get; set; }
        [NinjaScriptProperty]
        public double AdxLowThreshold { get; set; }
        [NinjaScriptProperty]
        public double AdxHighThreshold { get; set; }
        [NinjaScriptProperty]
        public double RocEntryThreshold { get; set; }
        [NinjaScriptProperty]
        public double AtrThreshold { get; set; }
        [NinjaScriptProperty]
        public int StopLossTicks { get; set; }
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "ADX‑ROC EMA Keltner Bull Strategy (custom decimal Keltner bands) with configurable stop";
                Name = "ADXROXwEMAKeltBull";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                IncludeCommission = true;

                // Default parameters to maximize trade frequency
                AdxPeriod = 5;
                RocPeriod = 2;
                AtrPeriod = 5;
                EmaPeriod = 10;
                KeltnerPeriod = 10;
                KeltnerMultiplier = 0.5;
                AdxLowThreshold = 10;
                AdxHighThreshold = 100;
                RocEntryThreshold = 0.0;
                AtrThreshold = 0.1;
                StopLossTicks = 4;

                AddPlot(Brushes.DodgerBlue, "UpperKC"); // index 0
                AddPlot(Brushes.DodgerBlue, "LowerKC"); // index 1
            }
            else if (State == State.DataLoaded)
            {
                ema = EMA(EmaPeriod);
                roc = ROC(Close, RocPeriod);
                adx = ADX(AdxPeriod);
                atr = ATR(AtrPeriod);

                AddChartIndicator(ema);
                AddChartIndicator(roc);
                AddChartIndicator(atr);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(Math.Max(AdxPeriod, RocPeriod), KeltnerPeriod))
                return;

            double emaVal = ema[0];
            double atrVal = atr[0];
            double adxVal = adx[0];
            double rocVal = roc[0];

            // Custom decimal Keltner bands
            double offset = KeltnerMultiplier * atrVal;
            upperKeltner = emaVal + offset;
            double lowerKeltner = emaVal - offset;

            Values[0][0] = upperKeltner;
            Values[1][0] = lowerKeltner;

            // Entry: Long on cross above upper Keltner
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                bool longSetup = CrossAbove(Close, upperKeltner, 1)
                                 && rocVal > RocEntryThreshold
                                 && atrVal > AtrThreshold
                                 && adxVal >= AdxLowThreshold && adxVal <= AdxHighThreshold;

                if (longSetup)
                {
                    entryPrice = Close[0];
                    EnterLong("EnterLongSignal");
                    SetStopLoss(CalculationMode.Ticks, StopLossTicks);
                }
            }

            // Exit: Close long when price falls below upper Keltner
            if (Position.MarketPosition == MarketPosition.Long)
            {
                if (Close[0] <= upperKeltner)
                    ExitLong("ExitKeltner", "EnterLongSignal");
            }
        }
    }
}

