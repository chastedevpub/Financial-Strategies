using System;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Strategy: Buy when price crosses any Keltner Channel band, Choppiness crosses a threshold, and ADX > threshold.
    /// Includes a configurable trailing stop that activates after a set profit in ticks.
    /// </summary>
    public class ChoppyADXKeltnerStrategy : Strategy
    {
        private EMA ema;
        private ATR atr;
        private ADX adx;
        private ChoppinessIndex choppiness;

        private double upperKelt;
        private double lowerKelt;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description          = "Buy when price crosses any Keltner Channel band, Choppiness crosses threshold, and ADX > threshold; with trailing stop.";
                Name                 = "ChoppyADXKeltnerStrategy";
                Calculate            = Calculate.OnEachTick;
                IsOverlay            = true;
                IncludeCommission    = true;

                // Default indicator settings
                EmaPeriod            = 20;
                KeltnerPeriod        = 20;
                KeltnerMultiplier    = 1.5;
                AtrPeriod            = 14;
                AdxPeriod            = 14;
                AdxThreshold         = 25;
                ChoppinessPeriod     = 14;
                ChoppinessThreshold  = 38.2;

                // Trailing stop settings (in ticks)
                TrailingStartTicks   = 10;  // start trailing when up this many ticks
                TrailingStopTicks    = 5;   // trail stop distance in ticks

                // Plot Keltner Bands
                AddPlot(Brushes.Goldenrod, "UpperKelt");
                AddPlot(Brushes.Goldenrod, "LowerKelt");
            }
            else if (State == State.DataLoaded)
            {
                // Initialize indicators
                ema           = EMA(EmaPeriod);
                atr           = ATR(AtrPeriod);
                adx           = ADX(AdxPeriod);
                choppiness    = ChoppinessIndex(ChoppinessPeriod);

                // Add to chart
                AddChartIndicator(ema);
                AddChartIndicator(atr);
                AddChartIndicator(adx);
                AddChartIndicator(choppiness);
            }
        }

        protected override void OnBarUpdate()
        {
            // Ensure enough bars
            int lookback = Math.Max(Math.Max(EmaPeriod, KeltnerPeriod), Math.Max(AtrPeriod, Math.Max(AdxPeriod, ChoppinessPeriod)));
            if (CurrentBar < lookback)
                return;

            // Values
            double emaVal     = ema[0];
            double atrVal     = atr[0];
            double adxVal     = adx[0];
            double choppyVal  = choppiness[0];
            double offset     = KeltnerMultiplier * atrVal;
            upperKelt         = emaVal + offset;
            lowerKelt         = emaVal - offset;

            // Plot bands
            Values[0][0] = upperKelt;
            Values[1][0] = lowerKelt;

            // Entry logic
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                bool keltBreak  = CrossAbove(Close, upperKelt, 1) || CrossBelow(Close, lowerKelt, 1);
                bool chopSignal = CrossBelow(choppiness, ChoppinessThreshold, 1);
                bool adxSignal  = adxVal > AdxThreshold;

                if (keltBreak && chopSignal && adxSignal)
                {
                    EnterLong("ChoppyADXKeltLong");
                }
            }

            // Trailing stop logic for long position
            if (Position.MarketPosition == MarketPosition.Long)
            {
                double ticksUp = (Close[0] - Position.AveragePrice) / TickSize;
                if (ticksUp >= TrailingStartTicks)
                {
                    // activate/update trailing stop
                    SetTrailStop("ChoppyADXKeltLong", CalculationMode.Ticks, TrailingStopTicks, false);
                }
            }
        }

        #region User-configurable Properties
        [NinjaScriptProperty]
        public int EmaPeriod { get; set; }

        [NinjaScriptProperty]
        public int KeltnerPeriod { get; set; }

        [NinjaScriptProperty]
        public double KeltnerMultiplier { get; set; }

        [NinjaScriptProperty]
        public int AtrPeriod { get; set; }

        [NinjaScriptProperty]
        public int AdxPeriod { get; set; }

        [NinjaScriptProperty]
        public double AdxThreshold { get; set; }

        [NinjaScriptProperty]
        public int ChoppinessPeriod { get; set; }

        [NinjaScriptProperty]
        public double ChoppinessThreshold { get; set; }

        [NinjaScriptProperty]
        public double TrailingStartTicks { get; set; }

        [NinjaScriptProperty]
        public double TrailingStopTicks { get; set; }
        #endregion
    }
}
