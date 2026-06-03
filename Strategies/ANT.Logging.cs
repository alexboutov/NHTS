#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class ANT
    {
        #region Trade Tracking Fields
        // Entry tracking for detailed exit logging
        private string tradeEntryDirection = "";
        private double tradeEntryPrice = 0;
        private DateTime tradeEntryTime = DateTime.MinValue;
        private string lastExitReason = "";
        #endregion
        
        #region CSV Indicator Logging
        private void InitializeCSVLog()
        {
            if (!EnableIndicatorCSVLog) return;
            try
            {
                string dir = System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "log");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                csvLogFilePath = System.IO.Path.Combine(dir, $"IndicatorValues_{DateTime.Now:yyyy-MM-dd}_{chartSessionId}.csv");
                csvWriter = new StreamWriter(csvLogFilePath, false) { AutoFlush = true };
                // Write CSV header - includes AAA_IsUp
                csvWriter.WriteLine("BarTime,Close,AIQ1_IsUp,RR_IsUp,DT_Signal,VY_IsUp,ET_IsUp,SW_IsUp,SW_Count,T3P_IsUp,AAA_IsUp,SB_IsUp,BullConf,BearConf,Source");
                LogAlways($"\U0001F4CA CSV Log: {csvLogFilePath}");
            }
            catch (Exception ex) { Print($"CSV Init Error: {ex.Message}"); }
        }
        
        private void WriteCSVRow(DateTime barTime)
        {
            if (csvWriter == null || !EnableIndicatorCSVLog) return;
            try
            {
                var (bull, bear, total) = GetConfluence();
                string source = GetIndicatorSourceSummary();
                // Include AAA_IsUp in CSV output - barTime includes full date
                csvWriter.WriteLine($"{barTime:yyyy-MM-dd HH:mm:ss},{Close[0]:F2},{B2I(AIQ1_IsUp)},{B2I(RR_IsUp)},{DT_Signal:F2},{B2I(VY_IsUp)},{B2I(ET_IsUp)},{B2I(SW_IsUp)},{SW_Count},{B2I(T3P_IsUp)},{B2I(AAA_IsUp)},{B2I(SB_IsUp)},{bull},{bear},{source}");
            }
            catch { }
        }
        
        private int B2I(bool b) => b ? 1 : 0;
        
        private string GetIndicatorSourceSummary()
        {
            // Returns a short code indicating indicator sources: N=ninZa, C=Chart, H=Hosted, -=N/A
            string aiq = useNativeAiq1 ? "N" : (useChartAiq1 ? "C" : "H");
            string rr = useChartRR ? "C" : "H";
            string dt = useChartDT ? "C" : "H";
            string vy = useChartVY ? "C" : "H";
            string et = useChartET ? "C" : "H";
            string sw = useChartSW ? "C" : "H";
            string t3 = useChartT3P ? "C" : "H";
            string aaa = useChartAAA ? "C" : "H";
            string sb = useNativeAiqSB ? "N" : (useChartSB ? "C" : "H");
            return $"AIQ:{aiq}|RR:{rr}|DT:{dt}|VY:{vy}|ET:{et}|SW:{sw}|T3:{t3}|AAA:{aaa}|SB:{sb}";
        }
        
        private void CloseCSVLog()
        {
            try { csvWriter?.Close(); } catch { }
        }
        #endregion
        
        #region Price Helpers
        private new double GetCurrentAsk()
        {
            if (BarsInProgress == 0 && GetCurrentAsk(0) > 0)
                return GetCurrentAsk(0);
            return Close[0];
        }
        
        private new double GetCurrentBid()
        {
            if (BarsInProgress == 0 && GetCurrentBid(0) > 0)
                return GetCurrentBid(0);
            return Close[0];
        }
        
        // Call this when entering a trade to track details for exit logging
        private void SetTradeEntry(string direction, double price, DateTime time)
        {
            tradeEntryDirection = direction;
            tradeEntryPrice = price;
            tradeEntryTime = time;
            lastExitReason = "";
        }
        
        // Call this to set exit reason before exit is executed
        private void SetExitReason(string reason)
        {
            lastExitReason = reason;
        }
        #endregion
        
        #region Signal Logging
        private void LogSignal(string dir, string trigger, DateTime t, int confluenceCount, int total)
        {
            double askPrice = GetCurrentAsk();
            double bidPrice = GetCurrentBid();
            double pointValue = Instrument.MasterInstrument.PointValue;
            double stopPoints = pointValue > 0 ? StopLossUSD / pointValue : 0;
            double tpPoints = pointValue > 0 ? TakeProfitUSD / pointValue : 0;
            
            double entryPriceLog, stopPrice, tpPrice;
            int barsAfterSquare;
            
            if (dir == "LONG")
            {
                entryPriceLog = askPrice;
                stopPrice = askPrice - stopPoints;
                tpPrice = askPrice + tpPoints;
                barsAfterSquare = barsSinceYellowSquare;
            }
            else
            {
                entryPriceLog = bidPrice;
                stopPrice = bidPrice + stopPoints;
                tpPrice = bidPrice - tpPoints;
                barsAfterSquare = barsSinceOrangeSquare;
            }
            
            string instrumentName = Instrument.FullName;
            string squareType = dir == "LONG" ? "Yellow\U0001F7E8" : "Orange\U0001F7E7";
            
            PrintAndLog($"", t);
            PrintAndLog($"\u2554\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2557", t);
            // FIXED: Include full date in signal timestamp for Market Replay analysis
            PrintAndLog($"\u2551  *** {dir} SIGNAL @ {t:yyyy-MM-dd HH:mm:ss} ***", t);
            PrintAndLog($"\u2560\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2563", t);
            PrintAndLog($"\u2551  Instrument: {instrumentName}", t);
            PrintAndLog($"\u2551  Ask: {askPrice:F2}    Bid: {bidPrice:F2}", t);
            PrintAndLog($"\u2551  STOP: {stopPrice:F2}  (${StopLossUSD:F0} = {stopPoints:F2} pts)", t);
            PrintAndLog($"\u2551  TP:   {tpPrice:F2}  (${TakeProfitUSD:F0} = {tpPoints:F2} pts)", t);
            PrintAndLog($"\u2560\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2563", t);
            PrintAndLog($"\u2551  Trigger: {trigger}", t);
            PrintAndLog($"\u2551  Confluence: {confluenceCount}/{total}", t);
            PrintAndLog($"\u2551  RR={Ts(RR_IsUp)} DT={DT_Signal:F0} VY={Ts(VY_IsUp)} ET={Ts(ET_IsUp)} SW={SW_Count} T3P={Ts(T3P_IsUp)} AAA={Ts(AAA_IsUp)} SB={Ts(SB_IsUp)}", t);
            PrintAndLog($"\u2551  AIQ1={Ts(AIQ1_IsUp)} | Bars after {squareType}: {barsAfterSquare}", t);
            PrintAndLog($"\u255A\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u255D", t);
        }
        
        private string Ts(bool up) => up ? "UP" : "DN";
        #endregion
        
        #region Logging
        private void InitializeLogFile()
        {
            try
            {
                string dir = System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "log");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                logFilePath = System.IO.Path.Combine(dir, $"ANT_{DateTime.Now:yyyy-MM-dd}_{chartSessionId}.txt");
                logWriter = new StreamWriter(logFilePath, true, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
                logWriter.WriteLine($"\n=== ANT Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                if (UniRenkoMode)
                {
                    logWriter.WriteLine($"    *** UNIRENKO MODE ***");
                    logWriter.WriteLine($"    Cooldown: {(UseTimeBasedCooldown ? $"{CooldownSeconds} seconds (time-based)" : $"{CooldownBars} bars")}");
                }
                logWriter.WriteLine($"    8-indicator confluence filter");
                logWriter.WriteLine($"    Signal Filter: MinConf={MinConfluenceRequired}/8, MaxBars={MaxBarsAfterYellowSquare}, Cooldown={CooldownBars}");
                logWriter.WriteLine($"    Auto Trade: {(EnableAutoTrading ? "ON" : "OFF")} | MinConf for Trade={MinConfluenceForAutoTrade}/8");
                logWriter.WriteLine($"    Risk: SL=${StopLossUSD:F0}, TP=${TakeProfitUSD:F0}");
                if (EnableDailyLossLimit)
                    logWriter.WriteLine($"    Daily Loss Limit: ${DailyLossLimitUSD:F0}");
                if (UseTradingHoursFilter)
                    logWriter.WriteLine($"    Trading Hours: {GetTradingHoursString()}");
                else
                    logWriter.WriteLine($"    Trading Hours: ALL (filter disabled)");
                if (CloseBeforeNews)
                    logWriter.WriteLine($"    Auto-Close Before News: {NewsCloseHour:D2}:{NewsCloseMinute:D2}");
                if (CloseAtEndOfDay)
                    logWriter.WriteLine($"    Auto-Close EOD: {EODCloseHour:D2}:{EODCloseMinute:D2}");
                logWriter.WriteLine($"    LONG:  Yellow\U0001F7E8 (AIQ1 UP) \u2192 Any indicator confirms \u2192 Bull Confluence \u2265 {MinConfluenceRequired}");
                logWriter.WriteLine($"    SHORT: Orange\U0001F7E7 (AIQ1 DN) \u2192 Any indicator confirms \u2192 Bear Confluence \u2265 {MinConfluenceRequired}\n");
            }
            catch { }
        }
        
        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution.Order != null && execution.Order.OrderState == OrderState.Filled)
            {
                string orderName = execution.Order.Name ?? "";
                OrderAction action = execution.Order.OrderAction;
                
                // Detect ENTRY fills
                bool isEntry = (action == OrderAction.Buy || action == OrderAction.SellShort) && 
                               (orderName == "Long" || orderName == "Short" || orderName.Contains("Entry"));
                
                if (isEntry)
                {
                    string dir = (action == OrderAction.Buy) ? "LONG" : "SHORT";
                    
                    // Calculate entry slippage (positive = unfavorable)
                    double entrySlippageTicks = 0;
                    double entrySlippageDollars = 0;
                    if (signalPriceAtEntry > 0 && TickSize > 0)
                    {
                        if (dir == "LONG")
                            entrySlippageTicks = (price - signalPriceAtEntry) / TickSize;
                        else
                            entrySlippageTicks = (signalPriceAtEntry - price) / TickSize;
                        
                        entrySlippageDollars = entrySlippageTicks * TickSize * Instrument.MasterInstrument.PointValue;
                    }
                    
                    SetTradeEntry(dir, price, time);
                    
                    string slipStr = entrySlippageTicks >= 0 ? $"+{entrySlippageTicks:F0}t" : $"{entrySlippageTicks:F0}t";
                    string slipDollarStr = entrySlippageDollars >= 0 ? $"${entrySlippageDollars:F2}" : $"-${Math.Abs(entrySlippageDollars):F2}";
                    PrintAndLog($">>> ENTRY FILLED: {dir} @ {price:F2} | Signal={signalPriceAtEntry:F2} | Slippage: {slipStr} ({slipDollarStr}) | {time:yyyy-MM-dd HH:mm:ss}", time);
                }
                
                // Detect EXIT fills - check order action and if we have an active entry tracked
                // Don't rely on Position.MarketPosition as it may not be updated yet
                bool isExitAction = (action == OrderAction.Sell || action == OrderAction.BuyToCover);
                bool hasActiveEntry = !string.IsNullOrEmpty(tradeEntryDirection);
                string orderNameLower = orderName.ToLower();
                bool isStopOrTarget = orderNameLower.Contains("stop") || orderNameLower.Contains("profit") || orderNameLower.Contains("exit") || orderNameLower.Contains("target");
                
                if (isExitAction && hasActiveEntry && isStopOrTarget)
                {
                    try
                    {
                        if (SystemPerformance.AllTrades.Count == 0) return;
                        var lastTrade = SystemPerformance.AllTrades[SystemPerformance.AllTrades.Count - 1];
                        double tradePnL = lastTrade.ProfitCurrency;
                        double exitPrice = price;
                    
                    // Determine exit reason from order name
                    string exitReason = "UNKNOWN";
                    if (!string.IsNullOrEmpty(lastExitReason))
                        exitReason = lastExitReason;
                    else if (orderName.Contains("Stop"))
                        exitReason = simpleTrailActive ? "TRAIL" : "SL";
                    else if (orderName.Contains("Profit"))
                        exitReason = "TP";
                    else if (orderName.Contains("Exit"))
                        exitReason = orderName.Replace(" ", "_");
                    else if (orderName.Contains("MaxProfit"))
                        exitReason = "MAX_PROFIT";
                    
                    // Calculate ticks P&L
                    double ticksPnL = 0;
                    if (tradeEntryPrice > 0 && TickSize > 0)
                    {
                        if (tradeEntryDirection == "LONG")
                            ticksPnL = (exitPrice - tradeEntryPrice) / TickSize;
                        else if (tradeEntryDirection == "SHORT")
                            ticksPnL = (tradeEntryPrice - exitPrice) / TickSize;
                    }
                    
                    // Calculate exit slippage (positive = unfavorable)
                    double exitSlippageTicks = 0;
                    if (TickSize > 0)
                    {
                        if (exitReason == "TP" && expectedTargetPrice > 0)
                        {
                            // TP is a limit order - should have 0 or favorable slippage
                            if (tradeEntryDirection == "LONG")
                                exitSlippageTicks = (expectedTargetPrice - exitPrice) / TickSize;
                            else
                                exitSlippageTicks = (exitPrice - expectedTargetPrice) / TickSize;
                        }
                        else if ((exitReason == "SL" || exitReason == "TRAIL") && expectedStopPrice > 0)
                        {
                            // Stop orders can have slippage
                            if (tradeEntryDirection == "LONG")
                                exitSlippageTicks = (expectedStopPrice - exitPrice) / TickSize;
                            else
                                exitSlippageTicks = (exitPrice - expectedStopPrice) / TickSize;
                        }
                    }
                    
                    dailyPnL += tradePnL;
                    dailyTradeCount++;
                    
                    string pnlIcon = tradePnL >= 0 ? "\u2705" : "\u274C";
                    string ticksStr = ticksPnL >= 0 ? $"+{ticksPnL:F0}t" : $"{ticksPnL:F0}t";
                    string exitSlipStr = exitSlippageTicks >= 0 ? $"+{exitSlippageTicks:F0}t" : $"{exitSlippageTicks:F0}t";
                    
                    // Comprehensive trade closed log line for analysis script - includes exit slippage
                    PrintAndLog($">>> {pnlIcon} TRADE CLOSED: {tradeEntryDirection} | Entry={tradeEntryPrice:F2} Exit={exitPrice:F2} | {ticksStr} ${tradePnL:F2} | Reason: {exitReason} | Exit Slip: {exitSlipStr}", time);
                    PrintAndLog($"   Daily P&L: ${dailyPnL:F2} ({dailyTradeCount} trades) | Entry Time: {tradeEntryTime:yyyy-MM-dd HH:mm:ss}", time);
                    
                    // Reset entry tracking
                    tradeEntryDirection = "";
                    tradeEntryPrice = 0;
                    tradeEntryTime = DateTime.MinValue;
                    lastExitReason = "";
                    
                    if (EnableSoundAlert)
                        try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
                    
                    if (EnableDailyLossLimit && dailyPnL <= -DailyLossLimitUSD)
                    {
                        dailyLossLimitHit = true;
                        PrintAndLog($"\U0001F6D1 DAILY LOSS LIMIT HIT: ${dailyPnL:F2} exceeds -${DailyLossLimitUSD:F2} limit. Trading stopped for today.", time);
                        if (EnableSoundAlert)
                            try { System.Media.SystemSounds.Hand.Play(); } catch { }
                    }
                    
                    if (EnableDailyProfitTarget && dailyPnL >= DailyProfitTargetUSD)
                    {
                        dailyProfitTargetHit = true;
                        PrintAndLog($"\U0001F3AF DAILY PROFIT TARGET HIT: ${dailyPnL:F2} reached ${DailyProfitTargetUSD:F2} target. Trading stopped for today.", time);
                        if (EnableSoundAlert)
                            try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
                    }
                    }
                    catch (InvalidOperationException)
                    {
                        // Collection was modified during enumeration - skip this update
                    }
                }
            }
        }
        
        private void CloseLogFile()
        {
            try
            {
                logWriter?.WriteLine($"\n=== Session Ended: {DateTime.Now:HH:mm:ss} | Signals: {signalCount} ===");
                logWriter?.Close();
            }
            catch { }
        }
        
        private void PrintAndLog(string msg, DateTime? barTime = null)
        {
            Print(msg);
            if (logWriter != null)
            {
                DateTime ts = barTime ?? DateTime.Now;
                try { logWriter.WriteLine($"{ts:yyyy-MM-dd HH:mm:ss} | {msg}"); } catch { }
            }
        }
        #endregion
    }
}
