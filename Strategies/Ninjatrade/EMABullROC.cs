using System;
using System.Windows.Media;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class EMABullROCStrategy : Strategy
    {
        private ROC roc;
        private ChoppinessIndex choppiness;
        private double emaValue;
        private double rocValue;
        private double chopValue;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Bull strategy: enter long when price > EMA and ROC > threshold, with configurable trailing stop and dynamic sizing based on choppiness";
                Name = "EMABullROCStrategy";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                IncludeCommission = true;

                // Configurable parameters
                EmaPeriod = 20;
                RocPeriod = 12;
                RocThreshold = 0.01;
                TrailingStopTicks = 20;
                ChopPeriod = 14;

                AddPlot(Brushes.Blue, "EMA");
            }
            else if (State == State.DataLoaded)
            {
                // instantiate indicators
                roc = ROC(RocPeriod);
                choppiness = ChoppinessIndex(ChopPeriod);

                // add to chart panels
                AddChartIndicator(roc);
                AddChartIndicator(choppiness);
            }
        }

        protected override void OnBarUpdate()
        {
            // ensure enough bars
            int maxPeriod = Math.Max(Math.Max(EmaPeriod, RocPeriod), ChopPeriod);
            if (CurrentBar < maxPeriod)
                return;

            // calculate indicators
            emaValue = EMA(EmaPeriod)[0];
            rocValue = roc[0];
            chopValue = choppiness[0];

            // plot EMA
            Values[0][0] = emaValue;

            // determine size by choppiness
            int size = chopValue > 60     ? 1
                     : chopValue > 52     ? 2
                     : chopValue > 46     ? 3
                     : chopValue > 38     ? 4
                     :                        5;

            // entry logic: price above EMA and ROC above threshold
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (Close[0] > emaValue && rocValue > RocThreshold)
                    EnterLong(size, "Long_EMA_ROC");
            }

            // manage trailing stop and exit logic
            if (Position.MarketPosition == MarketPosition.Long)
            {
                // configure trailing stop in ticks
                SetTrailStop(CalculationMode.Ticks, TrailingStopTicks);

                // exit when price crosses below EMA
                if (CrossBelow(Close, emaValue, 1))
                    ExitLong("Exit_EMA", "Long_EMA_ROC");
            }
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA Period", Order = 1, GroupName = "Parameters")]
        public int EmaPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ROC Period", Order = 2, GroupName = "Parameters")]
        public int RocPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(double.MinValue, double.MaxValue)]
        [Display(Name = "ROC Threshold", Order = 3, GroupName = "Parameters")]
        public double RocThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Trailing Stop (ticks)", Order = 4, GroupName = "Parameters")]
        public int TrailingStopTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Choppiness Period", Order = 5, GroupName = "Parameters")]
        public int ChopPeriod { get; set; }

        #endregion
    }
}
