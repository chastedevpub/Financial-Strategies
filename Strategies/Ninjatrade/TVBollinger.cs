// NinjaTrader strategy version of "Bollinger Bands Strategy with ROC Threshold"
// Exposes parameters for backtesting and allows for optimization

#region Using declarations
using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.Data;
using System.ComponentModel.DataAnnotations;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class TVBollinger : Strategy
    {
        // User-defined parameters exposed for optimization/testing
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "BB Length", Order = 1, GroupName = "Parameters")]
        public int BbLength { get; set; } = 50;

        [NinjaScriptProperty]
        [Range(0.001, 50.0)]
        [Display(Name = "BB Multiplier", Order = 2, GroupName = "Parameters")]
        public double BbMultiplier { get; set; } = 2.1;

        [NinjaScriptProperty]
        [Range(-1, 1)]
        [Display(Name = "Strategy Direction (-1=Short, 0=Both, 1=Long)", Order = 3, GroupName = "Parameters")]
        public int Direction { get; set; } = 1;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Base Position Size (Contracts)", Order = 4, GroupName = "Parameters")]
        public int BasePositionSize { get; set; } = 2;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Take Profit (Ticks)", Order = 5, GroupName = "Parameters")]
        public int TakeProfitTicks { get; set; } = 40;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Stop Loss (Ticks)", Order = 6, GroupName = "Parameters")]
        public int StopLossTicks { get; set; } = 32;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ROC Period", Order = 7, GroupName = "Parameters")]
        public int RocPeriod { get; set; } = 3;

        [NinjaScriptProperty]
        [Display(Name = "ROC Threshold (%)", Order = 8, GroupName = "Parameters")]
        public double RocThreshold { get; set; } = -0.01;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ATR Length", Order = 9, GroupName = "Parameters")]
        public int AtrLength { get; set; } = 14;

        // Internal variables for indicator calculations
        private double _basis, _dev, _upper, _lower, _rocValue, _atr;
        // Order references for possible cancellation (see OnExecutionUpdate)
        private Order lastLongOrder;
        private Order lastShortOrder;
        private string longOrderName = "BBandLE";
        private string shortOrderName = "BBandSE";
        private string ocoName = "BollingerBands";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "TVBollinger";
                // Use the correct Calculate enum value
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = Math.Max(BbLength, RocPeriod) + 2;
                IsInstantiatedOnEachOptimizationIteration = true;
            }
            else if (State == State.DataLoaded)
            {
                lastLongOrder = null;
                lastShortOrder = null;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(BbLength, RocPeriod))
                return;

            // 1. Calculate Bollinger Bands
            _basis = SMA(Close, BbLength)[0];
            _dev = BbMultiplier * StdDev(Close, BbLength)[0];
            _upper = _basis + _dev;
            _lower = _basis - _dev - 2; // "-2" matches the custom offset in the Pine script

            // 2. Calculate ROC
            _rocValue = RateOfChange(Close, RocPeriod);

            // 3. Calculate ATR (not directly used for position sizing, but available)
            _atr = ATR(AtrLength)[0];

            // 4. Strategy Direction filter
            bool allowLong = Direction == 0 || Direction > 0;
            bool allowShort = Direction == 0 || Direction < 0;
            int posSize = Math.Max(BasePositionSize, 1);

            // 5. Cancel old working orders (imitate Pine behavior)
            if (allowLong && lastLongOrder != null)
            {
                // Check order state before canceling
                if (lastLongOrder.OrderState == OrderState.Working || lastLongOrder.OrderState == OrderState.Accepted)
                    CancelOrder(lastLongOrder);
                lastLongOrder = null;
            }
            if (allowShort && lastShortOrder != null)
            {
                if (lastShortOrder.OrderState == OrderState.Working || lastShortOrder.OrderState == OrderState.Accepted)
                    CancelOrder(lastShortOrder);
                lastShortOrder = null;
            }

            // --- ENTRY LOGIC ---
            // Long Entry: Price crosses above lower band AND ROC > threshold
            if (allowLong
                && CrossAbove(Close, _lower, 1)
                && _rocValue > RocThreshold)
            {
                lastLongOrder = EnterLong(posSize, longOrderName);
            }

            // Short Entry: Price crosses below upper band AND ROC < -threshold
            if (allowShort
                && CrossBelow(Close, _upper, 1)
                && _rocValue < -RocThreshold)
            {
                lastShortOrder = EnterShort(posSize, shortOrderName);
            }

            // --- EXIT LOGIC ---
            // Use custom profit/stop logic as in PineScript (mimics market exit if target hit)
            double tickSize = TickSize;
            // Long
            if (Position.MarketPosition == MarketPosition.Long)
            {
                double longTp = Position.AveragePrice + TakeProfitTicks * tickSize;
                double longSl = Position.AveragePrice - StopLossTicks * tickSize;
                // Simulate Pine market order exits
                if (Close[0] >= longTp)
                    ExitLong(longOrderName + "_TP", longOrderName);
                else if (Close[0] <= longSl)
                    ExitLong(longOrderName + "_SL", longOrderName);
            }
            // Short
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                double shortTp = Position.AveragePrice - TakeProfitTicks * tickSize;
                double shortSl = Position.AveragePrice + StopLossTicks * tickSize;
                if (Close[0] <= shortTp)
                    ExitShort(shortOrderName + "_TP", shortOrderName);
                else if (Close[0] >= shortSl)
                    ExitShort(shortOrderName + "_SL", shortOrderName);
            }
        }

        // Helper for ROC calculation
        private double RateOfChange(ISeries<double> series, int period)
        {
            if (CurrentBar < period)
                return 0.0;
            double prev = series[period];
            double curr = series[0];
            return prev == 0.0 ? 0.0 : (curr - prev) / prev;
        }

        // Track references to entry orders using OnExecutionUpdate (not OnOrderUpdate which is not available in base Strategy)
        protected override void OnExecutionUpdate(Cbi.Execution execution, string executionId, double price, int quantity, Cbi.MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution.Order != null)
            {
                if (execution.Order.Name == longOrderName)
                {
                    lastLongOrder = execution.Order;
                }
                else if (execution.Order.Name == shortOrderName)
                {
                    lastShortOrder = execution.Order;
                }
            }
        }
    }
}
