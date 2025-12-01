using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class DScalpStrat : Strategy
    {
        private ROC roc;
        private ATR atr;
        private ChoppinessIndex choppiness;

        [NinjaScriptProperty] public bool UseATRStops { get; set; } = true;
        [NinjaScriptProperty] public int ATRPeriod { get; set; } = 14;
        [NinjaScriptProperty] public double StopMultiplier { get; set; } = 1.0;
        [NinjaScriptProperty] public int FixedStopTicks { get; set; } = 8;
        [NinjaScriptProperty] public int ProfitTargetTicks { get; set; } = 10;
        [NinjaScriptProperty] public double ROCThreshold { get; set; } = 0.015;
        [NinjaScriptProperty] public int ROCPeriod { get; set; } = 5;
        [NinjaScriptProperty] public int ChoppinessPeriod { get; set; } = 14;
        [NinjaScriptProperty] public double VolumeThreshold { get; set; } = 900;
        [NinjaScriptProperty] public int ProximityThreshold { get; set; } = 8;
        [NinjaScriptProperty] public int ProfitThresholdForTrailingStopTicks { get; set; } = 8;
        [NinjaScriptProperty] public int TrailingStopTicks { get; set; } = 5;
        [NinjaScriptProperty] public bool UseATRTrailingStop { get; set; } = false;
        [NinjaScriptProperty] public double ATRTrailingStopMultiplier { get; set; } = 0.75;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "DScalpStrat_ShortOnly";
                Calculate = Calculate.OnEachTick;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 60;
                IsInstantiatedOnEachOptimizationIteration = false;
            }
            else if (State == State.Configure)
            {
                SetStopLoss(CalculationMode.Price, 0);
                SetProfitTarget(CalculationMode.Price, 0);
            }
            else if (State == State.DataLoaded)
            {
                roc = ROC(ROCPeriod);
                atr = ATR(ATRPeriod);
                choppiness = ChoppinessIndex(ChoppinessPeriod);

                roc.Plots[0].Brush = Brushes.Blue;
                atr.Plots[0].Brush = Brushes.Green;
                choppiness.Plots[0].Brush = Brushes.Magenta;

                AddChartIndicator(roc);
                AddChartIndicator(atr);
                AddChartIndicator(choppiness);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(ROCPeriod, ATRPeriod))
                return;

            int timeNow = ToTime(Time[0]);
            if (timeNow < 043000 || timeNow > 093000)
                return;

            // LONG PROTECTION: exit any long immediately
            if (Position.MarketPosition == MarketPosition.Long)
            {
                ExitLong("ExitLong_Protection");
                return;
            }

            // Compute stops and targets
            double stopLoss      = UseATRStops ? atr[0] * StopMultiplier : TickSize * FixedStopTicks;
            double profitTarget  = TickSize * ProfitTargetTicks;

            // Proximity to 5-point levels
            int priceInt = (int)Close[0];
            int rem5     = priceInt % 5;
            int distTo5  = Math.Min(rem5, 5 - rem5);

            // Short entry: ROC cross below negative threshold, proximity, volume
            bool shortSignal = CrossBelow(roc, -ROCThreshold, 1)
                               && distTo5 < ProximityThreshold
                               && Volume[0] > VolumeThreshold;

            if (Position.MarketPosition == MarketPosition.Flat && shortSignal)
            {
                int qty = choppiness[0] < 38 ? 10 : choppiness[0] <= 60 ? 5 : 1;
                SetStopLoss(CalculationMode.Price, Close[0] + stopLoss);
                SetProfitTarget(CalculationMode.Price, Close[0] - profitTarget);
                EnterShort(qty);
            }

            // Trailing stop for short
            if (Position.MarketPosition == MarketPosition.Short)
            {
                double avgPrice       = Position.AveragePrice;
                bool profitReached    = Close[0] < avgPrice - (TickSize * ProfitThresholdForTrailingStopTicks);

                if (profitReached)
                {
                    if (UseATRTrailingStop)
                        SetTrailStop(CalculationMode.Price, Close[0] + (atr[0] * ATRTrailingStopMultiplier));
                    else
                        SetTrailStop(CalculationMode.Ticks, TrailingStopTicks);
                }
            }
        }
    }
}
