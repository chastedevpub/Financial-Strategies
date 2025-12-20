using System.Windows.Media;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class ADXROCATRStrategy : Strategy
    {
        private double entryPrice;
        private double atrValue;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "ADX ROC ATR Strategy";
                Name = "ADX ROC ATR Strategy";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                IsUnmanaged = false;
                IncludeCommission = true;

                AdxPeriod = 3;
                RocPeriod = 1;
                AtrPeriod = 3;
                ProfitTargetMultiplier = 1.0 / 8.0;
                TrailingStopMultiplier = 1.0 / 4.0;
                AdxThreshold = 60;
                RocThreshold = 0.02;
                StartHour = 9;
                StartMinute = 30;
                EndHour = 16;
                EndMinute = 0;

                AddPlot(Brushes.Transparent, "ADX");
                AddPlot(Brushes.Transparent, "ROC");
                AddPlot(Brushes.Transparent, "ATR");
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < AdxPeriod || CurrentBar < RocPeriod || CurrentBar < AtrPeriod)
                return;

            int currentHour = Time[0].Hour;
            int currentMinute = Time[0].Minute;

            bool withinTimePeriod = (currentHour > StartHour || (currentHour == StartHour && currentMinute >= StartMinute)) &&
                                    (currentHour < EndHour || (currentHour == EndHour && currentMinute <= EndMinute));

            if (!withinTimePeriod)
                return;

            double adxValue = ADX(AdxPeriod)[0];
            double rocValue = ROC(Close, RocPeriod)[0];
            atrValue = ATR(AtrPeriod)[0];

            bool adxCondition = adxValue >= AdxThreshold;
            bool rocCondition = rocValue < RocThreshold;
            bool candleCondition = Close[0] < Open[0];

            if (adxCondition && rocCondition && candleCondition && Position.MarketPosition == MarketPosition.Flat)
            {
                entryPrice = Close[0];
                double profitTargetPrice = entryPrice + (atrValue * ProfitTargetMultiplier);
                double trailingStopTicks = (atrValue * TrailingStopMultiplier) / TickSize;

                EnterLong();
                SetProfitTarget(CalculationMode.Price, profitTargetPrice);
                SetTrailStop(CalculationMode.Ticks, trailingStopTicks);
            }
        }

        [NinjaScriptProperty]
        public int AdxPeriod { get; set; } = 3;

        [NinjaScriptProperty]
        public int RocPeriod { get; set; } = 1;

        [NinjaScriptProperty]
        public int AtrPeriod { get; set; } = 3;

        [NinjaScriptProperty]
        public double ProfitTargetMultiplier { get; set; } = 1.0 / 8.0;

        [NinjaScriptProperty]
        public double TrailingStopMultiplier { get; set; } = 1.0 / 4.0;

        [NinjaScriptProperty]
        public double AdxThreshold { get; set; } = 60;

        [NinjaScriptProperty]
        public double RocThreshold { get; set; } = 0.02;

        [NinjaScriptProperty]
        public int StartHour { get; set; } = 9;

        [NinjaScriptProperty]
        public int StartMinute { get; set; } = 30;

        [NinjaScriptProperty]
        public int EndHour { get; set; } = 16;

        [NinjaScriptProperty]
        public int EndMinute { get; set; } = 0;
    }
}
