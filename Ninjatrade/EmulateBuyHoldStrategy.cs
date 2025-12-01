using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class EmulateBuyHoldStrategy : Strategy
    {
        private ROC roc;
        private RSI rsi;
        private ATR atr;

        private double highestHighSinceEntry;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Emulates buy and hold using ATR, RSI, ROC for day trading futures";
                Name = "EmulateBuyHoldStrategy";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30; // Exit 30 seconds before session close
                IsFillLimitOnTouch = false;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = true; // Enable to debug order issues
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 50; // Enough for ROC

                // Configurable parameters (relaxed for testing)
                RocPeriod = 20;
                RocThreshold = -5;
                RsiPeriod = 14;
                RsiLower = 30;
                RsiUpper = 80;
                AtrPeriod = 14;
                StopMultiplier = 1.5;
                TrailMultiplier = 2.5;
                RiskPercent = 2.0;
                AccountSize = 100000;
                ExitHour = 15; // e.g., 3 PM for ES close at 4 PM ET
                ExitMinute = 59;
                AllowAnyBarEntry = true; // Allow entries on any bar for testing
            }
            else if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Day, 1);
            }
            else if (State == State.DataLoaded)
            {
                roc = ROC(BarsArray[1], RocPeriod);
                rsi = RSI(BarsArray[1], RsiPeriod, 3);
                atr = ATR(BarsArray[1], AtrPeriod);
                Print("Indicators initialized: ROC, RSI, ATR");
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0) return; // Only process primary bars (intraday)

            // Check data availability
            if (CurrentBars[0] < BarsRequiredToTrade || CurrentBars[1] < BarsRequiredToTrade)
            {
                Print($"{Time[0]}: Insufficient data. Primary bars: {CurrentBars[0]}, Secondary bars: {CurrentBars[1]}");
                return;
            }

            // Verify data sync
            DateTime primaryTime = BarsArray[0].GetTime(0).Date;
            DateTime secondaryTime = BarsArray[1].GetTime(0).Date;
            if (primaryTime != secondaryTime)
            {
                Print($"{Time[0]}: Data sync issue. Primary time: {primaryTime}, Secondary time: {secondaryTime}");
            }

            // Use previous daily bar values (closed bar)
            double rocValue = roc[1];
            double rsiValue = rsi[1];
            double atrValue = atr[1];

            // Debug indicator values and session info
            bool isEntryBar = AllowAnyBarEntry || Bars.IsFirstBarOfSession;
            Print($"{Time[0]}: ROC={rocValue:F2}, RSI={rsiValue:F2}, ATR={atrValue:F2}, IsFirstBarOfSession={Bars.IsFirstBarOfSession}, AllowAnyBarEntry={AllowAnyBarEntry}, IsEntryBar={isEntryBar}");

            // Entry logic
            if (isEntryBar && Position.MarketPosition == MarketPosition.Flat)
            {
                if (rocValue > RocThreshold && rsiValue > RsiLower && rsiValue < RsiUpper)
                {
                    // Calculate position size
                    double stopDistance = StopMultiplier * atrValue;
                    double riskAmount = AccountSize * (RiskPercent / 100.0);
                    double tickValue = Instrument.MasterInstrument.PointValue * TickSize; // Value per tick
                    double ticksInStop = stopDistance / TickSize;
                    int quantity = (int)Math.Floor(riskAmount / (ticksInStop * tickValue));

                    // Ensure at least 1 contract
                    quantity = Math.Max(1, quantity);

                    Print($"{Time[0]}: Entry conditions met. ROC={rocValue:F2}, RSI={rsiValue:F2}, ATR={atrValue:F2}, Quantity={quantity}, StopDistance={stopDistance:F2}");

                    if (quantity > 0)
                    {
                        EnterLong(quantity, "LongEntry");
                        highestHighSinceEntry = High[0];
                        Print($"{Time[0]}: Entered long with {quantity} contracts");
                    }
                    else
                    {
                        Print($"{Time[0]}: No entry - Quantity calculated as 0");
                    }
                }
                else
                {
                    Print($"{Time[0]}: Entry conditions not met. ROC={rocValue:F2} <= {RocThreshold}, RSI={rsiValue:F2} not in [{RsiLower}, {RsiUpper}]");
                }
            }
            else if (!isEntryBar)
            {
                Print($"{Time[0]}: No entry - Not an entry bar (IsFirstBarOfSession={Bars.IsFirstBarOfSession}, AllowAnyBarEntry={AllowAnyBarEntry})");
            }

            // Manage position: trail stop and exit at EOD
            if (Position.MarketPosition == MarketPosition.Long)
            {
                // Update highest high
                highestHighSinceEntry = Math.Max(highestHighSinceEntry, High[0]);

                // Calculate trail stop
                double initialStop = Position.AveragePrice - StopMultiplier * atrValue;
                double trailStop = highestHighSinceEntry - TrailMultiplier * atrValue;

                // Use the tighter stop (higher for long)
                double currentStop = Math.Max(initialStop, trailStop);

                SetStopLoss("LongEntry", CalculationMode.Price, currentStop, false);

                // Debug stop levels
                Print($"{Time[0]}: In position. InitialStop={initialStop:F2}, TrailStop={trailStop:F2}, CurrentStop={currentStop:F2}");

                // Exit near session close
                if (ToTime(Time[0]) >= ToTime(ExitHour, ExitMinute, 0))
                {
                    ExitLong("EODExit", "LongEntry");
                    Print($"{Time[0]}: Exiting position at EOD");
                }
            }
        }

        #region Properties
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ROC Period", Order = 1, GroupName = "Parameters")]
        public int RocPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ROC Threshold", Order = 2, GroupName = "Parameters")]
        public double RocThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "RSI Period", Order = 3, GroupName = "Parameters")]
        public int RsiPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RSI Lower Bound", Order = 4, GroupName = "Parameters")]
        public double RsiLower { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RSI Upper Bound", Order = 5, GroupName = "Parameters")]
        public double RsiUpper { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ATR Period", Order = 6, GroupName = "Parameters")]
        public int AtrPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Stop Multiplier", Order = 7, GroupName = "Parameters")]
        public double StopMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail Multiplier", Order = 8, GroupName = "Parameters")]
        public double TrailMultiplier { get; set; }

        [NinjaScriptProperty]
        [Range(0.01, double.MaxValue)]
        [Display(Name = "Risk Percent", Order = 9, GroupName = "Parameters")]
        public double RiskPercent { get; set; }

        [NinjaScriptProperty]
        [Range(1000, double.MaxValue)]
        [Display(Name = "Account Size", Order = 10, GroupName = "Parameters")]
        public double AccountSize { get; set; }

        [NinjaScriptProperty]
        [Range(0, 23)]
        [Display(Name = "Exit Hour", Order = 11, GroupName = "Parameters")]
        public int ExitHour { get; set; }

        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "Exit Minute", Order = 12, GroupName = "Parameters")]
        public int ExitMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Allow Entry on Any Bar", Order = 13, GroupName = "Parameters")]
        public bool AllowAnyBarEntry { get; set; }
        #endregion
    }
}