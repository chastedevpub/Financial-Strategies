using System;
using System.Windows.Media;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class ADXROCShortWithEMA : Strategy
    {
        private double entryPrice;
        private double atrValue;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Dynamic ADX-ROC ATR Short Strategy with EMA filter";
                Name = "ADXROCShortWithEMA";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                IsUnmanaged = false;
                IncludeCommission = true;

                // Periods and thresholds (all dynamic)
                AdxPeriod = 3;
                RocPeriod = 1;
                AtrPeriod = 3;
                EmaPeriod = 14;

                ProfitTargetMultiplier = 1.0 / 8.0;
                TrailingStopMultiplier = 1.0 / 4.0;

                // Entry thresholds
                AdxThreshold = 60;
                RocThreshold = 0.02;
                // For the EMA condition, the price must be below (EMA - EmaOffset).
                // EmaOffset lets you adjust the strictness of the EMA condition.
                EmaOffset = 0.0;

                // Exit thresholds
                // The strategy will exit if the ROC recovers above ExitRocThreshold
                ExitRocThreshold = 0.0;

                // Trade only during a specified intraday period.
                StartHour = 9;
                StartMinute = 30;
                EndHour = 16;
                EndMinute = 0;

                AddPlot(Brushes.Transparent, "ADX");
                AddPlot(Brushes.Transparent, "ROC");
                AddPlot(Brushes.Transparent, "ATR");
                AddPlot(Brushes.Blue, "EMA");
            }
        }

        protected override void OnBarUpdate()
        {
            // Ensure enough bars are available for all indicators
            int maxPeriod = Math.Max(Math.Max(AdxPeriod, RocPeriod), Math.Max(AtrPeriod, EmaPeriod));
            if (CurrentBar < maxPeriod)
                return;

            // Only trade during specified session hours
            int currentHour = Time[0].Hour;
            int currentMinute = Time[0].Minute;
            bool withinTimePeriod = (currentHour > StartHour || (currentHour == StartHour && currentMinute >= StartMinute))
                                    && (currentHour < EndHour || (currentHour == EndHour && currentMinute <= EndMinute));
            if (!withinTimePeriod)
                return;

            // Calculate indicator values
            double adxValue = ADX(AdxPeriod)[0];
            double rocValue = ROC(Close, RocPeriod)[0];
            atrValue = ATR(AtrPeriod)[0];
            double emaValue = EMA(EmaPeriod)[0];

            // Plot EMA for visual reference
            Values[3][0] = emaValue;

            // ENTRY CONDITIONS:
            // Enter a short position if:
            // 1. The current price is below the EMA (adjusted by any EmaOffset)
            // 2. The ADX indicates a strong trend (>= AdxThreshold)
            // 3. The ROC is below its threshold (i.e. showing downward momentum)
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (Close[0] < (emaValue - EmaOffset) && adxValue >= AdxThreshold && rocValue < RocThreshold)
                {
                    entryPrice = Close[0];
                    EnterShort("EnterShortSignal");
                }
            }

            // EXIT CONDITIONS (only when in a short position):
            // Exit if either:
            // 1. The ROC recovers above the dynamic ExitRocThreshold, or
            // 2. The price crosses back above the EMA (i.e. the bearish condition is lost)
            if (Position.MarketPosition == MarketPosition.Short)
            {
                if (rocValue > ExitRocThreshold || CrossAbove(Close, EMA(EmaPeriod), 1))
                {
                    // Close the entire short position by linking the exit signal to the entry signal
                    ExitShort("ExitShortSignal", "EnterShortSignal");
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
        public double ProfitTargetMultiplier { get; set; }

        [NinjaScriptProperty]
        public double TrailingStopMultiplier { get; set; }

        [NinjaScriptProperty]
        public double AdxThreshold { get; set; }

        [NinjaScriptProperty]
        public double RocThreshold { get; set; }

        [NinjaScriptProperty]
        public double ExitRocThreshold { get; set; }

        [NinjaScriptProperty]
        public double EmaOffset { get; set; }

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
