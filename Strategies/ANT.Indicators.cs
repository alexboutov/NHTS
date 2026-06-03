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
        #region Indicator Loading and Accessors
        
        // Force hosted-only indicator usage. Do NOT use chart-attached or native indicators.
        private void LoadNinZaIndicators()
        {
            // Force hosted equivalents as the only source
            useNativeAiq1 = false;
            useNativeAiqSB = false;

            useChartAiq1 = false;
            useChartRR = false;
            useChartDT = false;
            useChartVY = false;
            useChartET = false;
            useChartSW = false;
            useChartT3P = false;
            useChartAAA = false;
            useChartSB = false;

            useHostedT3Pro = true;
            useHostedVIDYAPro = true;
            useHostedEasyTrend = true;
            useHostedRubyRiver = true;
            useHostedDragonTrend = true;
            useHostedSolarWave = true;


            InitHostedCalculators();
            indicatorsReady = true;
            return;
        }
        
        private void LogDetectedIndicators()
        {
            LogAlways($"--- Indicators (HOSTED ONLY) ---");
            LogAlways($"  RubyRiver:    HOSTED");
            LogAlways($"  DragonTrend:  HOSTED");
            LogAlways($"  VIDYAPro:     HOSTED");
            LogAlways($"  EasyTrend:    HOSTED");
            LogAlways($"  SolarWave:    HOSTED");
            LogAlways($"  T3Pro:        HOSTED");
            LogAlways($"  AAATrendSync: HOSTED (AAATrendSyncEquivalent)");
            LogAlways($"  AIQ_1:        HOSTED (AIQ_1Equivalent)");
            LogAlways($"  AIQ_SuperBands: HOSTED (AIQ_SuperBandsEquivalent)");
            LogAlways($"--------------------------------");
        }
        private void LogAlways(string msg)
        {
            Print(msg);
            if (logWriter != null)
            {
                try 
                { 
                    // Use Time[0] for Market Replay time. 
                    // If the strategy is initializing and Time[0] isn't ready, fall back to system time.
                    string marketTimestamp = (CurrentBar >= 0) 
                        ? Time[0].ToString("yyyy-MM-dd HH:mm:ss") 
                        : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    logWriter.WriteLine($"{marketTimestamp} | {msg}"); 
                } 
                catch { }
            }
        }
/*        
        private void LogAlways(string msg)
        {
            Print(msg);
            if (logWriter != null)
                try { logWriter.WriteLine($"{DateTime.Now:HH:mm:ss} | {msg}"); } catch { }
        }
*/        
        // Helper methods for reflection-based indicator reading (not used for hosted-only, kept for parity)
        private bool GetBool(object o, FieldInfo f) { try { return o != null && f != null && (bool)f.GetValue(o); } catch { return false; } }
        private double GetDbl(object o, FieldInfo f) { try { return o != null && f != null ? (double)f.GetValue(o) : 0; } catch { return 0; } }
        private int GetInt(object o, FieldInfo f) { try { return o != null && f != null ? (int)f.GetValue(o) : 0; } catch { return 0; } }

        // Helper methods for chart-attached indicator reading (via PropertyInfo) - not used when hosted-only
        private bool GetChartBool(object o, PropertyInfo p) { try { return o != null && p != null && (bool)p.GetValue(o); } catch { return false; } }
        private double GetChartDbl(object o, PropertyInfo p) { try { return o != null && p != null ? (double)p.GetValue(o) : 0; } catch { return 0; } }
        private int GetChartInt(object o, PropertyInfo p) { try { return o != null && p != null ? (int)p.GetValue(o) : 0; } catch { return 0; } }

        // Indicator value accessors - HOSTED ONLY (chart/native detection intentionally disabled)
        [Browsable(false)] public bool RR_IsUp => _rrCalc != null ? _rrCalc.IsUptrend : false;
        [Browsable(false)] public bool VY_IsUp => _vidyaCalc != null ? _vidyaCalc.IsUptrend : false;
        [Browsable(false)] public bool ET_IsUp => _etCalc != null ? _etCalc.IsUptrend : false;
        [Browsable(false)] public double DT_Signal => _dtCalc != null ? _dtCalc.PrevSignal : 0;
        [Browsable(false)] public bool DT_IsUp => DT_Signal > 0;
        [Browsable(false)] public bool DT_IsDown => DT_Signal < 0;
        [Browsable(false)] public bool SW_IsUp => _swCalc != null ? _swCalc.IsUptrend : false;
        [Browsable(false)] public int SW_Count => _swCalc != null ? _swCalc.CountWave : 0;
        [Browsable(false)] public bool T3P_IsUp => _t3Calc != null ? _t3Calc.IsUptrend : false;
        [Browsable(false)] public bool AAA_IsUp => _aaaCalc != null ? _aaaCalc.IsUptrend : false;
        [Browsable(false)] public bool AAA_Available => _aaaCalc != null;
        [Browsable(false)] public bool SB_IsUp => _sbCalc != null ? _sbCalc.IsUptrend : false;
        [Browsable(false)] public bool SB_Available => _sbCalc != null;
        // AIQ_1 trigger indicator - HOSTED ONLY
        [Browsable(false)] public bool AIQ1_IsUp => _aiq1Calc != null ? _aiq1Calc.IsUptrend : false;


        #endregion
    }
}
