// File: ShortROCIQRStrategy.cs
using System;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class ShortROCIQRStrategy : Strategy
    {
        private EMA ema;
        private ATR atr;
        private IQR iqr;
        private ROC roc;
        private ChoppinessIndex choppiness;

        private double highestSinceEntry;

        [NinjaScriptProperty]
        [Display(Name = "EMA Period", Order = 1)]
        public int EmaPeriod { get; set; } = 21;

        [NinjaScriptProperty]
        [Display(Name = "ROC Period", Order = 2)]
        public int RocPeriod { get; set; } = 9;

        [NinjaScriptProperty]
        [Display(Name = "ROC Threshold", Order = 3)]
        public double RocThreshold { get; set; } = -0.005;

        [NinjaScriptProperty]
        [Display(Name = "IQR Period", Order = 4)]
        public int IqrPeriod { get; set; } = 14;

        [NinjaScriptProperty]
        [Display(Name = "IQR Threshold", Order = 5)]
        public double IqrThreshold { get; set; } = 1.0;

        [NinjaScriptProperty]
        [Display(Name = "Choppiness Period", Order = 6)]
        public int ChopPeriod { get; set; } = 14;

        [NinjaScriptProperty]
        [Display(Name = "ATR Period", Order = 7)]
        public int AtrPeriod { get; set; } = 14;

        [NinjaScriptProperty]
        [Display(Name = "ATR Multiplier", Order = 8)]
        public double AtrMultiplier { get; set; } = 2.0;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "ShortROCIQRStrategy";
                Calculate = Calculate.OnEachTick;
                IsOverlay = false;
                IncludeCommission = true;
            }
            else if (State == State.Configure)
            {
                ema = EMA(EmaPeriod);
                atr = ATR(AtrPeriod);
                iqr = IQR(IqrPeriod);
                roc = ROC(RocPeriod);
                choppiness = ChoppinessIndex(ChopPeriod);

                AddChartIndicator(ema);
                AddChartIndicator(atr);
                AddChartIndicator(iqr);
                AddChartIndicator(roc);
                AddChartIndicator(choppiness);
            }
            else if (State == State.DataLoaded)
            {
                highestSinceEntry = 0;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(Math.Max(Math.Max(EmaPeriod, RocPeriod), Math.Max(IqrPeriod, AtrPeriod)), ChopPeriod))
                return;

            double price = Close[0];
            double emaVal = ema[0];
            double rocVal = roc[0];
            double iqrVal = iqr[0];
            double atrVal = atr[0];
            double chopVal = choppiness[0];

            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (price < emaVal && rocVal < RocThreshold && iqrVal >= IqrThreshold)
                {
                    // Position sizing: larger size when market is less choppy
                    int qty = 1;
                    if (chopVal < 38.0)
                        qty = 10;
                    else if (chopVal <= 60.0)
                        qty = 6;
                    else
                        qty = 1;

                    EnterShort(qty, "ShortEntry");
                    highestSinceEntry = High[0];
                }
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                highestSinceEntry = Math.Min(highestSinceEntry, Low[0]);
                double stopPrice = highestSinceEntry + (atrVal * AtrMultiplier);
                stopPrice = Instrument.MasterInstrument.RoundToTickSize(stopPrice);

                    ExitShort("TrailingStop", "ShortEntry");
                
            }
        }
    }
}
