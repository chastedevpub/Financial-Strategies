using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class DailyEMA_IQR_Strategy : Strategy
    {
        private EMA ema;
        private IQR iqr;

        #region Properties

        [NinjaScriptProperty]
        public int StartHour { get; set; }

        [NinjaScriptProperty]
        public int StartMinute { get; set; }

        [NinjaScriptProperty]
        public int EndHour { get; set; }

        [NinjaScriptProperty]
        public int EndMinute { get; set; }

        [NinjaScriptProperty]
        public int EmaPeriod { get; set; }

        [NinjaScriptProperty]
        public int IqrPeriod { get; set; }

        [NinjaScriptProperty]
        public double IqrThreshold { get; set; }

        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Enter a long once price crosses above EMA (configurable period) and IQR > threshold, only between a start and shutdown time. Closes any short immediately and applies a 130-tick stop loss.";
                Name        = "DailyEMA_IQR_Strategy";
                Calculate   = Calculate.OnBarClose;
                IsOverlay   = false;
                IncludeCommission = true;

                // Default time window
                StartHour   = 9;
                StartMinute = 30;
                EndHour     = 16;
                EndMinute   = 0;

                // Default indicator settings
                EmaPeriod    = 20;
                IqrPeriod    = 14;
                IqrThreshold = 0.5;
            }
            else if (State == State.Configure)
            {
                // Instantiate indicators
                ema = EMA(EmaPeriod);
                iqr = IQR(IqrPeriod);

                // Add to chart for visualization
                AddChartIndicator(ema);
                AddChartIndicator(iqr);

                // Apply a 130-tick stop loss on the "LongEntry" signal
                SetStopLoss("LongEntry", CalculationMode.Ticks, 1000, false);
            }
        }

        protected override void OnBarUpdate()
        {
            // Wait for enough bars
            int maxPeriod = Math.Max(EmaPeriod, IqrPeriod);
            if (CurrentBar < maxPeriod)
                return;

            DateTime barTime = Time[0];
            int hour   = barTime.Hour;
            int minute = barTime.Minute;

            // 1) Protection: if any short position exists, close it immediately
            if (Position.MarketPosition == MarketPosition.Short)
            {
                ExitShort("ExitShortProtect", "ShortEntrySignal");
                return;
            }

            // 2) Only allow entries within the time window
            bool afterStart  = (hour > StartHour) || (hour == StartHour && minute >= StartMinute);
            bool beforeEnd   = (hour < EndHour)  || (hour == EndHour   && minute < EndMinute);

            if (!afterStart || !beforeEnd)
                return;

            // 3) Entry logic: only if flat
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                // Cross above EMA on this bar?
                bool crossedAboveEma = CrossAbove(Close, ema, 1);

                // IQR condition
                bool iqrCondition = iqr[0] > IqrThreshold;

                if (crossedAboveEma && iqrCondition)
                {
                    EnterLong("LongEntry");
                }
            }
        }
    }
}
