// ANT.HostedIndicators.cs
// Pure C# calculator classes for hosted indicator fallback mode.
// These replicate the calculation logic of the *Equivalent NT8 indicators
// without any NT8 framework dependencies (no Series<>, no AddChartIndicator,
// no OnBarUpdate inheritance). ANT calls Update() on each instance from
// its own OnBarUpdate when the hosted fallback path is active.
//
// Usage in ANT.OnBarUpdate (after indicatorsReady check):
//   if (useHostedT3Pro)        _t3Calc.Update(CurrentBar, Open[0], High[0], Low[0], Close[0], TickSize);
//   if (useHostedVIDYAPro)     _vidyaCalc.Update(CurrentBar, High[0], Low[0], Close[0]);
//   ... etc.
//
// Output properties match the originals:
//   _t3Calc.IsUptrend, _vidyaCalc.IsUptrend, _rrCalc.IsUptrend,
//   _dtCalc.IsUptrend, _dtCalc.PrevSignal, _etCalc.IsUptrend,
//   _swCalc.IsUptrend, _swCalc.CountWave, _aiq1Calc.IsUptrend,
//   _aaaCalc.IsUptrend, _sbCalc.IsUptrend

using System;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class ANT
    {
        // â”€â”€ Hosted calculator instances â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private T3ProCalc        _t3Calc;
        private VIDYAProCalc     _vidyaCalc;
        private EasyTrendCalc    _etCalc;
        private RubyRiverCalc    _rrCalc;
        private DragonTrendCalc  _dtCalc;
        private SolarWaveCalc    _swCalc;
        private AIQ1Calc         _aiq1Calc;
        private AAACalc          _aaaCalc;
        private SuperBandsCalc   _sbCalc;
        private ATRCalc          _atrCalc;        // dynamic-exit ATR (period 14)
        private ATRCalc          _atrTrailCalc;   // trailing-stop ATR (ATRTrailPeriod)

        // â”€â”€ Initialise with same defaults used in ANT.cs State.Configure â”€â”€â”€â”€
        private void InitHostedCalculators()
        {
            _t3Calc = new T3ProCalc(
                period: T3ProPeriod, tCount: T3ProTCount, vFactor: T3ProVFactor,
                chaosSmoothingEnabled: T3ProChaosSmoothingEnabled,
                chaosSmoothingPeriod: T3ProChaosSmoothingPeriod,
                filterEnabled: T3ProFilterEnabled,
                filterMultiplier: T3ProFilterMultiplier,
                filterATRPeriod: 14);

            _vidyaCalc = new VIDYAProCalc(
                period: VIDYAPeriod,
                volatilityPeriod: VIDYAVolatilityPeriod,
                smoothingEnabled: VIDYASmoothingEnabled,
                smoothingPeriod: VIDYASmoothingPeriod,
                filterEnabled: VIDYAFilterEnabled,
                filterMultiplier: VIDYAFilterMultiplier,
                atrPeriod: 14);

            _etCalc = new EasyTrendCalc(
                period: EasyTrendPeriod,
                smoothingEnabled: EasyTrendSmoothingEnabled,
                smoothingPeriod: EasyTrendSmoothingPeriod,
                filterEnabled: EasyTrendFilterEnabled,
                filterMultiplier: EasyTrendFilterMultiplier,
                filterATRPeriod: EasyTrendATRPeriod);

            _rrCalc = new RubyRiverCalc(
                maPeriod: RubyRiverMAPeriod,
                smoothingEnabled: RubyRiverSmoothingEnabled,
                smoothingPeriod: RubyRiverSmoothingPeriod,
                offsetMultiplier: RubyRiverOffsetMultiplier,
                offsetPeriod: RubyRiverOffsetPeriod);

            _dtCalc = new DragonTrendCalc(
                period: DragonTrendPeriod,
                smoothingEnabled: DragonTrendSmoothingEnabled,
                smoothingPeriod: DragonTrendSmoothingPeriod);

            _swCalc = new SolarWaveCalc(
                atrPeriod: SolarWaveATRPeriod,
                trendMultiplier: SolarWaveTrendMultiplier,
                stopMultiplier: SolarWaveStopMultiplier,
                refPricePeriod: 2,
                refPriceCloseWeight: 1);

            _aiq1Calc = new AIQ1Calc(period: 3, useBetterFormula: true,
                sPctAbove: 0.03, sPctBelow: 0.03);

            _aaaCalc = new AAACalc(
                fastPeriod: 10, fastSmoothingEnabled: true, fastSmoothingPeriod: 2,
                midPeriod: 20,  midSmoothingEnabled: true,  midSmoothingPeriod: 2,
                slowPeriod: 30, slowSmoothingEnabled: true, slowSmoothingPeriod: 5,
                minSpreadEnabled: true, minSpreadATRMultiplier: 0.05, minSpreadATRPeriod: 100);

            _sbCalc = new SuperBandsCalc(halfLengthMain: 101, halfLengthFast: 11);

            _atrCalc      = new ATRCalc(14);
            _atrTrailCalc = new ATRCalc(ATRTrailPeriod);
        }

        // â”€â”€ Call from ANT.OnBarUpdate for hosted fallback path â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private void UpdateHostedCalculators()
        {
            double o = Open[0], h = High[0], l = Low[0], c = Close[0];
            double prevC = CurrentBar > 0 ? Close[1] : c;
            int bar = CurrentBar;

            if (useHostedT3Pro)       _t3Calc.Update(bar, h, l, c, prevC);
            if (useHostedVIDYAPro)    _vidyaCalc.Update(bar, h, l, c, prevC);
            if (useHostedEasyTrend)   _etCalc.Update(bar, h, l, c, prevC);
            if (useHostedRubyRiver)   _rrCalc.Update(bar, h, l, c, prevC);
            if (useHostedDragonTrend) _dtCalc.Update(bar, c);
            if (useHostedSolarWave)   _swCalc.Update(bar, h, l, c, prevC);
            // AIQ1, AAA, SB always updated (they are always hosted when not chart-attached)
            _aiq1Calc.Update(bar, o, h, l, c);
            _aaaCalc.Update(bar, h, l, c);
            _sbCalc.Update(bar, c);
            // ATR calculators (always updated; no useHosted flag)
            _atrCalc.Update(bar, h, l, c, prevC);
            _atrTrailCalc.Update(bar, h, l, c, prevC);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // ATR Calculator (NT8-exact) â€” Option B hosted, no NT8 indicator object
        //   bar 0:    Value = High - Low
        //   bar n>0:  Value = ((min(bar+1,P) - 1) * prevValue + TR) / min(bar+1,P)
        //   TR = max(H-L, |H-prevC|, |L-prevC|)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private class ATRCalc
        {
            private readonly int _period;
            private double _atr;
            private bool _seeded;

            public ATRCalc(int period) { _period = Math.Max(1, period); }

            public double Value { get; private set; }
            public bool IsReady => _seeded;

            // bar = CurrentBar; prevC = Close[1] (== c on bar 0)
            public void Update(int bar, double h, double l, double c, double prevC)
            {
                if (!_seeded || bar == 0)
                {
                    _atr = h - l;                 // NT8 CurrentBar==0 seed
                    _seeded = true;
                    Value = _atr;
                    return;
                }
                double tr = Math.Max(h - l, Math.Max(Math.Abs(h - prevC), Math.Abs(l - prevC)));
                int n = Math.Min(bar + 1, _period);   // NT8 min(CurrentBar+1, Period)
                _atr = ((n - 1) * _atr + tr) / n;
                Value = _atr;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // T3 Pro Calculator
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private class T3ProCalc
        {
            private readonly int _period, _tCount, _chaosPeriod, _filterATRPeriod;
            private readonly double _vFactor, _filterMultiplier;
            private readonly bool _chaosEnabled, _filterEnabled;
            private readonly double _c1, _c2, _c3, _c4, _alpha;

            private double _e1, _e2, _e3, _e4, _e5, _e6;
            private double _chaosEma1, _chaosEma2;
            private double _filteredValue, _prevT3;
            private double _atrEma;
            private bool _initialized;

            public bool IsUptrend { get; private set; }

            public T3ProCalc(int period, int tCount, double vFactor,
                bool chaosSmoothingEnabled, int chaosSmoothingPeriod,
                bool filterEnabled, double filterMultiplier, int filterATRPeriod)
            {
                _period = period; _tCount = tCount; _vFactor = vFactor;
                _chaosEnabled = chaosSmoothingEnabled; _chaosPeriod = chaosSmoothingPeriod;
                _filterEnabled = filterEnabled; _filterMultiplier = filterMultiplier;
                _filterATRPeriod = filterATRPeriod;

                double v = vFactor;
                _c1 = -v * v * v;
                _c2 = 3 * v * v + 3 * v * v * v;
                _c3 = -6 * v * v - 3 * v - 3 * v * v * v;
                _c4 = 1 + 3 * v + v * v * v + 3 * v * v;
                _alpha = 2.0 / (period + 1);
            }

            public void Update(int bar, double high, double low, double close, double prevClose)
            {
                if (bar == 0)
                {
                    _e1 = _e2 = _e3 = _e4 = _e5 = _e6 = close;
                    _chaosEma1 = _chaosEma2 = close;
                    _filteredValue = close; _prevT3 = close; _atrEma = 0;
                    IsUptrend = true; _initialized = false; return;
                }

                // T3
                _e1 += _alpha * (close - _e1);
                _e2 += _alpha * (_e1 - _e2);
                double t3;
                if (_tCount >= 2) { _e3 += _alpha * (_e2 - _e3); _e4 += _alpha * (_e3 - _e4); }
                if (_tCount >= 3) { _e5 += _alpha * (_e4 - _e5); _e6 += _alpha * (_e5 - _e6); }
                switch (_tCount)
                {
                    case 1: t3 = (1 + _vFactor) * _e1 - _vFactor * _e2; break;
                    case 2:
                        double gd1 = (1 + _vFactor) * _e1 - _vFactor * _e2;
                        double gd2 = (1 + _vFactor) * _e3 - _vFactor * _e4;
                        t3 = (1 + _vFactor) * gd1 - _vFactor * gd2; break;
                    default:
                        t3 = _c1 * _e6 + _c2 * _e5 + _c3 * _e4 + _c4 * _e3; break;
                }

                // Chaos smoothing (EMA of t3)
                double smoothed = t3;
                if (_chaosEnabled && bar >= _chaosPeriod)
                {
                    double sa = 2.0 / (_chaosPeriod + 1);
                    _chaosEma1 += sa * (t3 - _chaosEma1);
                    _chaosEma2 += sa * (_chaosEma1 - _chaosEma2);
                    smoothed = 2 * _chaosEma1 - _chaosEma2; // DEMA default
                }

                // Filter
                double final = smoothed;
                if (_filterEnabled && bar >= _filterATRPeriod)
                {
                    double tr = Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));
                    double fa = 2.0 / (_filterATRPeriod + 1);
                    _atrEma += fa * (tr - _atrEma);
                    double thresh = _atrEma / _filterMultiplier;
                    if (Math.Abs(smoothed - _filteredValue) > thresh)
                        _filteredValue = smoothed;
                    final = _filteredValue;
                }

                // Trend
                if (!_initialized && bar >= _period) { IsUptrend = final >= _prevT3; _initialized = true; }
                else if (_initialized) { if (final > _prevT3) IsUptrend = true; else if (final < _prevT3) IsUptrend = false; }
                _prevT3 = final;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // VIDYA Pro Calculator
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private class VIDYAProCalc
        {
            private readonly int _period, _volPeriod, _smoothPeriod, _atrPeriod;
            private readonly bool _smoothEnabled, _filterEnabled;
            private readonly double _filterMultiplier;

            private double[] _changes;
            private int _changeIdx;
            private double _sumUp, _sumDown;
            private double _vidya, _smoothed, _emaSmooth, _atrEma;
            private double _upperBand, _lowerBand;
            private bool _initialized;

            public bool IsUptrend { get; private set; }

            public VIDYAProCalc(int period, int volatilityPeriod, bool smoothingEnabled,
                int smoothingPeriod, bool filterEnabled, double filterMultiplier, int atrPeriod)
            {
                _period = period; _volPeriod = volatilityPeriod;
                _smoothEnabled = smoothingEnabled; _smoothPeriod = smoothingPeriod;
                _filterEnabled = filterEnabled; _filterMultiplier = filterMultiplier;
                _atrPeriod = atrPeriod;
                _changes = new double[volatilityPeriod];
            }

            public void Update(int bar, double high, double low, double close, double prevClose)
            {
                if (bar == 0) { _vidya = close; _smoothed = close; _emaSmooth = close; return; }

                // ATR
                double tr = Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));
                double fa = 2.0 / (_atrPeriod + 1);
                _atrEma += fa * (tr - _atrEma);

                // CMO
                double change = close - prevClose;
                if (bar >= _volPeriod)
                {
                    double old = _changes[_changeIdx];
                    if (old > 0) _sumUp -= old; else _sumDown -= Math.Abs(old);
                }
                if (change > 0) _sumUp += change; else _sumDown += Math.Abs(change);
                _changes[_changeIdx] = change;
                _changeIdx = (_changeIdx + 1) % _volPeriod;

                double cmo = (_sumUp + _sumDown) != 0 ? (_sumUp - _sumDown) / (_sumUp + _sumDown) : 0;
                double alpha = 2.0 / (_period + 1);
                double scaledAlpha = alpha * Math.Abs(cmo);
                _vidya = scaledAlpha * close + (1 - scaledAlpha) * _vidya;

                // Smoothing (EMA)
                double output = _vidya;
                if (_smoothEnabled)
                {
                    double sa = 2.0 / (_smoothPeriod + 1);
                    _emaSmooth += sa * (_vidya - _emaSmooth);
                    output = _emaSmooth;
                }
                _smoothed = output;

                // Bands & trend
                double fd = _filterEnabled ? _filterMultiplier * _atrEma : 0;
                _upperBand = output + fd;
                _lowerBand = output - fd;

                if (!_initialized && bar >= Math.Max(_period, _volPeriod))
                { IsUptrend = close > output; _initialized = true; }
                else if (_initialized)
                {
                    if (_filterEnabled)
                    { if (!IsUptrend && close > _upperBand) IsUptrend = true; else if (IsUptrend && close < _lowerBand) IsUptrend = false; }
                    else
                    { IsUptrend = close > output; }
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // EasyTrend Calculator
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private class EasyTrendCalc
        {
            private readonly int _period, _smoothPeriod, _atrPeriod;
            private readonly bool _smoothEnabled, _filterEnabled;
            private readonly double _filterMultiplier;

            private double _ema, _smoothEma, _atrEma;
            private double _upper, _lower;
            private bool _initialized;

            public bool IsUptrend { get; private set; }

            public EasyTrendCalc(int period, bool smoothingEnabled, int smoothingPeriod,
                bool filterEnabled, double filterMultiplier, int filterATRPeriod)
            {
                _period = period; _smoothEnabled = smoothingEnabled; _smoothPeriod = smoothingPeriod;
                _filterEnabled = filterEnabled; _filterMultiplier = filterMultiplier; _atrPeriod = filterATRPeriod;
            }

            public void Update(int bar, double high, double low, double close, double prevClose)
            {
                if (bar == 0) { _ema = close; _smoothEma = close; _atrEma = 0; return; }

                // ATR
                double tr = Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));
                _atrEma += (2.0 / (_atrPeriod + 1)) * (tr - _atrEma);

                // MA (EMA)
                _ema += (2.0 / (_period + 1)) * (close - _ema);
                double output = _ema;

                // Smoothing
                if (_smoothEnabled)
                { _smoothEma += (2.0 / (_smoothPeriod + 1)) * (_ema - _smoothEma); output = _smoothEma; }

                // Bands & trend
                double fd = _filterEnabled ? _filterMultiplier * _atrEma : 0;
                _upper = output + fd; _lower = output - fd;

                if (!_initialized && bar >= _period) { IsUptrend = close > output; _initialized = true; }
                else if (_initialized)
                {
                    if (_filterEnabled)
                    { if (!IsUptrend && close > _upper) IsUptrend = true; else if (IsUptrend && close < _lower) IsUptrend = false; }
                    else { IsUptrend = close > output; }
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Ruby River Calculator
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private class RubyRiverCalc
        {
            private readonly int _maPeriod, _smoothPeriod, _offsetPeriod;
            private readonly bool _smoothEnabled;
            private readonly double _offsetMultiplier;

            private double _ema, _smoothEma, _atrEma;
            private bool _initialized;

            public bool IsUptrend { get; private set; }
            public double HighMA { get; private set; }
            public double LowMA  { get; private set; }

            public RubyRiverCalc(int maPeriod, bool smoothingEnabled, int smoothingPeriod,
                double offsetMultiplier, int offsetPeriod)
            {
                _maPeriod = maPeriod; _smoothEnabled = smoothingEnabled; _smoothPeriod = smoothingPeriod;
                _offsetMultiplier = offsetMultiplier; _offsetPeriod = offsetPeriod;
            }

            public void Update(int bar, double high, double low, double close, double prevClose)
            {
                if (bar == 0)
                {
                    _ema = close; _smoothEma = close; _atrEma = 0;
                    HighMA = high; LowMA = low; return;
                }

                // ATR
                double tr = Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));
                _atrEma += (2.0 / (_offsetPeriod + 1)) * (tr - _atrEma);

                // MA
                _ema += (2.0 / (_maPeriod + 1)) * (close - _ema);
                double ma = _ema;
                if (_smoothEnabled)
                { _smoothEma += (2.0 / (_smoothPeriod + 1)) * (_ema - _smoothEma); ma = _smoothEma; }

                double offset = _offsetMultiplier * _atrEma;
                HighMA = ma + offset;
                LowMA  = ma - offset;

                if (!_initialized && bar >= _maPeriod) { IsUptrend = close > ma; _initialized = true; }
                else if (_initialized)
                {
                    if (close > HighMA) IsUptrend = true;
                    else if (close < LowMA) IsUptrend = false;
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Dragon Trend Calculator
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private class DragonTrendCalc
        {
            private readonly int _period, _smoothPeriod;
            private readonly bool _smoothEnabled;

            private double _momEma, _smoothEma;
            private bool _initialized;

            public bool   IsUptrend  { get; private set; }
            public double PrevSignal { get; private set; }

            public DragonTrendCalc(int period, bool smoothingEnabled, int smoothingPeriod)
            { _period = period; _smoothEnabled = smoothingEnabled; _smoothPeriod = smoothingPeriod; }

            // Maintains a small ring buffer of close prices to compute momentum
            private double[] _closes;
            private int _closeIdx;
            private bool _bufferFilled;

            public void Update(int bar, double close)
            {
                if (_closes == null) _closes = new double[_period + 1];

                _closes[_closeIdx] = close;
                _closeIdx = (_closeIdx + 1) % _closes.Length;
                if (!_bufferFilled && bar >= _period) _bufferFilled = true;

                if (bar < _period) { PrevSignal = 0; return; }

                // Momentum = close - close[period bars ago]
                int oldIdx = _closeIdx; // after increment, points to oldest
                double momentum = close - _closes[oldIdx];

                // Smooth momentum with EMA
                _momEma += (2.0 / (_period + 1)) * (momentum - _momEma);
                double signal = _momEma;

                if (_smoothEnabled)
                { _smoothEma += (2.0 / (_smoothPeriod + 1)) * (signal - _smoothEma); signal = _smoothEma; }

                double previousSignal = PrevSignal;
                PrevSignal = signal;

                if (!_initialized && bar >= _period + _smoothPeriod)
                { IsUptrend = signal > 0; _initialized = true; }
                else if (_initialized)
                {
                    if (signal > 0 && previousSignal <= 0) IsUptrend = true;
                    else if (signal < 0 && previousSignal >= 0) IsUptrend = false;
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Solar Wave Calculator
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private class SolarWaveCalc
        {
            private readonly int _atrPeriod, _refPeriod, _refCloseWeight;
            private readonly double _trendMult, _stopMult;

            private double[] _trBuffer;
            private double[] _refBuffer;
            private int _trIdx, _refIdx;
            private double _atr, _trailingStop, _refPrice;
            private bool _initialized;

            public bool IsUptrend  { get; private set; }
            public int  CountWave  { get; private set; }

            public SolarWaveCalc(int atrPeriod, double trendMultiplier, double stopMultiplier,
                int refPricePeriod, int refPriceCloseWeight)
            {
                _atrPeriod = atrPeriod; _trendMult = trendMultiplier; _stopMult = stopMultiplier;
                _refPeriod = refPricePeriod; _refCloseWeight = refPriceCloseWeight;
                _trBuffer  = new double[atrPeriod];
                _refBuffer = new double[refPricePeriod + 1];
            }

            public void Update(int bar, double high, double low, double close, double prevClose)
            {
                if (bar == 0)
                { _refPrice = close; _trailingStop = close; CountWave = 0; return; }

                // ATR
                double tr = Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));
                _trBuffer[_trIdx] = tr;
                _trIdx = (_trIdx + 1) % _atrPeriod;
                int cnt = Math.Min(bar + 1, _atrPeriod);
                double atrSum = 0; for (int i = 0; i < cnt; i++) atrSum += _trBuffer[i];
                _atr = atrSum / cnt;

                // Reference price
                double hl = (high + low) / 2;
                double wp = (close * _refCloseWeight + hl) / (_refCloseWeight + 1);
                _refBuffer[_refIdx] = wp;
                _refIdx = (_refIdx + 1) % _refBuffer.Length;
                int rc = Math.Min(bar + 1, _refPeriod);
                double rs = 0; for (int i = 0; i < rc; i++) rs += _refBuffer[i];
                _refPrice = rs / rc;

                double stopOff = _stopMult * _atr;
                bool prevUp = IsUptrend;

                if (!_initialized && bar >= _atrPeriod)
                { IsUptrend = close > _refPrice; _trailingStop = IsUptrend ? _refPrice - stopOff : _refPrice + stopOff; _initialized = true; }
                else if (_initialized)
                {
                    if (IsUptrend)
                    {
                        double ns = _refPrice - stopOff;
                        if (ns > _trailingStop) _trailingStop = ns;
                        if (close < _trailingStop) { IsUptrend = false; _trailingStop = _refPrice + stopOff; CountWave = -1; }
                        else { CountWave = close > prevClose ? Math.Max(1, CountWave + 1) : Math.Max(1, CountWave - 1); }
                    }
                    else
                    {
                        double ns = _refPrice + stopOff;
                        if (ns < _trailingStop) _trailingStop = ns;
                        if (close > _trailingStop) { IsUptrend = true; _trailingStop = _refPrice - stopOff; CountWave = 1; }
                        else { CountWave = close < prevClose ? Math.Min(-1, CountWave - 1) : Math.Min(-1, CountWave + 1); }
                    }
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // AIQ_1 Calculator
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private class AIQ1Calc
        {
            private readonly int _period;
            private readonly bool _useBetter;
            private readonly double _sPctAbove, _sPctBelow;

            private double _haOpen, _haHigh, _haLow, _haClose;
            private double _prevHaOpen, _prevHaClose;
            private double _emaO, _emaH, _emaL, _emaC;
            private bool _initialized;

            public bool IsUptrend { get; private set; }

            public AIQ1Calc(int period, bool useBetterFormula, double sPctAbove, double sPctBelow)
            { _period = period; _useBetter = useBetterFormula; _sPctAbove = sPctAbove; _sPctBelow = sPctBelow; }

            public void Update(int bar, double open, double high, double low, double close)
            {
                if (bar == 0)
                {
                    _haOpen = open; _haClose = (open + high + low + close) / 4;
                    _haHigh = high; _haLow = low;
                    _prevHaOpen = _haOpen; _prevHaClose = _haClose;
                    _emaO = _haOpen; _emaH = _haHigh; _emaL = _haLow; _emaC = _haClose;
                    return;
                }

                // HA
                if (_useBetter)
                {
                    _haOpen = (_prevHaOpen + _prevHaClose) / 2;
                    _haClose = (open + high + low + close) / 4;
                    _haHigh = Math.Max(high, Math.Max(_haOpen, _haClose));
                    _haLow  = Math.Min(low,  Math.Min(_haOpen, _haClose));
                }
                else
                {
                    _haOpen  = (open + close) / 2; // approximation without prev bar
                    _haClose = (open + high + low + close) / 4;
                    _haHigh  = Math.Max(high, Math.Max(_haOpen, _haClose));
                    _haLow   = Math.Min(low,  Math.Min(_haOpen, _haClose));
                }
                _prevHaOpen = _haOpen; _prevHaClose = _haClose;

                // Smooth (SMA approximation via EMA with period alpha)
                double k = 2.0 / (_period + 1);
                _emaO += k * (_haOpen  - _emaO);
                _emaH += k * (_haHigh  - _emaH);
                _emaL += k * (_haLow   - _emaL);
                _emaC += k * (_haClose - _emaC);

                bool prevUp = IsUptrend;
                bool bull = _emaC > _emaO;
                bool bear = _emaC < _emaO;
                bool strongBull = bull && _emaL >= Math.Min(_emaO, _emaC);
                bool strongBear = bear && _emaH <= Math.Max(_emaO, _emaC);

                if (strongBull)       IsUptrend = true;
                else if (strongBear)  IsUptrend = false;
                else if (bull)        IsUptrend = true;
                else if (bear)        IsUptrend = false;

                if (!_initialized && bar >= _period) _initialized = true;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // AAATrendSync Calculator
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private class AAACalc
        {
            private readonly int _fp, _fsp, _mp, _msp, _sp, _ssp, _atrPeriod;
            private readonly bool _fse, _mse, _sse, _minSpread;
            private readonly double _minSpreadMult;

            private double _fEma, _fSmooth, _mEma, _mSmooth, _sEma, _sSmooth, _atrEma;

            public bool IsUptrend   { get; private set; }
            public bool IsDowntrend { get; private set; }

            public AAACalc(int fastPeriod, bool fastSmoothingEnabled, int fastSmoothingPeriod,
                int midPeriod, bool midSmoothingEnabled, int midSmoothingPeriod,
                int slowPeriod, bool slowSmoothingEnabled, int slowSmoothingPeriod,
                bool minSpreadEnabled, double minSpreadATRMultiplier, int minSpreadATRPeriod)
            {
                _fp = fastPeriod; _fse = fastSmoothingEnabled; _fsp = fastSmoothingPeriod;
                _mp = midPeriod;  _mse = midSmoothingEnabled;  _msp = midSmoothingPeriod;
                _sp = slowPeriod; _sse = slowSmoothingEnabled; _ssp = slowSmoothingPeriod;
                _minSpread = minSpreadEnabled; _minSpreadMult = minSpreadATRMultiplier; _atrPeriod = minSpreadATRPeriod;
            }

            public void Update(int bar, double high, double low, double close)
            {
                if (bar == 0)
                {
                    _fEma = _fSmooth = _mEma = _mSmooth = _sEma = _sSmooth = close; _atrEma = 0; return;
                }

                // ATR (for spread filter)
                double prevClose = close; // best we can do without prevClose param here â€” use close as proxy on bar 0
                _atrEma += (2.0 / (_atrPeriod + 1)) * ((high - low) - _atrEma);

                // EMAs
                _fEma += (2.0 / (_fp + 1)) * (close - _fEma);
                double fast = _fse ? (_fSmooth += (2.0 / (_fsp + 1)) * (_fEma - _fSmooth)) : _fEma;

                _mEma += (2.0 / (_mp + 1)) * (close - _mEma);
                double mid = _mse ? (_mSmooth += (2.0 / (_msp + 1)) * (_mEma - _mSmooth)) : _mEma;

                _sEma += (2.0 / (_sp + 1)) * (close - _sEma);
                double slow = _sse ? (_sSmooth += (2.0 / (_ssp + 1)) * (_sEma - _sSmooth)) : _sEma;

                bool spreadOK = true;
                if (_minSpread) { double minS = _atrEma * _minSpreadMult; spreadOK = Math.Abs(fast - mid) >= minS; }

                IsUptrend   = spreadOK && fast > mid && mid > slow;
                IsDowntrend = spreadOK && fast < mid && mid < slow;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // AIQ SuperBands Calculator
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private class SuperBandsCalc
        {
            private readonly int _mainLen, _fastLen;
            private double[] _mainBuf, _fastBuf;
            private int _mIdx, _fIdx;
            private double _mainMiddle, _mainUpper, _mainLower;

            public bool IsUptrend { get; private set; }

            public SuperBandsCalc(int halfLengthMain, int halfLengthFast)
            {
                _mainLen = halfLengthMain * 2 + 1;
                _fastLen = halfLengthFast * 2 + 1;
                _mainBuf = new double[_mainLen];
                _fastBuf = new double[_fastLen];
            }

            public void Update(int bar, double close)
            {
                _mainBuf[_mIdx] = close;
                _fastBuf[_fIdx] = close;

                int mp = Math.Min(bar + 1, _mainLen);
                CalcBands(_mainBuf, mp, _mIdx, out _mainMiddle, out _);
                double mainStd; CalcBands(_mainBuf, mp, _mIdx, out _, out mainStd);
                _mainUpper = _mainMiddle + 2.5 * mainStd;
                _mainLower = _mainMiddle - 2.5 * mainStd;

                IsUptrend = close > _mainMiddle;

                _mIdx = (_mIdx + 1) % _mainLen;
                _fIdx = (_fIdx + 1) % _fastLen;
            }

            private void CalcBands(double[] buf, int period, int curIdx, out double middle, out double stdDev)
            {
                double sum = 0, wsum = 0;
                int half = period / 2;
                for (int i = 0; i < period; i++)
                {
                    int idx = (curIdx - i + buf.Length) % buf.Length;
                    double w = half - Math.Abs(i - half) + 1;
                    sum += buf[idx] * w; wsum += w;
                }
                middle = sum / wsum;
                double sq = 0;
                for (int i = 0; i < period; i++)
                { int idx = (curIdx - i + buf.Length) % buf.Length; double d = buf[idx] - middle; sq += d * d; }
                stdDev = Math.Sqrt(sq / period);
            }
        }
    }
}
