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
    public partial class NHTS
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

            // Clear any chart-attached references to be safe
            chartAiq1Equivalent = null;
            chartRubyRiverEquiv = null;
            chartDragonTrendEquiv = null;
            chartVidyaProEquiv = null;
            chartEasyTrendEquiv = null;
            chartSolarWaveEquiv = null;
            chartT3ProEquiv = null;
            chartAAATrendSyncEquiv = null;
            chartAiqSuperBandsEquiv = null;

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
            LogAlways($"  AAATrendSync: N/A (no hosted equivalent)");
            LogAlways($"  AIQ_1:        HOSTED (AIQ_1Equivalent)");
            LogAlways($"  AIQ_SuperBands: N/A (no hosted equivalent)");
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
        [Browsable(false)] public bool RR_IsUp => rubyRiverEquivalent?.IsUptrend ?? false;
        [Browsable(false)] public bool VY_IsUp => vidyaProEquivalent?.IsUptrend ?? false;
        [Browsable(false)] public bool ET_IsUp => easyTrendEquivalent?.IsUptrend ?? false;
        [Browsable(false)] public double DT_Signal => dragonTrendEquivalent?.PrevSignal ?? 0;
        [Browsable(false)] public bool DT_IsUp => DT_Signal > 0;
        [Browsable(false)] public bool DT_IsDown => DT_Signal < 0;
        [Browsable(false)] public bool SW_IsUp => solarWaveEquivalent?.IsUptrend ?? false;
        [Browsable(false)] public int SW_Count => solarWaveEquivalent?.CountWave ?? 0;
        [Browsable(false)] public bool T3P_IsUp => t3ProEquivalent?.IsUptrend ?? false;
        
        // AAATrendSync - no hosted equivalent; always false when forcing hosted-only deployment
        [Browsable(false)] public bool AAA_IsUp => false;
        [Browsable(false)] public bool AAA_Available => false;
        
        // AIQ_SuperBands - no hosted equivalent; always false when forcing hosted-only
        [Browsable(false)] public bool SB_IsUp 
        {
            get 
            {
                return false;
            }
        }
        [Browsable(false)] public bool SB_Available => false;
        
        // AIQ_1 trigger indicator - HOSTED ONLY
        [Browsable(false)] public bool AIQ1_IsUp 
        {
            get 
            {
                return aiq1Equivalent?.IsUptrend ?? false;
            }
        }

        // ── Hosted indicator factory methods ──────────────────────────────────────
        // These replicate the NT8 generated factory pattern inside the NHTS partial
        // class so CacheIndicator<T> (inherited from NinjaScriptBase) is in scope.

        private T3ProEquivalent[] _cacheT3Pro;
        private T3ProEquivalent T3ProEquivalent(
            T3ProMAType mAType, int period, int tCount, double vFactor,
            bool chaosSmoothingEnabled, T3ProMAType chaosSmoothingMethod, int chaosSmoothingPeriod,
            bool filterEnabled, double filterMultiplier, int filterATRPeriod,
            bool plotEnabled, bool markerEnabled,
            string markerStringUptrend, string markerStringDowntrend, int markerOffset)
        {
            var _ind = new T3ProEquivalent {
                    MAType = mAType, Period = period, TCount = tCount, VFactor = vFactor,
                    ChaosSmoothingEnabled = chaosSmoothingEnabled,
                    ChaosSmoothingMethod = chaosSmoothingMethod,
                    ChaosSmoothingPeriod = chaosSmoothingPeriod,
                    FilterEnabled = filterEnabled, FilterMultiplier = filterMultiplier,
                    FilterATRPeriod = filterATRPeriod, PlotEnabled = plotEnabled,
                    MarkerEnabled = markerEnabled,
                    MarkerStringUptrend = markerStringUptrend,
                    MarkerStringDowntrend = markerStringDowntrend,
                    MarkerOffset = markerOffset
            };

            return _ind;
        }

        private VIDYAProEquivalent[] _cacheVIDYA;
        private VIDYAProEquivalent VIDYAProEquivalent(
            int period, int volatilityPeriod, bool smoothingEnabled,
            VIDYAProMAType smoothingMethod, int smoothingPeriod,
            bool filterEnabled, double filterMultiplier, int aTRPeriod,
            bool showPlot, bool showMarkers,
            string uptrendMarker, string downtrendMarker, int markerOffset)
        {
            var _ind = new VIDYAProEquivalent {
                    Period = period, VolatilityPeriod = volatilityPeriod,
                    SmoothingEnabled = smoothingEnabled, SmoothingMethod = smoothingMethod,
                    SmoothingPeriod = smoothingPeriod, FilterEnabled = filterEnabled,
                    FilterMultiplier = filterMultiplier, ATRPeriod = aTRPeriod,
                    ShowPlot = showPlot, ShowMarkers = showMarkers,
                    UptrendMarker = uptrendMarker, DowntrendMarker = downtrendMarker,
                    MarkerOffset = markerOffset
            };

            return _ind;
        }

        private EasyTrendEquivalent[] _cacheEasyTrend;
        private EasyTrendEquivalent EasyTrendEquivalent(
            EasyTrendMAType mAType, int period, bool smoothingEnabled,
            EasyTrendMAType smoothingMethod, int smoothingPeriod,
            bool filterEnabled, bool filterAfterSmoothing, double filterMultiplier,
            EasyTrendFilterUnit filterUnit, int filterATRPeriod,
            bool showPlot, bool showMarkers,
            string uptrendMarker, string downtrendMarker, int markerOffset)
        {
            var _ind = new EasyTrendEquivalent {
                    MAType = mAType, Period = period, SmoothingEnabled = smoothingEnabled,
                    SmoothingMethod = smoothingMethod, SmoothingPeriod = smoothingPeriod,
                    FilterEnabled = filterEnabled, FilterAfterSmoothing = filterAfterSmoothing,
                    FilterMultiplier = filterMultiplier, FilterUnit = filterUnit,
                    FilterATRPeriod = filterATRPeriod, ShowPlot = showPlot,
                    ShowMarkers = showMarkers, UptrendMarker = uptrendMarker,
                    DowntrendMarker = downtrendMarker, MarkerOffset = markerOffset
            };

            return _ind;
        }

        private RubyRiverEquivalent[] _cacheRubyRiver;
        private RubyRiverEquivalent RubyRiverEquivalent(
            RubyRiverMAType mAType, int mAPeriod, bool mASmoothingEnabled,
            RubyRiverMAType mASmoothingMethod, int mASmoothingPeriod,
            double offsetMultiplier, int offsetPeriod,
            bool showPlot, bool showMarkers,
            string uptrendMarker, string downtrendMarker, int markerOffset)
        {
            var _ind = new RubyRiverEquivalent {
                    MAType = mAType, MAPeriod = mAPeriod,
                    MASmoothingEnabled = mASmoothingEnabled,
                    MASmoothingMethod = mASmoothingMethod,
                    MASmoothingPeriod = mASmoothingPeriod,
                    OffsetMultiplier = offsetMultiplier, OffsetPeriod = offsetPeriod,
                    ShowPlot = showPlot, ShowMarkers = showMarkers,
                    UptrendMarker = uptrendMarker, DowntrendMarker = downtrendMarker,
                    MarkerOffset = markerOffset
            };

            return _ind;
        }

        private DragonTrendEquivalent[] _cacheDragonTrend;
        private DragonTrendEquivalent DragonTrendEquivalent(
            int period, bool smoothingEnabled,
            DragonTrendMAType smoothingMethod, int smoothingPeriod,
            bool showMarkers, string uptrendMarker, string downtrendMarker, int markerOffset)
        {
            var _ind = new DragonTrendEquivalent {
                    Period = period, SmoothingEnabled = smoothingEnabled,
                    SmoothingMethod = smoothingMethod, SmoothingPeriod = smoothingPeriod,
                    ShowMarkers = showMarkers, UptrendMarker = uptrendMarker,
                    DowntrendMarker = downtrendMarker, MarkerOffset = markerOffset
            };

            return _ind;
        }

        private SolarWaveEquivalent[] _cacheSolarWave;
        private SolarWaveEquivalent SolarWaveEquivalent(
            int offsetATRPeriod, double offsetMultiplierTrend, double offsetMultiplierStop,
            int referencePricePeriod, int referencePriceCloseWeight,
            int slowdownScan, int weakWeakSplit, int pullbackSplit,
            bool showTrailingStop, bool showMarkers,
            string uptrendMarker, string downtrendMarker, int markerOffset)
        {
            var _ind = new SolarWaveEquivalent {
                    OffsetATRPeriod = offsetATRPeriod,
                    OffsetMultiplierTrend = offsetMultiplierTrend,
                    OffsetMultiplierStop = offsetMultiplierStop,
                    ReferencePricePeriod = referencePricePeriod,
                    ReferencePriceCloseWeight = referencePriceCloseWeight,
                    SlowdownScan = slowdownScan, WeakWeakSplit = weakWeakSplit,
                    PullbackSplit = pullbackSplit, ShowTrailingStop = showTrailingStop,
                    ShowMarkers = showMarkers, UptrendMarker = uptrendMarker,
                    DowntrendMarker = downtrendMarker, MarkerOffset = markerOffset
            };

            return _ind;
        }

        private AIQ_1Equivalent[] _cacheAIQ1;
        private AIQ_1Equivalent AIQ_1Equivalent(
            int period, int phase, AIQ1EquivMAMethod method,
            bool useBetterFormula, double pctAbove, double pctBelow,
            double sPctAbove, double sPctBelow,
            bool showSquares, int squareSize, int squareOpacity,
            bool showDots, int dotSize,
            Brush upSquareColor, Brush downSquareColor)
        {
            var _ind = new AIQ_1Equivalent {
                    Period = period, Phase = phase, Method = method,
                    UseBetterFormula = useBetterFormula,
                    PctAbove = pctAbove, PctBelow = pctBelow,
                    SPctAbove = sPctAbove, SPctBelow = sPctBelow,
                    ShowSquares = showSquares, SquareSize = squareSize,
                    SquareOpacity = squareOpacity, ShowDots = showDots,
                    DotSize = dotSize, UpSquareColor = upSquareColor,
                    DownSquareColor = downSquareColor
            };

            return _ind;
        }

        #endregion
    }
}

