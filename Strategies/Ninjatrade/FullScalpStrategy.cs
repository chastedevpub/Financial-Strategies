// Strategy: Full config with plots and short protection

using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class FullScalpStrategyWithShortExitAndPlots : Strategy
    {
        private KeltnerChannel keltner;
        private ROC roc;
        private ATR atr;
        private IQR iqr;
        private EMA ema;
        private ChoppinessIndex choppiness;

        [NinjaScriptProperty] public bool UseATRTargets { get; set; } = true;
        [NinjaScriptProperty] public int ATRPeriod { get; set; } = 14;
        [NinjaScriptProperty] public double StopMultiplier { get; set; } = 1.0;
        [NinjaScriptProperty] public double ProfitTargetMultiplier { get; set; } = 0.75;
        [NinjaScriptProperty] public int FixedStopTicks { get; set; } = 8;
        [NinjaScriptProperty] public int FixedProfitTicks { get; set; } = 10;
        [NinjaScriptProperty] public double ROCThreshold { get; set; } = 0.015;
        [NinjaScriptProperty] public int ROCPeriod { get; set; } = 5;
        [NinjaScriptProperty] public int IQRPeriod { get; set; } = 14;
        [NinjaScriptProperty] public double IQRThreshold { get; set; } = 0.15;
        [NinjaScriptProperty] public int KeltnerPeriod { get; set; } = 20;
        [NinjaScriptProperty] public double KeltnerMultiplier { get; set; } = 1.25;
        [NinjaScriptProperty] public int EMAPeriod { get; set; } = 13;
        [NinjaScriptProperty] public int ChoppinessPeriod { get; set; } = 14;
        [NinjaScriptProperty] public double VolumeThreshold { get; set; } = 900;
        [NinjaScriptProperty] public int ProfitThresholdForTrailingStopTicks { get; set; } = 8;
        [NinjaScriptProperty] public int TrailingStopTicks { get; set; } = 5;
        [NinjaScriptProperty] public bool UseATRTrailingStop { get; set; } = false;
        [NinjaScriptProperty] public double ATRTrailingStopMultiplier { get; set; } = 0.75;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "FullScalpStrategyWithShortExitAndPlots";
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
                keltner = KeltnerChannel(KeltnerMultiplier, KeltnerPeriod);
                roc = ROC(ROCPeriod);
                atr = ATR(ATRPeriod);
                iqr = IQR(IQRPeriod);
                ema = EMA(EMAPeriod);
                choppiness = ChoppinessIndex(ChoppinessPeriod);

                AddChartIndicator(keltner);  // Keltner on price panel

                roc.Plots[0].Brush = Brushes.Blue;
                atr.Plots[0].Brush = Brushes.Green;
                iqr.Plots[0].Brush = Brushes.Orange;
                ema.Plots[0].Brush = Brushes.Cyan;
                choppiness.Plots[0].Brush = Brushes.Magenta;

                AddChartIndicator(roc);
                AddChartIndicator(atr);
                AddChartIndicator(iqr);
                AddChartIndicator(ema);
                AddChartIndicator(choppiness);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(Math.Max(ROCPeriod, ATRPeriod), Math.Max(IQRPeriod, EMAPeriod)))
                return;

            int timeNow = ToTime(Time[0]);
            if (timeNow < 043000 || timeNow > 073000)
                return;

            // 🚫 SHORT PROTECTION: Exit immediately if holding short
            if (Position.MarketPosition == MarketPosition.Short)
            {
                ExitShort("ExitShort_Protection");
                return;
            }

            double stopLoss = UseATRTargets ? atr[0] * StopMultiplier : TickSize * FixedStopTicks;
            double profitTarget = UseATRTargets ? atr[0] * ProfitTargetMultiplier : TickSize * FixedProfitTicks;

            bool longSignal = Close[0] <= keltner.Lower[0] &&
                              roc[0] > ROCThreshold &&
                              iqr[0] > IQRThreshold &&
                              Close[0] > ema[0] &&
                              Volume[0] > VolumeThreshold;

            if (Position.MarketPosition == MarketPosition.Flat && longSignal)
            {
                SetStopLoss(CalculationMode.Price, Close[0] - stopLoss);
                SetProfitTarget(CalculationMode.Price, Close[0] + profitTarget);
                EnterLong();
            }

            // Trailing stop logic (if long only)
            if (Position.MarketPosition == MarketPosition.Long)
            {
                double avgPrice = Position.AveragePrice;
                bool profitReached = Close[0] > avgPrice + (TickSize * ProfitThresholdForTrailingStopTicks);

                if (profitReached)
                {
                    if (UseATRTrailingStop)
                        SetTrailStop(CalculationMode.Price, Close[0] - (atr[0] * ATRTrailingStopMultiplier));
                    else
                        SetTrailStop(CalculationMode.Ticks, TrailingStopTicks);
                }
            }
        }
    }
}
