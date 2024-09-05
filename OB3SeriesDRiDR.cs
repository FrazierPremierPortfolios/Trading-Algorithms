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
// using NinjaTrader.NinjaScript.Indicators.Gemify;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{

	public class OBR : Strategy
	{

//		public OBR()
//		{
//			VendorLicense("WarriorsEdgeEquityFund", "OB3SeriesDRiDR", "https://warriorsedgeequityfund.com/", "alexanderfrazier@warriorsedgeequity.com");
//		}
		
		#region Exit Conditions
		
		private bool ProfitTarget;
		private bool StopLoss;

		#endregion

		#region DR/iDR
		
		private List<double> highs = new List<double>();
		private List<double> lows = new List<double>();
		private List<double> closes = new List<double>();
		private bool calculationsDone = false;
		private double highestHigh;
		private double lowestLow;
		private double highestHighIDR;
		private double lowestLowIDR;
		private double drDirection; // 1 for upward, -1 for downward, 0 for flat
		
		#endregion

		#region Crossovers
		private bool up_trend;
		private bool down_trend;
		private bool cross_above;
		private bool cross_below;
		private bool price_above;
		private bool price_below;
		
		#endregion
			
		#region Reversal Pattern

		private bool bull_engulf_3;
		private bool bear_engulf_3;

		private bool bull_engulf_4;
		private bool bear_engulf_4;

		private bool dojiEnd;
		private bool dojiStart;
		
		#endregion
		
		
		#region stop Offset
		
		private double stopAreaLong;
		private double stopAreaShort;
		
		private double candleBarOffsetStop;
				
		private double stopLong;
		private double stopShort;

		private bool myFreeTradeLong;
		private bool myFreeTradeShort;
		
		#endregion
		
		
		#region Trade Limits
		
		private bool countOnce;
		private int currentCount;
		
		private double totalPnL;
		
		private double cumPnL;
		private double dailyPnL;
		
		private int priorTradesCount = 0;
		private double priorTradesCumProfit = 0;

		#endregion
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{



				Description									= @"OB3Series with DR/iDR";
				Name										= "OB3SeriesDRiDR";
				Calculate									= Calculate.OnEachTick;
				EntriesPerDirection							= 2;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 20;

				Start								= DateTime.Parse("02:30", System.Globalization.CultureInfo.InvariantCulture);
				End									= DateTime.Parse("16:00", System.Globalization.CultureInfo.InvariantCulture);

				// Moving Averages
				SlowMA										= 25;
				FastMA										= 20;

				// stop offsets
				TickOffsetStop								= 44;
				
				// daily trading limits
				DailyProfitLimit							= 10000;
				DailyLossLimit								= -1000;
				MaxDailyTrades								= 10;

				// position details
				PositionSize								= 2;
				ProfitTargetTicks							= 345;
				
				//Inlcude all trades in chart range
				IncludePreviousTrades						= false;

				threeCandleOnly								= false;
				fourCandleOnly								= false;
				threeFourCandle								= true;

			}
			else if (State == State.Configure)
			{
			}
			
			else if (State == State.DataLoaded)
			{
				ClearOutputWindow(); //Clears Output window every time strategy is enabled

			}
			
		}
		
		protected override void OnPositionUpdate(Cbi.Position position, double averagePrice, 
			int quantity, Cbi.MarketPosition marketPosition)
		{
			
			if (Position.Quantity == PositionSize)
			{
				currentCount++;
			}
			
			if (Position.MarketPosition == MarketPosition.Flat)
			{

			}

		}

		
		protected override void OnBarUpdate()
		{
			ProfitTarget = true;
			StopLoss = true;

			#region if Return	

			// Only trades real-time or backtesting based on IncludePreviousTrades and backTestMode settings
			if (!IncludePreviousTrades && !backTestMode && State != State.Realtime)
			{
			    return;
			}		
			
			if (CurrentBars[0] < 6) //Need more than 6 bars to trade
			{
				return;
			}
			    
			
			if (Bars.IsFirstBarOfSession && IsFirstTickOfBar)
			{
				currentCount 	= 0; ///Resets amount of trades you can take in a day
				
				ResetDailyCalculations();
				
				cumPnL 			= totalPnL; ///Double that copies the full session PnL (If trading multiple days). Is only calculated once per day.
				dailyPnL		= totalPnL - cumPnL; ///Subtract the copy of the full session by the full session PnL. This resets your daily PnL back to 0.
				
				
				priorTradesCount = SystemPerformance.AllTrades.Count;
				priorTradesCumProfit = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;

			}

			if ((SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit > DailyProfitLimit) || (SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit < DailyLossLimit) || (currentCount > MaxDailyTrades))
			{
				return;
			}
			
			if (Bars.BarsSinceNewTradingDay < 1 ) 
			{
				return;
			}

   			if (  (Time[0].TimeOfDay < Start.TimeOfDay) || (Time[0].TimeOfDay > End.TimeOfDay) ) //Trade is taking place within specified time
      			{
	 			return;
			}


			#endregion

			#region	DR/iDR calculations
		    // Reset calculations and lists at the start of each trading day
			if (UseDRiDR)
			{	
			    if (Bars.IsFirstBarOfSession && IsFirstTickOfBar)
			    {
			        Print("Resetting calculations for a new trading day: " + Time[0]);
			        highs.Clear();
			        lows.Clear();
			        closes.Clear();
			        calculationsDone = false;  // Ensure this is reset each day
			    }
			
			    // Proceed with your trading logic
			    TimeSpan currentTime = Time[0].TimeOfDay;
			
			    // Collect data between 9:30 and 10:30 AM
			    if (currentTime >= new TimeSpan(9, 30, 0) && currentTime < new TimeSpan(10, 30, 0))
			    {
			        Print("Collecting data between 9:30 and 10:30: " + Time[0]);
			        highs.Add(High[0]);
			        lows.Add(Low[0]);
			        closes.Add(Close[0]);
			    }
			    // Perform DR/iDR calculations after 10:30 AM if they haven't been done yet
			    else if (currentTime >= new TimeSpan(10, 30, 0) && !calculationsDone && highs.Count > 0 && lows.Count > 0 && closes.Count > 0)
			    {
			        Print("Performing DR/iDR calculations after 10:30: " + Time[0]);
			        highestHigh = highs.Max();
			        lowestLow = lows.Min();
			        highestHighIDR = closes.Max();
			        lowestLowIDR = closes.Min();
			
			        // Determine DR direction
			        double openingPrice = closes[closes.Count - highs.Count];
			        double closingPrice = closes.Last();
			        drDirection = openingPrice < closingPrice ? 1 : (openingPrice > closingPrice ? -1 : 0);
			
			        calculationsDone = true;  // Ensure calculations are done only once per day
			
			        // Print statements for debugging
			        Print("Highest High: " + highestHigh);
			        Print("Lowest Low: " + lowestLow);
			        Print("Highest High IDR: " + highestHighIDR);
			        Print("Lowest Low IDR: " + lowestLowIDR);
			
			        DrawLines();
			    }
			    else if (calculationsDone)
			    {
			        Print("Skipping calculation, already done for the day: " + Time[0]);
			    }
			}
			#endregion

			if (countOnce)
			{

				#region Stop Offset
				
				//Define what area you will set a stop based on the entry candle high/low
				stopAreaLong		= Low[0];
				stopAreaShort		= High[0];
				
				//Adds offset to your stop area. Gives user customization.
				candleBarOffsetStop = TickOffsetStop * TickSize;

				//Add both of them together to define final stop point
				stopLong = stopAreaLong - candleBarOffsetStop;
				stopShort = stopAreaShort + candleBarOffsetStop;
				#endregion

				
				#region Moving Averages
				if (UseSMA && countOnce)
				{
					cross_above = CrossAbove(SMA(FastMA), SMA(SlowMA), 1);
					cross_below = CrossBelow(SMA(FastMA), SMA(SlowMA), 1);
						
					up_trend = SMA(FastMA)[0] > SMA(SlowMA)[0];
					down_trend = SMA(FastMA)[0] < SMA(SlowMA)[0];
					if (Close[0] > SMA(SlowMA)[0])
					{
						price_above = true;
						price_below = false;
					}
					if (Close[0] < SMA(SlowMA)[0])
					{
						price_above = false;
						price_below = true;
					}
				}
				#endregion
				

				#region Reversal Pattern
				dojiStart = Open[4] == Close[4];
				dojiEnd = Open[0] == Close[0];


				bull_engulf_3 = 	Open[4] < Close[4]
								&& 	Open[3] > Close[3]
								&& 	Open[2] > Close[2]
								&& 	Open[1] > Close[1]
								&& 	((Open[0] < Close[0]) || dojiEnd);

				bear_engulf_3 = 	Open[4] > Close[4]
								&& 	Open[3] < Close[3]
								&& 	Open[2] < Close[2]
								&& 	Open[1] < Close[1]
								&& 	((Open[0] > Close[0]) || dojiEnd);

				dojiStart = Open[5] == Close[5];			
			
				bull_engulf_4 = ((Open[5] < Close[5]) || dojiStart)
								&&	Open[4] > Close[4]
								&& 	Open[3] > Close[3]
								&& 	Open[2] > Close[2]
								&& 	Open[1] > Close[1]
								&& 	((Open[0] < Close[0]) || dojiEnd);

				bear_engulf_4 = ((Open[5] > Close[5]) || dojiStart)   
								&&	Open[4] < Close[4]
								&&	Open[3] < Close[3]
								&& 	Open[2] < Close[2]
								&& 	Open[1] < Close[1]
								&& 	((Open[0] > Close[0]) || dojiEnd);
				#endregion


				countOnce = false;
			}

			if (IsFirstTickOfBar)
			{
				countOnce = true;
			}

			if (threeCandleOnly)
			{
				fourCandleOnly = false;
				threeFourCandle = false;
			}
			if (fourCandleOnly)
			{
				threeCandleOnly = false;
				threeFourCandle = false;
			}

			bool bullEngSignal = false;
			if (threeCandleOnly)
			{
				bullEngSignal = bull_engulf_3;
			}
			else if (fourCandleOnly)
			{
				bullEngSignal = bull_engulf_4;
			}
			else
			{
				if (bull_engulf_3 || bull_engulf_4)
				{
					bullEngSignal = true;	
				}
				else
				{
					bullEngSignal = false;
				}
			}

			bool bearEngSignal = false;
			if (threeCandleOnly)
			{
				bearEngSignal = bear_engulf_3;
			}
			else if (fourCandleOnly)
			{
				bearEngSignal = bear_engulf_4;
			}
			else
			{
				if (bear_engulf_3 || bear_engulf_4)
				{
					bearEngSignal = true;	
				}
				else
				{
					bearEngSignal = false;
				}
			}

			#region Long Trade

			// Ensure the trade is only taken after 10:30 AM and within the user-defined time range
			TimeSpan earliestTradeTime = new TimeSpan(10, 30, 0); // 10:30 AM
			TimeSpan startTime = Start.TimeOfDay > earliestTradeTime ? Start.TimeOfDay : earliestTradeTime; // Use manual time only if it's after 10:30 AM
						

			bool drConditionsMet = !UseDRiDR || (Close[0] <= highestHigh && Close[0] >= lowestLow && drDirection == 1);
			bool smaConditionsMet = !UseSMA || up_trend;
			
			if (bullEngSignal
			    && (Position.MarketPosition == MarketPosition.Flat)
			    && (currentCount < MaxDailyTrades)
			    && (BarsSinceExitExecution(0, "TX_Stop_Long", 0) > 1 || BarsSinceExitExecution(0, "TX_Stop_Long", 0) == -1)
			    && (BarsSinceExitExecution(0, "TX_Target_Long", 0) > 1 || BarsSinceExitExecution(0, "TX_Target_Long", 0) == -1)
			    && drConditionsMet // DR/iDR conditions are either met or bypassed
			    && smaConditionsMet // SMA conditions are either met or bypassed
			    && Time[0].TimeOfDay >= startTime // Check if after 10:30 AM or user-defined start time (whichever is later)
			    && Time[0].TimeOfDay <= End.TimeOfDay // Check if within the end time
			    )
			{
				EnterLong(PositionSize, "TX_Enter_Long");
				
				myFreeTradeLong = true;
			}

			if (Position.MarketPosition == MarketPosition.Long && myFreeTradeLong == true)
			{
				if (StopLoss)
				{
					ExitLongStopMarket(0, true, Position.Quantity, stopLong, "TX_Stop_Long", "TX_Enter_Long");
				}
				if (ProfitTarget)
				{
					ExitLongLimit(0,true,Position.Quantity,Position.AveragePrice + (TickSize*ProfitTargetTicks), "TX_Target_Long", "TX_Enter_Long");
				}
				myFreeTradeLong = false;
			}
			#endregion

			#region Short Trade

			bool drShortConditionsMet = !UseDRiDR || (Close[0] <= highestHigh && Close[0] >= lowestLow && drDirection == -1);
			bool smaShortConditionsMet = !UseSMA || down_trend;
			
			if (bearEngSignal
			    && (Position.MarketPosition == MarketPosition.Flat)
			    && (currentCount < MaxDailyTrades)
			    && (BarsSinceExitExecution(0, "TX_Stop_Short", 0) > 1 || BarsSinceExitExecution(0, "TX_Stop_Short", 0) == -1)
			    && (BarsSinceExitExecution(0, "TX_Target_Short", 0) > 1 || BarsSinceExitExecution(0, "TX_Target_Short", 0) == -1)
			    && drShortConditionsMet // DR/iDR conditions are either met or bypassed
			    && smaShortConditionsMet // SMA conditions are either met or bypassed
			    && Time[0].TimeOfDay >= startTime // Check if after 10:30 AM or user-defined start time (whichever is later)
			    && Time[0].TimeOfDay <= End.TimeOfDay // Check if within the end time
			    )
			{
				EnterShort(PositionSize, "TX_Enter_Short");
				myFreeTradeShort = true;
			}

			if (Position.MarketPosition == MarketPosition.Short && myFreeTradeShort == true)
			{
				if (StopLoss)
				{
					ExitShortStopMarket(0, true, Position.Quantity, stopShort, "TX_Stop_Short", "TX_Enter_Short");
				}
				if (ProfitTarget)
				{
					ExitShortLimit(0,true,Position.Quantity,Position.AveragePrice - (TickSize*ProfitTargetTicks), "TX_Target_Short", "TX_Enter_Short");
				}
				myFreeTradeShort = false;
			}

			#endregion


		}
		
		private void ResetDailyCalculations()
		{
		    Print("Resetting calculations for a new trading day: " + Time[0]);
		    highs.Clear();
		    lows.Clear();
		    closes.Clear();
		    calculationsDone = false;  // Ensure this is reset each day
		}
		
		private void DrawLines()
		{
		    // Create a unique identifier using the current time
		    string timeTag = Time[0].ToString("yyyyMMdd_HHmmss");
		
		    DateTime sessionStart = Time[0].Date.Add(new TimeSpan(9, 30, 0)); // Session start time
		    DateTime sessionEnd = Time[0].Date.Add(new TimeSpan(16, 0, 0)); // Session end time
		
		    // Draw DR lines with unique tags
		    Draw.Line(this, "highestHigh_" + timeTag, true, sessionStart, highestHigh, sessionEnd, highestHigh, Brushes.Green, DashStyleHelper.Solid, 2);
		    Draw.Line(this, "lowestLow_" + timeTag, true, sessionStart, lowestLow, sessionEnd, lowestLow, Brushes.Green, DashStyleHelper.Solid, 2);
		
		    // Draw IDR lines with unique tags
		    Draw.Line(this, "highestHighIDR_" + timeTag, true, sessionStart, highestHighIDR, sessionEnd, highestHighIDR, Brushes.Red, DashStyleHelper.Solid, 2);
		    Draw.Line(this, "lowestLowIDR_" + timeTag, true, sessionStart, lowestLowIDR, sessionEnd, lowestLowIDR, Brushes.Red, DashStyleHelper.Solid, 2);
		}

		protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
		{

		}

		protected override void OnOrderUpdate(Cbi.Order order, double limitPrice, double stopPrice, 
			int quantity, int filled, double averageFillPrice, 
			Cbi.OrderState orderState, DateTime time, Cbi.ErrorCode error, string comment)
		{
			
		}
		
		protected override void OnExecutionUpdate(Cbi.Execution execution, string executionId, double price, int quantity, 
			Cbi.MarketPosition marketPosition, string orderId, DateTime time)
		{
			
		}


		#region Properties

		#region 1. Position
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Position Size", Order=1, GroupName="1. Contracts")]
		public int PositionSize
		{ get; set; }

		#endregion

		#region 1. ExitConditions
		[NinjaScriptProperty]
		[Display(Name="Profit Target (Ticks)", Order=2, GroupName="2. Exit Conditions")]
		public int ProfitTargetTicks
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="Stop Loss (Ticks)", Order=3, GroupName="2. Exit Conditions")]
		public int TickOffsetStop
		{ get; set; }
		
		#endregion

		#region 2. EntryConditions

		[NinjaScriptProperty]
		[Display(Name="3 Candle Drive Only", Order=3, GroupName="3. Entry Conditions")]
		public bool threeCandleOnly
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="4 Candle Drive Only", Order=4, GroupName="3. Entry Conditions")]
		public bool fourCandleOnly
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="3 or 4 candle drives", Order=5, GroupName="3. Entry Conditions")]
		public bool threeFourCandle
		{ get; set; }
		
		#endregion

		#region Trading Variables 
		[NinjaScriptProperty]
		[Display(Name = "Use SMA", Order = 0, GroupName = "SMA Settings")]
		public bool UseSMA { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Use DR/iDR", Order = 1, GroupName = "DR/iDR Settings")]
		public bool UseDRiDR { get; set; }
		#endregion
		
		#region 3. Filters - SMA
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Fast MA Length", Order=1, GroupName="4. Filters")]
		public int FastMA
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Slow MA Length", Order=2, GroupName="4. Filters")]
		public int SlowMA
		{ get; set; }
		#endregion

		#region 4. Order Limits
			
		[NinjaScriptProperty]
		[Display(Name="Daily Profit Limit", Order=1, GroupName="5. Order Limits")]
		public double DailyProfitLimit
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="Daily Loss Limit", Order=2, GroupName="5. Order Limits")]
		public double DailyLossLimit
		{ get; set; }
	
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Max Trade Count", Description="Max number of trades the bot will enter in a day", Order=3, GroupName="5. Order Limits")]
		public int MaxDailyTrades
		{ get; set; }

  		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Start Time", Order=4, GroupName="5. Order Limits")]
		public DateTime Start
		{ get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="End Time", Order=5, GroupName="5. Order Limits")]
		public DateTime End
		{ get; set; }
	
		#endregion
		
		#region 8. Debugging
		
		[NinjaScriptProperty]
		[Display(Name="Include All Trades", Order=1, GroupName="6. Debug")]
		public bool IncludePreviousTrades
		{ get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Backtest mode", Description = "Run in backtesting mode by offsetting N days", Order = 100, GroupName = "6. Debug")]
        public bool backTestMode
        { get; set; }
		

		[NinjaScriptProperty]
        [Display(Name = "Offset days", Description = "Go back this many days from today for backtesting", Order = 200, GroupName = "6. Debug")]
        public int offsetDays
        { get; set; }
		
		#endregion



		#endregion
	}
	
}
 
