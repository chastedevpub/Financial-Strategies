#region Using declarations
using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.Core.FloatingPoint;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Data;
using NinjaTrader.Gui.Tools;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class TimeBasedROCIRQStrategy : Strategy
    {
        #region User-configured Parameters

        [NinjaScriptProperty]
        [Display(Name = "ROC Period", Order = 1, GroupName = "Parameters")]
        [Range(1, int.MaxValue)]
        public int RocPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "IRQ Period", Order = 2, GroupName = "Parameters")]
        [Range(1, int.MaxValue)]
        public int IRQPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "IRQ Threshold", Order = 3, GroupName = "Parameters")]
        public double IRQThreshold { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Stop Loss (Ticks)", Order = 4, GroupName = "Parameters")]
        [Range(1, int.MaxValue)]
        public int StopLossTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Order Quantity", Order = 5, GroupName = "Parameters")]
        [Range(1, int.MaxValue)]
        public int OrderQuantity { get; set; }

        #endregion

        #region Private members

        private ROC roc;   // built-in ROC indicator
        private ATR atr;   // placeholder for your IRQ indicator

        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                              = "Opens a daily long at 00:00, 07:00 or 15:00 when ROC>0 and IRQ>threshold.";
                Name                                     = "TimeBasedROCIRQStrategy";
                Calculate                                = Calculate.OnBarClose;
                EntriesPerDirection                      = 1;
                EntryHandling                            = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy             = true;
                ExitOnSessionCloseSeconds                = 30;
                IsInstantiatedOnEachOptimizationIteration = false;

                // defaults
                RocPeriod      = 14;
                IRQPeriod      = 14;
                IRQThreshold   = 1.0;
                StopLossTicks  = 10;
                OrderQuantity  = 1;
            }
            else if (State == State.Configure)
            {
                // apply a tick-based stop-loss to every entry
                SetStopLoss(CalculationMode.Ticks, StopLossTicks);
            }
            else if (State == State.DataLoaded)
            {
                // instantiate indicators with independent periods
                roc = ROC(RocPeriod);
                atr = ATR(IRQPeriod);  // replace ATR with your IRQ(...) if you have a custom IRQ indicator
            }
        }

        protected override void OnBarUpdate()
        {
            // wait for both indicators to warm up
            if (CurrentBar < Math.Max(RocPeriod, IRQPeriod))
                return;

            // only proceed at exactly 00:00, 07:00 or 15:00 (server time)
            int h = Time[0].Hour;
            int m = Time[0].Minute;
            if (!((h == 0 || h == 7 || h == 15) && m == 0))
                return;

            double rocVal = roc[0];
            double irqVal = atr[0];  // or your IRQ[0]

            // enter long if conditions met and not already long
            if (rocVal > 0
                && irqVal > IRQThreshold
                && Position.MarketPosition != MarketPosition.Long)
            {
                EnterLong(OrderQuantity, "DailyLong");
            }
        }
    }
}
