using System;
using System.Windows.Media;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class Adaptive_ATR_IQR_ROC_DayTrade : Strategy
    {
        //—— Indicator handles ——————————————————————————
        private ATR atr;
        private IQR iqr;
        private ROC rocExit;       // used for exit logic
        private ROC rocEntry;      // used for entry threshold
        private EMA ema;

        //—— Cached values for plotting (optional) ————————
        private double currentAtr;
        private double currentIqr;
        private double currentRocExit;
        private double currentRocEntry;
        private double currentEma;

        //—— Internal tracking ————————————————————————
        private double highestSinceEntry = 0.0;
        private int entryBarIndex     = -1;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description =
                    "Enter a long trade once flat—but only if:\n" +
                    " • Close > EMA (configurable period)\n" +
                    " • ROC (configurable period) > entry threshold\n" +
                    "Once filled, manage (adapt) the trade:\n" +
                    " • ATR‐based trailing stop\n" +
                    " • Exit if IQR (choppiness) exceeds a threshold\n" +
                    " • Exit if ROC (exit‐period) turns negative below a threshold\n" +
                    " • Forced exit before EndOfDayExitTime\n" +
                    " • If any short position appears, close it immediately\n\n" +
                    "Remains a pure day‐trade: no overnight holding.";
                Name = "Adaptive_ATR_IQR_ROC_DayTrade";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                IncludeCommission = true;

                //———— DEFAULT PARAMETERS ——————————————————
                ATRPeriod            = 14;
                IQRPeriod            = 14;
                RocExitPeriod        = 9;
                RocEntryPeriod       = 9;
                EMAPeriod            = 20;

                // ATR trailing stop multiplier
                ATRTrailingMultiplier = 2.0;

                // If IQR (choppiness index) rises above this, exit
                IQRChopThreshold     = 60.0;

                // If ROC (exit‐period) falls below this negative threshold, exit
                RocExitThreshold     = -0.02;

                // If ROC (entry‐period) must exceed this to allow entry
                RocEntryThreshold    = 0.01;

                // End‐of‐day forced exit time (HHmm)
                EndOfDayExitTime     = 1555;

                // Plot current ATR, IQR, ROC_exit, ROC_entry, EMA (optional)
                AddPlot(Brushes.Orange, "Plot_ATR");
                AddPlot(Brushes.Blue,   "Plot_IQR");
                AddPlot(Brushes.Green,  "Plot_ROC_Exit");
                AddPlot(Brushes.Red,    "Plot_ROC_Entry");
                AddPlot(Brushes.Gray,   "Plot_EMA");
            }
            else if (State == State.Configure)
            {
                // Instantiate indicators
                atr      = ATR(ATRPeriod);
                iqr      = IQR(IQRPeriod);
                rocExit  = ROC(Close, RocExitPeriod);
                rocEntry = ROC(Close, RocEntryPeriod);
                ema      = EMA(EMAPeriod);

                // Add to chart for visualization (optional)
                AddChartIndicator(atr);
                AddChartIndicator(iqr);
                AddChartIndicator(rocExit);
                AddChartIndicator(rocEntry);
                AddChartIndicator(ema);
            }
        }

        protected override void OnBarUpdate()
        {
            //———— IMMEDIATELY CLOSE ANY SHORT POSITIONS —————————————————
            if (Position.MarketPosition == MarketPosition.Short)
            {
                ExitShort("ExitShort_ImmediateClose");
                return;
            }

            //——– Ensure enough bars for all indicators —————————————————
            int maxPeriod = Math.Max(Math.Max(ATRPeriod, IQRPeriod),
                                     Math.Max(RocExitPeriod, Math.Max(RocEntryPeriod, EMAPeriod)));
            if (CurrentBar < maxPeriod)
                return;

            //——– Cache the current indicator values ————————————————————
            currentAtr       = atr[0];
            currentIqr       = iqr[0];
            currentRocExit   = rocExit[0];
            currentRocEntry  = rocEntry[0];
            currentEma       = ema[0];

            //——– Plot them for backtest/chart view (optional) —————————
            Values[0][0] = currentAtr;
            Values[1][0] = currentIqr;
            Values[2][0] = currentRocExit;
            Values[3][0] = currentRocEntry;
            Values[4][0] = currentEma;

            //———— ENTRY LOGIC — can only enter if:
            //  1) We’re Flat
            //  2) CurrentBar > maxPeriod
            //  3) Close > EMA
            //  4) ROC_entry > RocEntryThreshold
            if (Position.MarketPosition == MarketPosition.Flat && CurrentBar > maxPeriod)
            {
                if (Close[0] > currentEma && currentRocEntry > RocEntryThreshold)
                {
                    EnterLong("Long_ImmediateEntry");
                    entryBarIndex     = CurrentBar;
                    highestSinceEntry = High[0];
                }
                return;
            }

            //———— TRADE MANAGEMENT (only if we’re long) —————————————————————
            if (Position.MarketPosition == MarketPosition.Long)
            {
                // 1) Update highest high since entry
                if (High[0] > highestSinceEntry)
                    highestSinceEntry = High[0];

                // 2) Compute ATR‐based trailing stop
                double trailingStop = highestSinceEntry - (currentAtr * ATRTrailingMultiplier);

                // 3) Exit conditions:
                //    a) Price falls below ATR‐trailing stop
                bool exitByAtrTrail = Close[0] < trailingStop;

                //    b) IQR (choppiness) spikes above threshold → too choppy, exit
                bool exitByIQR = currentIqr > IQRChopThreshold;

                //    c) ROC_exit turns sufficiently negative → momentum reversal, exit
                bool exitByRoc = currentRocExit < RocExitThreshold;

                //    d) Mandatory end‐of‐day flatten
                int hhmm      = Time[0].Hour * 100 + Time[0].Minute;
                bool exitByEod = hhmm >= EndOfDayExitTime;

                // Evaluate exits in priority order
                if (exitByAtrTrail)
                    ExitLong("Exit_ATR_Trail", "Long_ImmediateEntry");
                else if (exitByIQR)
                    ExitLong("Exit_By_IQR_Chop", "Long_ImmediateEntry");
                else if (exitByRoc)
                    ExitLong("Exit_By_ROC_Neg", "Long_ImmediateEntry");
                else if (exitByEod)
                    ExitLong("Exit_EndOfDay", "Long_ImmediateEntry");
            }
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(5, 50)]
        [Display(Name = "ATR Period", Order = 1, GroupName = "Parameters")]
        public int ATRPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(5, 50)]
        [Display(Name = "IQR Period", Order = 2, GroupName = "Parameters")]
        public int IQRPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(5, 30)]
        [Display(Name = "ROC Exit Period", Order = 3, GroupName = "Parameters")]
        public int RocExitPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(5, 30)]
        [Display(Name = "ROC Entry Period", Order = 4, GroupName = "Parameters")]
        public int RocEntryPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ROC Entry Threshold", Order = 5, GroupName = "Parameters")]
        public double RocEntryThreshold { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EMA Period", Order = 6, GroupName = "Parameters")]
        public int EMAPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, 5.0)]
        [Display(Name = "ATR Trailing Multiplier", Order = 7, GroupName = "Exit Parameters")]
        public double ATRTrailingMultiplier { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 100.0)]
        [Display(Name = "IQR Chop Threshold", Order = 8, GroupName = "Exit Parameters")]
        public double IQRChopThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(-1.0, 0.0)]
        [Display(Name = "ROC Exit Threshold", Order = 9, GroupName = "Exit Parameters")]
        public double RocExitThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(0, 2359)]
        [Display(Name = "End Of Day Exit (HHmm)", Order = 10, GroupName = "Exit Parameters")]
        public int EndOfDayExitTime { get; set; }

        #endregion
    }
}
