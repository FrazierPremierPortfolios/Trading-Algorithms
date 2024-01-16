#region Using declarations
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
using Brush = System.Windows.Media.Brush;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class TEMAWithCrossover : Strategy
    {
        // Define strategy variables
        private double entryPriceLong = 0.0;
        private double entryPriceShort = 0.0;
        private double stopLossPriceLong = 0.0;
        private double stopLossPriceShort = 0.0;
        private bool trailingStopActivatedLong = false;
        private bool trailingStopActivatedShort = false;
        private double trailAmount = 10; // Adjust this value based on your preference

        // Define EMA lengths
        private int shortEMAPeriod = 10;
        private int longEMAPeriod = 30;

        // EMA indicators
        private EMA shortEMA;
        private EMA longEMA;

        // Calculate TEMA values
        private double currentTEMAValue;
        private double previousTEMAValue;

        // Initialize method
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "TEMA Trailing Stop with EMA Crossover";
                Calculate = MarketDataType.Last;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;
                IsInstantiatedOnEachOptimizationIteration = true;

                // Initialize EMAs
                shortEMA = EMA(Close, shortEMAPeriod);
                longEMA = EMA(Close, longEMAPeriod);
            }
        }

        // OnBarUpdate method
        protected override void OnBarUpdate()
        {
            // Calculate TEMA values
            currentTEMAValue = TEMA(Close, 14)[0];
            previousTEMAValue = TEMA(Close, 14)[1];

            // EMA crossover conditions
            bool emaCrossoverLong = CrossAbove(shortEMA, longEMA);
            bool emaCrossoverShort = CrossBelow(shortEMA, longEMA);

            // Long entry condition
            if (emaCrossoverLong)
            {
                entryPriceLong = Close[0];
                trailingStopActivatedLong = false;
                trailingStopActivatedShort = false; // Reset short trailing stop if switching from short to long
                SetStopLoss(CalculationMode.Price, entryPriceLong - (trailAmount * TickSize), "Long_Trade_Stop");
                EnterLong();
            }

            // Short entry condition
            else if (emaCrossoverShort)
            {
                entryPriceShort = Close[0];
                trailingStopActivatedLong = false; // Reset long trailing stop if switching from long to short
                trailingStopActivatedShort = false;
                SetStopLoss(CalculationMode.Price, entryPriceShort + (trailAmount * TickSize), "Short_Trade_Stop");
                EnterShort();
            }

            // Long trailing stop logic
            if (Position.MarketPosition == MarketPosition.Long)
            {
                if (!trailingStopActivatedLong && Close[0] > entryPriceLong)
                {
                    // Activate trailing stop once the trade is in profit
                    stopLossPriceLong = entryPriceLong - (trailAmount * TickSize);
                    trailingStopActivatedLong = true;
                }

                // Check if the current candle has closed above the TEMA
                if (trailingStopActivatedLong && currentTEMAValue > previousTEMAValue && IsCandleClosed(0))
                {
                    // Update trailing stop as price moves in the direction
                    stopLossPriceLong = Math.Max(stopLossPriceLong, entryPriceLong - (trailAmount * TickSize));
                    SetStopLoss(CalculationMode.Price, stopLossPriceLong);
                }

                // Additional conditions for Long position can be added here
                // Example: if (someCondition) { /* additional logic */ }
            }

            // Short trailing stop logic
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                if (!trailingStopActivatedShort && Close[0] < entryPriceShort)
                {
                    // Activate trailing stop once the trade is in profit
                    stopLossPriceShort = entryPriceShort + (trailAmount * TickSize);
                    trailingStopActivatedShort = true;
                }

                // Check if the current candle has closed below the TEMA
                if (trailingStopActivatedShort && currentTEMAValue < previousTEMAValue && IsCandleClosed(0))
                {
                    // Update trailing stop as price moves in the direction
                    stopLossPriceShort = Math.Min(stopLossPriceShort, entryPriceShort + (trailAmount * TickSize));
                    SetStopLoss(CalculationMode.Price, stopLossPriceShort);
                }

                // Additional conditions for Short position can be added here
                // Example: if (someCondition) { /* additional logic */ }
            }
        }

        // Custom method to check if the candle is closed
        private bool IsCandleClosed(int barIndex)
        {
            // Implement your logic to check if the candle is closed
            // You may use conditions based on Open, High, Low, and Close prices
            // Example: return Close[barIndex] > Open[barIndex];
            return true; // Modify this based on your requirements
        }
    }
}
