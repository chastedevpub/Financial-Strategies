using System;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class Sideways_ADXROC_EMA_Keltner_ChopStrategy : Strategy
    {
        private double entryPrice;
        private double atrValue;
        private double upperKeltner;
        private double lowerKeltner;
        private double highestSinceEntry;
        private double trailingStop;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Strategy for choppy/sideways markets using ADX, ROC, EMA (entry), custom decimal Keltner Channels, and ATR-based trailing stop.";
                Name = "Sideways_ADXROC_EMA_Keltner_ChopStrategy";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                IsUnmanaged = false;
                IncludeCommission = true;

                // Indicator periods
                AdxPeriod = 14;
                RocPeriod = 9;
                AtrPeriod = 14;
                EmaPeriod = 20;
                KeltnerPeriod = 20;
                KeltnerMultiplier = 1.5;

                // Thresholds
                AdxLowThreshold = 10;
                AdxHighThreshold = 25;
                RocEntryThreshold = 0.02;
                RocExitThreshold = -0.02;
                AtrThreshold = 1.0;
                AtrTrailingMultiplier = 2.0;

                // Trade session time
                StartHour = 9;
                StartMinute = 30;
                EndHour = 16;
                EndMinute = 0;

                AddPlot(Brushes.Blue, "EMA");
                AddPlot(Brushes.Gray, "UpperKeltner");
                AddPlot(Brushes.DarkGray, "LowerKeltner");
            }
        }

        protected override void OnBarUpdate()
        {
            int maxPeriod = Math.Max(Math.Max(AdxPeriod, RocPeriod), Math.Max(AtrPeriod, EmaPeriod));
            if (CurrentBar < maxPeriod)
                return;

            // Time filter
            int currentHour = Time[0].Hour;
            int currentMinute = Time[0].Minute;
            bool withinTimePeriod = (currentHour > StartHour || (currentHour == StartHour && currentMinute >= StartMinute))
                                    && (currentHour < EndHour || (currentHour == EndHour && currentMinute <= EndMinute));
            if (!withinTimePeriod)
                return;

            // Calculate indicators
            double adxValue = ADX(AdxPeriod)[0];
            double rocValue = ROC(Close, RocPeriod)[0];
            atrValue = ATR(AtrPeriod)[0];
            double emaValue = EMA(EmaPeriod)[0];

            // Manually calculate Keltner Channels using decimal multiplier
            double middleKeltner = EMA(KeltnerPeriod)[0];
            double keltnerATR = ATR(KeltnerPeriod)[0];
            upperKeltner = middleKeltner + (keltnerATR * KeltnerMultiplier);
            lowerKeltner = middleKeltner - (keltnerATR * KeltnerMultiplier);

            // Plot EMA and Keltner bands
            Values[0][0] = emaValue;
            Values[1][0] = upperKeltner;
            Values[2][0] = lowerKeltner;

            // ENTRY: Cross above EMA + ROC + ATR threshold + ADX range
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (CrossAbove(Close, emaValue, 1)
                    && rocValue > RocEntryThreshold
                    && atrValue < AtrThreshold
                    && adxValue >= AdxLowThreshold && adxValue <= AdxHighThreshold)
                {
                    entryPrice = Close[0];
                    highestSinceEntry = High[0]; // initialize
                    EnterLong("EnterLongSignal");
                }
            }

            // EXIT logic
            if (Position.MarketPosition == MarketPosition.Long)
            {
                highestSinceEntry = Math.Max(highestSinceEntry, High[0]);
                trailingStop = highestSinceEntry - (atrValue * AtrTrailingMultiplier);

                if (Close[0] < trailingStop)
                {
                    ExitLong("ExitTrailingStop", "EnterLongSignal");
                }
                else if (Close[0] >= upperKeltner)
                {
                    ExitLong("ExitKeltner", "EnterLongSignal");
                }
                else if (CrossBelow(Close, emaValue, 1) && rocValue < RocExitThreshold)
                {
                    ExitLong("ExitEMA_ROC", "EnterLongSignal");
                }
            }
        }

        #region Properties

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
        public double RocExitThreshold { get; set; }

        [NinjaScriptProperty]
        public double AtrThreshold { get; set; }

        [NinjaScriptProperty]
        public double AtrTrailingMultiplier { get; set; }

        [NinjaScriptProperty]
        public int StartHour { get; set; }

        [NinjaScriptProperty]
        public int StartMinute { get; set; }

        [NinjaScriptProperty]
        public int EndHour { get; set; }

        [NinjaScriptProperty]
        public int EndMinute { get; set; }

        #endregion
    }
}
