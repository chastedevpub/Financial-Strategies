using System;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class SidewaysRaschkeKeltner_ADX_RSI_ATR : Strategy
    {
        // Indicator handles
        private KeltnerChannel keltner;
        private ADX adx;
        private RSI rsi;
        private ATR atr;

        // Trailing stop variables
        private double highestSinceEntry = 0.0;
        private double trailingStop = 0.0;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description     = "Raschke-inspired sideways strategy: Entry on deep band cross + ATR, with ADX/RSI filter; exit at midline. All periods and thresholds configurable.";
                Name            = "SidewaysRaschkeKeltner_ADX_RSI_ATR";
                Calculate       = Calculate.OnEachTick;
                IsOverlay       = true;

                // --- Configurable Parameters ---
                KeltnerPeriod = 20;
                KeltnerMultiplier = 1.5;
                AdxPeriod = 14;
                AdxThreshold = 20;
                RsiPeriod = 14;
                RsiThreshold = 35;
                AtrPeriod = 14;
                TrailingMultiplier = 2.0;          // ATR x N for trailing stop
                EntryATRMultiplier = 1.0;           // ATR x N for entry overshoot
                StartHour = 9;
                StartMinute = 30;
                EndHour = 16;
                EndMinute = 0;
                Quantity = 1;

                AddPlot(Brushes.Blue, "KeltnerMid");
                AddPlot(Brushes.DarkGray, "KeltnerUpper");
                AddPlot(Brushes.DarkGray, "KeltnerLower");
            }
            else if (State == State.DataLoaded)
            {
                keltner = KeltnerChannel(KeltnerMultiplier, KeltnerPeriod);
                adx     = ADX(AdxPeriod);
                rsi     = RSI(Close, RsiPeriod, 3);
                atr     = ATR(AtrPeriod);

                AddChartIndicator(adx);
                AddChartIndicator(rsi);
            }
        }

        protected override void OnBarUpdate()
        {
            // Instantly flatten any accidental short position
            if (Position.MarketPosition == MarketPosition.Short)
            {
                ExitShort("FlattenShort", "");
                return;
            }

            int maxPeriod = Math.Max(Math.Max(Math.Max(KeltnerPeriod, AtrPeriod), Math.Max(AdxPeriod, RsiPeriod)), 3);
            if (CurrentBar < maxPeriod)
                return;

            // Time filter
            int currentHour = Time[0].Hour;
            int currentMinute = Time[0].Minute;
            bool withinTimePeriod = (currentHour > StartHour || (currentHour == StartHour && currentMinute >= StartMinute))
                                    && (currentHour < EndHour || (currentHour == EndHour && currentMinute <= EndMinute));
            if (!withinTimePeriod)
                return;

            // Indicator values
            double mid   = keltner.Midline[0];
            double upper = keltner.Upper[0];
            double lower = keltner.Lower[0];
            double atrValue  = atr[0];
            double adxValue  = adx[0];
            double rsiValue  = rsi[0];

            // Plot Keltner for price panel context
            Values[0][0] = mid;
            Values[1][0] = upper;
            Values[2][0] = lower;

            // ENTRY LOGIC: Buy only when Close crosses below (Lower Keltner - ATR * multiplier), and ADX < threshold, and RSI < threshold
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                double entryBand = lower - (atrValue * EntryATRMultiplier);

                if (
                    CrossBelow(Close, entryBand, 1) &&
                    adxValue < AdxThreshold &&
                    rsiValue < RsiThreshold
                )
                {
                    EnterLong(Quantity, "LongEntry");
                }
            }

            // EXIT LOGIC & TRAILING STOP
            if (Position.MarketPosition == MarketPosition.Long)
            {
                // Update highest high for trailing stop
                if (Position.Quantity > 0)
                    highestSinceEntry = Math.Max(highestSinceEntry, High[0]);
                else
                    highestSinceEntry = High[0];

                // Set trailing stop price
                trailingStop = highestSinceEntry - (atrValue * TrailingMultiplier);

                bool exit = false;

                // 1. Exit at EMA/Keltner midline
                if (CrossAbove(Close, mid, 1))
                {
                    ExitLong("ExitMid", "LongEntry");
                    exit = true;
                }
                // 2. ATR-based trailing stop
                else if (Close[0] < trailingStop)
                {
                    ExitLong("TrailingStop", "LongEntry");
                    exit = true;
                }

                if (exit)
                    highestSinceEntry = 0.0; // Reset
            }
            else
            {
                // If flat, reset highestSinceEntry for next trade
                highestSinceEntry = 0.0;
            }
        }

        #region Properties

        [NinjaScriptProperty]
        public int KeltnerPeriod { get; set; }

        [NinjaScriptProperty]
        public double KeltnerMultiplier { get; set; }

        [NinjaScriptProperty]
        public int AdxPeriod { get; set; }

        [NinjaScriptProperty]
        public double AdxThreshold { get; set; }

        [NinjaScriptProperty]
        public int RsiPeriod { get; set; }

        [NinjaScriptProperty]
        public double RsiThreshold { get; set; }

        [NinjaScriptProperty]
        public int AtrPeriod { get; set; }

        [NinjaScriptProperty]
        public double TrailingMultiplier { get; set; }

        [NinjaScriptProperty]
        public double EntryATRMultiplier { get; set; }

        [NinjaScriptProperty]
        public int StartHour { get; set; }

        [NinjaScriptProperty]
        public int StartMinute { get; set; }

        [NinjaScriptProperty]
        public int EndHour { get; set; }

        [NinjaScriptProperty]
        public int EndMinute { get; set; }

        [NinjaScriptProperty]
        public int Quantity { get; set; }

        #endregion
    }
}
