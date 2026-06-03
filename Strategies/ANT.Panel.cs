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
using System.Windows.Input;
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
        #region Resize Edge Enum
        private enum ResizeEdge
        {
            None,
            Left,
            Right,
            Top,
            Bottom,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }
        #endregion

        #region Additional Panel Fields
        private ResizeEdge currentResizeEdge = ResizeEdge.None;
        private const double EdgeThreshold = 8;  // pixels from edge to trigger resize
        private double panelWidth = 200;
        private double panelHeight = 400;
        private double minPanelWidth = 150;
        private double minPanelHeight = 200;
        private Point resizeStartMousePos;
        private double resizeStartLeft, resizeStartTop;
        private TextBlock lblAIQ1Name;  // Dynamic trigger label

        // Parameter-confirmation gate UI
        private StackPanel confirmView;   // shown until armed
        private StackPanel tradingView;   // shown after armed
        private TextBox txtTP, txtSL, txtS1Start, txtS1End, txtS2Start, txtS2End;
        private CheckBox chkAutoTrade;
        private TextBlock lblConfirmError;
        private string confirmedParamsFile;

        // Host-window key interception (stops NT8 chart from hijacking keystrokes
        // while a confirm field is focused).
        private Window hostWindow;
        private KeyEventHandler hostKeyHandler;
        #endregion

        #region Chart Panel
        private void CreateControlPanel()
        {
            try
            {
                string settingsDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8", "settings");
                panelSettingsFile = System.IO.Path.Combine(settingsDir, "ANT_PanelSettings.txt");
                panelTransform = new TranslateTransform(0, 0);
                panelScale = new ScaleTransform(1, 1);
                
                LoadPanelSettings();

                controlPanel = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = new SolidColorBrush(Color.FromArgb(230, 30, 30, 40)),
                    Width = panelWidth,
                    MinWidth = minPanelWidth,
                    MinHeight = minPanelHeight,
                    RenderTransform = panelTransform,
                    Cursor = Cursors.Arrow
                };
                
                controlPanel.MouseLeftButtonDown += Panel_MouseLeftButtonDown;
                controlPanel.MouseLeftButtonUp += Panel_MouseLeftButtonUp;
                controlPanel.MouseMove += Panel_MouseMove;
                controlPanel.MouseLeave += Panel_MouseLeave;

                var border = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 100)),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(8)
                };
                
                var rootStack = new StackPanel();

                // Header - ALWAYS visible (armed and unarmed)
                rootStack.Children.Add(new TextBlock { Text = "ANTrading", FontWeight = FontWeights.Bold, Foreground = Brushes.Cyan, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 2) });

                // Confirm view - shown until the trader clicks Confirm
                confirmView = BuildConfirmView();
                rootStack.Children.Add(confirmView);

                // Trading view - everything that was previously in the panel
                tradingView = new StackPanel();

                lblSubtitle = new TextBlock { Foreground = Brushes.LightGray, FontSize = 8, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,6) };
                tradingView.Children.Add(lblSubtitle);
                
                tradingView.Children.Add(new TextBlock { Text = "--- Confluence (8) ---", Foreground = Brushes.Gray, FontSize = 8, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,2,0,2) });
                
                // Indicators in alphabetical order
                tradingView.Children.Add(CreateRow("AAA TrendSync", ref chkAAASync, ref lblAAASync, UseAAATrendSync));
                tradingView.Children.Add(CreateRow("AIQ SuperBands", ref chkSuperBands, ref lblSuperBands, UseAIQSuperBands));
                tradingView.Children.Add(CreateRow("Dragon Trend", ref chkDragonTrend, ref lblDragonTrend, UseDragonTrend));
                tradingView.Children.Add(CreateRow("Easy Trend", ref chkEasyTrend, ref lblEasyTrend, UseEasyTrend));
                tradingView.Children.Add(CreateRow("Ruby River", ref chkRubyRiver, ref lblRubyRiver, UseRubyRiver));
                tradingView.Children.Add(CreateRow("Solar Wave", ref chkSolarWave, ref lblSolarWave, UseSolarWave));
                tradingView.Children.Add(CreateRow("T3 Pro", ref chkT3Pro, ref lblT3Pro, UseT3Pro));
                tradingView.Children.Add(CreateRow("VIDYA Pro", ref chkVIDYA, ref lblVIDYA, UseVIDYAPro));
                
                tradingView.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0,1,0,0), Margin = new Thickness(0,6,0,6) });
                tradingView.Children.Add(new TextBlock { Text = "--- Trigger ---", Foreground = Brushes.Orange, FontSize = 8, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,2,0,2) });
                
                var aiqRow = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                aiqRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                aiqRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                lblAIQ1Name = new TextBlock { Text = "AIQ_1 (Yellow)", Foreground = Brushes.Yellow, FontSize = 9, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(lblAIQ1Name, 0); aiqRow.Children.Add(lblAIQ1Name);
                lblAIQ1Status = new TextBlock { Text = "---", Foreground = Brushes.Gray, FontSize = 9, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(lblAIQ1Status, 1); aiqRow.Children.Add(lblAIQ1Status);
                tradingView.Children.Add(aiqRow);
                
                lblWindowStatus = new TextBlock { Text = "Window: CLOSED", Foreground = Brushes.Gray, FontSize = 8, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,2,0,2) };
                tradingView.Children.Add(lblWindowStatus);
                
                tradingView.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0,1,0,0), Margin = new Thickness(0,6,0,6) });

                lblTriggerMode = new TextBlock { Text = $"Signal>={MinConfluenceRequired} Trade>={MinConfluenceForAutoTrade} CD={CooldownBars}", Foreground = Brushes.LightGray, FontSize = 9 };
                lblTradeStatus = new TextBlock { Text = EnableAutoTrading ? "AUTO TRADING ON" : "Mode: Signal Only", Foreground = EnableAutoTrading ? Brushes.Lime : Brushes.Cyan, FontWeight = FontWeights.Bold, FontSize = 10, Margin = new Thickness(0,2,0,2) };
                lblSessionStats = new TextBlock { Text = "Signals: 0", Foreground = Brushes.LightGray, FontSize = 9 };

                tradingView.Children.Add(lblTriggerMode);
                tradingView.Children.Add(lblTradeStatus);
                tradingView.Children.Add(lblSessionStats);
                
                tradingView.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0,1,0,0), Margin = new Thickness(0,6,0,6) });
                
                signalBorder = new Border { BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(3), Padding = new Thickness(4) };
                lblLastSignal = new TextBlock { Text = "Waiting for Yellow...", Foreground = Brushes.Gray, FontSize = 9, TextWrapping = TextWrapping.Wrap };
                signalBorder.Child = lblLastSignal;
                tradingView.Children.Add(signalBorder);
                
                // Add resize grip indicator in bottom-right corner
                var resizeIndicator = new Canvas { Width = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 4, 0, 0) };
                for (int i = 0; i < 3; i++)
                {
                    var line = new Line { X1 = 10 - i * 4, Y1 = 10, X2 = 10, Y2 = 10 - i * 4, Stroke = new SolidColorBrush(Color.FromRgb(120, 120, 140)), StrokeThickness = 1 };
                    resizeIndicator.Children.Add(line);
                }
                tradingView.Children.Add(resizeIndicator);

                rootStack.Children.Add(tradingView);

                // isArmed is always false at load, so confirmView shows and tradingView hides
                confirmView.Visibility = isArmed ? Visibility.Collapsed : Visibility.Visible;
                tradingView.Visibility = isArmed ? Visibility.Visible : Visibility.Collapsed;

                border.Child = rootStack;
                controlPanel.Children.Add(border);

                UIElementCollection panelHolder = (ChartControl.Parent as Grid)?.Children;
                if (panelHolder != null) panelHolder.Add(controlPanel);
                panelActive = true;

                // Intercept keystrokes at the host window so the chart cannot hijack
                // typing into the confirm fields. Registered with handledEventsToo so
                // it still runs even if something downstream marks the event handled.
                hostWindow = Window.GetWindow(controlPanel) ?? Window.GetWindow(ChartControl);
                if (hostWindow != null)
                {
                    hostKeyHandler = new KeyEventHandler(Host_PreviewKeyDown);
                    hostWindow.AddHandler(UIElement.PreviewKeyDownEvent, hostKeyHandler, true);
                }
                
                // Apply initial position
                ApplyPanelConstraints();
            }
            catch (Exception ex) { Print($"Panel error: {ex.Message}"); }
        }

        private StackPanel BuildConfirmView()
        {
            var v = new StackPanel();

            v.Children.Add(new TextBlock { Text = "Confirm parameters", Foreground = Brushes.LightGray, FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,6) });

            v.Children.Add(BuildParamRow("Take Profit $", ref txtTP, TakeProfitUSD.ToString("F0")));
            v.Children.Add(BuildParamRow("Stop Loss $", ref txtSL, StopLossUSD.ToString("F0")));

            v.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0,1,0,0), Margin = new Thickness(0,6,0,6) });
            v.Children.Add(new TextBlock { Text = "Session 1", Foreground = Brushes.Orange, FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,2) });
            v.Children.Add(BuildParamRow("Start", ref txtS1Start, $"{Session1StartHour:D2}:{Session1StartMinute:D2}"));
            v.Children.Add(BuildParamRow("End", ref txtS1End, $"{Session1EndHour:D2}:{Session1EndMinute:D2}"));

            v.Children.Add(new TextBlock { Text = "Session 2", Foreground = Brushes.Orange, FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,4,0,2) });
            v.Children.Add(BuildParamRow("Start", ref txtS2Start, $"{Session2StartHour:D2}:{Session2StartMinute:D2}"));
            v.Children.Add(BuildParamRow("End", ref txtS2End, $"{Session2EndHour:D2}:{Session2EndMinute:D2}"));

            // Normalize the four time fields to zero-padded HH:MM whenever they lose
            // focus (Tab away, click another field, click Confirm). Valid entries snap
            // to HH:MM; invalid entries are left alone for ValidateAndApply to flag.
            txtS1Start.LostKeyboardFocus += NormalizeTimeField;
            txtS1End.LostKeyboardFocus   += NormalizeTimeField;
            txtS2Start.LostKeyboardFocus += NormalizeTimeField;
            txtS2End.LostKeyboardFocus   += NormalizeTimeField;

            v.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0,1,0,0), Margin = new Thickness(0,6,0,6) });

            var atRow = new Grid { Margin = new Thickness(0,1,0,1) };
            atRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            atRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var atLbl = new TextBlock { Text = "Auto-Trading", Foreground = Brushes.White, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(atLbl, 0); atRow.Children.Add(atLbl);
            chkAutoTrade = new CheckBox { IsChecked = EnableAutoTrading, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(chkAutoTrade, 1); atRow.Children.Add(chkAutoTrade);
            v.Children.Add(atRow);

            lblConfirmError = new TextBlock { Text = "", Foreground = Brushes.Red, FontSize = 9, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,4,0,0) };
            v.Children.Add(lblConfirmError);

            var btn = new Button { Content = "CONFIRM", FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0,8,0,0), Padding = new Thickness(0,4,0,4), Background = new SolidColorBrush(Color.FromRgb(20,80,20)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(42,138,42)) };
            btn.Click += OnConfirmClick;
            v.Children.Add(btn);

            return v;
        }

        private Grid BuildParamRow(string label, ref TextBox box, string initial)
        {
            var row = new Grid { Margin = new Thickness(0,2,0,2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });

            var lbl = new TextBlock { Text = label, Foreground = Brushes.White, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 0); row.Children.Add(lbl);

            box = new TextBox
            {
                Text = initial,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(45,45,58)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(90,90,110)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Right
            };
            box.PreviewMouseLeftButtonDown += TextBox_PreviewMouseLeftButtonDown;
            box.GotKeyboardFocus += (s, e) => { (s as TextBox)?.SelectAll(); };
            Grid.SetColumn(box, 1); row.Children.Add(box);
            return row;
        }

        // Snap a valid time field to zero-padded HH:MM when it loses focus.
        private void NormalizeTimeField(object sender, KeyboardFocusChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;
            if (TryParseHHMM(tb.Text, out int hh, out int mm))
                tb.Text = $"{hh:D2}:{mm:D2}";
        }

        // Forces keyboard focus into a chart-hosted TextBox on first click and
        // stops that click from starting a panel drag.
        private void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb != null && !tb.IsKeyboardFocusWithin)
            {
                tb.Focus();
                Keyboard.Focus(tb);
                e.Handled = true;
            }
        }

        private bool IsConfirmBox(TextBox tb)
        {
            return tb == txtTP || tb == txtSL || tb == txtS1Start || tb == txtS1End || tb == txtS2Start || tb == txtS2End;
        }

        // While a confirm field is focused, fully own the keystroke: edit the
        // TextBox text ourselves and mark the event handled so NT8's chart never
        // sees it (otherwise the chart hijacks digits as instrument quick-search).
        private void Host_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (isArmed) return;
            var tb = Keyboard.FocusedElement as TextBox;
            if (tb == null || !IsConfirmBox(tb)) return;

            Key k = e.Key;

            // Clipboard / select-all
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (k == Key.A) { tb.SelectAll(); e.Handled = true; return; }
                if (k == Key.V) { try { InsertText(tb, System.Windows.Clipboard.GetText() ?? ""); } catch { } e.Handled = true; return; }
                // Other Ctrl combos: swallow so the chart doesn't act on them
                e.Handled = true;
                return;
            }

            // From here we fully own the keystroke.
            e.Handled = true;

            // Digits (top row, no shift) and numpad
            if (k >= Key.D0 && k <= Key.D9 && Keyboard.Modifiers == ModifierKeys.None)
            { InsertText(tb, ((char)('0' + (k - Key.D0))).ToString()); return; }
            if (k >= Key.NumPad0 && k <= Key.NumPad9)
            { InsertText(tb, ((char)('0' + (k - Key.NumPad0))).ToString()); return; }

            // Colon for HH:MM (US keyboard colon = Shift+OemSemicolon; accept the key either way)
            if (k == Key.OemSemicolon) { InsertText(tb, ":"); return; }
            // Period / decimal point
            if (k == Key.OemPeriod || k == Key.Decimal) { InsertText(tb, "."); return; }

            switch (k)
            {
                case Key.Back:   Backspace(tb); return;
                case Key.Delete: DeleteForward(tb); return;
                case Key.Left:   MoveCaret(tb, -1); return;
                case Key.Right:  MoveCaret(tb, +1); return;
                case Key.Home:   tb.SelectionStart = 0; tb.SelectionLength = 0; return;
                case Key.End:    tb.SelectionStart = tb.Text.Length; tb.SelectionLength = 0; return;
                case Key.Tab:    FocusNextConfirmBox(tb); return;
                case Key.Return: OnConfirmClick(null, null); return;
                default: return;  // swallowed no-op (blocks letters etc. from reaching the chart)
            }
        }

        private void InsertText(TextBox tb, string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            int start = tb.SelectionStart;
            int len = tb.SelectionLength;
            string t = tb.Text.Remove(start, len).Insert(start, s);
            tb.Text = t;
            tb.SelectionStart = start + s.Length;
            tb.SelectionLength = 0;
        }

        private void Backspace(TextBox tb)
        {
            int start = tb.SelectionStart, len = tb.SelectionLength;
            if (len > 0) { tb.Text = tb.Text.Remove(start, len); tb.SelectionStart = start; }
            else if (start > 0) { tb.Text = tb.Text.Remove(start - 1, 1); tb.SelectionStart = start - 1; }
            tb.SelectionLength = 0;
        }

        private void DeleteForward(TextBox tb)
        {
            int start = tb.SelectionStart, len = tb.SelectionLength;
            if (len > 0) { tb.Text = tb.Text.Remove(start, len); tb.SelectionStart = start; }
            else if (start < tb.Text.Length) { tb.Text = tb.Text.Remove(start, 1); tb.SelectionStart = start; }
            tb.SelectionLength = 0;
        }

        private void MoveCaret(TextBox tb, int delta)
        {
            int p = tb.SelectionStart + delta;
            if (p < 0) p = 0;
            if (p > tb.Text.Length) p = tb.Text.Length;
            tb.SelectionStart = p;
            tb.SelectionLength = 0;
        }

        private void FocusNextConfirmBox(TextBox tb)
        {
            var order = new[] { txtTP, txtSL, txtS1Start, txtS1End, txtS2Start, txtS2End };
            int i = Array.IndexOf(order, tb);
            if (i < 0) return;
            var next = order[(i + 1) % order.Length];
            if (next == null) return;
            next.Focus();
            Keyboard.Focus(next);
            next.SelectAll();
        }

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            string err;
            if (!ValidateAndApply(out err))
            {
                if (lblConfirmError != null) lblConfirmError.Text = err;
                return;
            }

            SaveConfirmedParams();
            isArmed = true;

            if (confirmView != null) confirmView.Visibility = Visibility.Collapsed;
            if (tradingView != null) tradingView.Visibility = Visibility.Visible;

            PrintAndLog($">>> PARAMETERS CONFIRMED & ARMED | SL=${StopLossUSD:F0} TP=${TakeProfitUSD:F0} | Sessions: {GetTradingHoursString()} | AutoTrade={EnableAutoTrading}", DateTime.Now);
        }

        private bool ValidateAndApply(out string error)
        {
            error = "";

            if (!double.TryParse(txtTP.Text.Trim(), out double tp) || tp < 10 || tp > 3000)
            { error = "TP must be 10-3000"; return false; }
            if (!double.TryParse(txtSL.Text.Trim(), out double sl) || sl < 10 || sl > 3000)
            { error = "SL must be 10-3000"; return false; }

            if (!TryParseHHMM(txtS1Start.Text, out int s1sh, out int s1sm)) { error = "S1 start: use HH:MM"; return false; }
            if (!TryParseHHMM(txtS1End.Text,   out int s1eh, out int s1em)) { error = "S1 end: use HH:MM"; return false; }
            if (!TryParseHHMM(txtS2Start.Text, out int s2sh, out int s2sm)) { error = "S2 start: use HH:MM"; return false; }
            if (!TryParseHHMM(txtS2End.Text,   out int s2eh, out int s2em)) { error = "S2 end: use HH:MM"; return false; }

            if (s1sh * 60 + s1sm > s1eh * 60 + s1em) { error = "S1 start after end"; return false; }
            if (s2sh * 60 + s2sm > s2eh * 60 + s2em) { error = "S2 start after end"; return false; }

            TakeProfitUSD = tp;
            StopLossUSD = sl;
            Session1StartHour = s1sh; Session1StartMinute = s1sm;
            Session1EndHour   = s1eh; Session1EndMinute   = s1em;
            Session2StartHour = s2sh; Session2StartMinute = s2sm;
            Session2EndHour   = s2eh; Session2EndMinute   = s2em;
            EnableAutoTrading = chkAutoTrade?.IsChecked ?? false;

            return true;
        }

        private bool TryParseHHMM(string s, out int hh, out int mm)
        {
            hh = 0; mm = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            string[] p = s.Trim().Split(':');
            if (p.Length != 2) return false;
            if (!int.TryParse(p[0].Trim(), out hh) || !int.TryParse(p[1].Trim(), out mm)) return false;
            if (hh < 0 || hh > 23 || mm < 0 || mm > 59) return false;
            return true;
        }

        private void SaveConfirmedParams()
        {
            try
            {
                if (string.IsNullOrEmpty(confirmedParamsFile)) return;
                string dir = System.IO.Path.GetDirectoryName(confirmedParamsFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string line = string.Join(",",
                    TakeProfitUSD.ToString("F0"),
                    StopLossUSD.ToString("F0"),
                    Session1StartHour, Session1StartMinute,
                    Session1EndHour, Session1EndMinute,
                    Session2StartHour, Session2StartMinute,
                    Session2EndHour, Session2EndMinute,
                    EnableAutoTrading ? "1" : "0");

                File.WriteAllText(confirmedParamsFile, line);
            }
            catch { }
        }

        private void LoadConfirmedParams()
        {
            try
            {
                string settingsDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8", "settings");
                string instr = (Instrument != null && Instrument.MasterInstrument != null) ? Instrument.MasterInstrument.Name : "DEFAULT";
                confirmedParamsFile = System.IO.Path.Combine(settingsDir, $"ANT_ConfirmedParams_{instr}.txt");

                if (!File.Exists(confirmedParamsFile)) return;

                string[] p = File.ReadAllText(confirmedParamsFile).Split(',');
                if (p.Length < 11) return;

                if (double.TryParse(p[0], out double tp)) TakeProfitUSD = tp;
                if (double.TryParse(p[1], out double sl)) StopLossUSD = sl;
                if (int.TryParse(p[2], out int v2)) Session1StartHour = v2;
                if (int.TryParse(p[3], out int v3)) Session1StartMinute = v3;
                if (int.TryParse(p[4], out int v4)) Session1EndHour = v4;
                if (int.TryParse(p[5], out int v5)) Session1EndMinute = v5;
                if (int.TryParse(p[6], out int v6)) Session2StartHour = v6;
                if (int.TryParse(p[7], out int v7)) Session2StartMinute = v7;
                if (int.TryParse(p[8], out int v8)) Session2EndHour = v8;
                if (int.TryParse(p[9], out int v9)) Session2EndMinute = v9;
                EnableAutoTrading = p[10].Trim() == "1";
            }
            catch { }
        }
        
        private Grid CreateRow(string name, ref CheckBox chk, ref TextBlock lbl, bool isChecked)
        {
            var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) });
            
            chk = new CheckBox { IsChecked = isChecked, VerticalAlignment = VerticalAlignment.Center };
            chk.Checked += OnChk; chk.Unchecked += OnChk;
            Grid.SetColumn(chk, 0); row.Children.Add(chk);
            
            var txt = new TextBlock { Text = name, Foreground = Brushes.White, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3,0,0,0) };
            Grid.SetColumn(txt, 1); row.Children.Add(txt);
            
            lbl = new TextBlock { Text = "---", Foreground = Brushes.Gray, FontSize = 9, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(lbl, 2); row.Children.Add(lbl);
            return row;
        }
        
        private void OnChk(object s, RoutedEventArgs e)
        {
            UseRubyRiver = chkRubyRiver?.IsChecked ?? false;
            UseDragonTrend = chkDragonTrend?.IsChecked ?? false;
            UseSolarWave = chkSolarWave?.IsChecked ?? false;
            UseVIDYAPro = chkVIDYA?.IsChecked ?? false;
            UseEasyTrend = chkEasyTrend?.IsChecked ?? false;
            UseT3Pro = chkT3Pro?.IsChecked ?? false;
            UseAAATrendSync = chkAAASync?.IsChecked ?? false;
            UseAIQSuperBands = chkSuperBands?.IsChecked ?? false;
        }
        
        private void RemoveControlPanel()
        {
            try
            {
                if (hostWindow != null && hostKeyHandler != null)
                {
                    hostWindow.RemoveHandler(UIElement.PreviewKeyDownEvent, hostKeyHandler);
                    hostWindow = null;
                    hostKeyHandler = null;
                }
                if (controlPanel != null && panelActive)
                {
                    controlPanel.MouseLeftButtonDown -= Panel_MouseLeftButtonDown;
                    controlPanel.MouseLeftButtonUp -= Panel_MouseLeftButtonUp;
                    controlPanel.MouseMove -= Panel_MouseMove;
                    controlPanel.MouseLeave -= Panel_MouseLeave;
                    UIElementCollection panelHolder = (ChartControl?.Parent as Grid)?.Children;
                    if (panelHolder != null && panelHolder.Contains(controlPanel))
                        panelHolder.Remove(controlPanel);
                    panelActive = false;
                }
            }
            catch { }
        }

        private ResizeEdge GetResizeEdge(Point mousePos)
        {
            double w = controlPanel.ActualWidth > 0 ? controlPanel.ActualWidth : panelWidth;
            double h = controlPanel.ActualHeight > 0 ? controlPanel.ActualHeight : panelHeight;
            
            bool nearLeft = mousePos.X <= EdgeThreshold;
            bool nearRight = mousePos.X >= w - EdgeThreshold;
            bool nearTop = mousePos.Y <= EdgeThreshold;
            bool nearBottom = mousePos.Y >= h - EdgeThreshold;
            
            if (nearTop && nearLeft) return ResizeEdge.TopLeft;
            if (nearTop && nearRight) return ResizeEdge.TopRight;
            if (nearBottom && nearLeft) return ResizeEdge.BottomLeft;
            if (nearBottom && nearRight) return ResizeEdge.BottomRight;
            if (nearLeft) return ResizeEdge.Left;
            if (nearRight) return ResizeEdge.Right;
            if (nearTop) return ResizeEdge.Top;
            if (nearBottom) return ResizeEdge.Bottom;
            
            return ResizeEdge.None;
        }
        
        private Cursor GetCursorForEdge(ResizeEdge edge)
        {
            switch (edge)
            {
                case ResizeEdge.Left:
                case ResizeEdge.Right:
                    return Cursors.SizeWE;
                case ResizeEdge.Top:
                case ResizeEdge.Bottom:
                    return Cursors.SizeNS;
                case ResizeEdge.TopLeft:
                case ResizeEdge.BottomRight:
                    return Cursors.SizeNWSE;
                case ResizeEdge.TopRight:
                case ResizeEdge.BottomLeft:
                    return Cursors.SizeNESW;
                default:
                    return Cursors.Hand;
            }
        }

        private void Panel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point mousePos = e.GetPosition(controlPanel);
            ResizeEdge edge = GetResizeEdge(mousePos);
            
            if (edge != ResizeEdge.None)
            {
                // Start resizing
                currentResizeEdge = edge;
                isResizing = true;
                resizeStartMousePos = e.GetPosition(ChartControl?.Parent as UIElement);
                resizeStartWidth = controlPanel.ActualWidth > 0 ? controlPanel.ActualWidth : panelWidth;
                resizeStartHeight = controlPanel.ActualHeight > 0 ? controlPanel.ActualHeight : panelHeight;
                resizeStartLeft = panelTransform.X;
                resizeStartTop = panelTransform.Y;
                controlPanel.CaptureMouse();
                e.Handled = true;
            }
            else
            {
                // Start dragging
                isDragging = true;
                dragStartPoint = e.GetPosition(ChartControl?.Parent as UIElement);
                dragStartPoint.X -= panelTransform.X;
                dragStartPoint.Y -= panelTransform.Y;
                controlPanel.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Panel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging || isResizing)
            {
                isDragging = false;
                isResizing = false;
                currentResizeEdge = ResizeEdge.None;
                controlPanel.ReleaseMouseCapture();
                SavePanelSettings();
                e.Handled = true;
            }
        }
        
        private void Panel_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!isDragging && !isResizing)
            {
                controlPanel.Cursor = Cursors.Arrow;
            }
        }

        private void Panel_MouseMove(object sender, MouseEventArgs e)
        {
            var parent = ChartControl?.Parent as FrameworkElement;
            if (parent == null) return;
            
            Point currentMousePos = e.GetPosition(parent);
            
            if (isResizing && currentResizeEdge != ResizeEdge.None)
            {
                double deltaX = currentMousePos.X - resizeStartMousePos.X;
                double deltaY = currentMousePos.Y - resizeStartMousePos.Y;
                
                double newWidth = resizeStartWidth;
                double newHeight = resizeStartHeight;
                double newLeft = resizeStartLeft;
                double newTop = resizeStartTop;
                
                // Calculate new dimensions based on which edge is being dragged
                switch (currentResizeEdge)
                {
                    case ResizeEdge.Right:
                        newWidth = resizeStartWidth + deltaX;
                        break;
                    case ResizeEdge.Left:
                        newWidth = resizeStartWidth - deltaX;
                        newLeft = resizeStartLeft + deltaX;
                        break;
                    case ResizeEdge.Bottom:
                        newHeight = resizeStartHeight + deltaY;
                        break;
                    case ResizeEdge.Top:
                        newHeight = resizeStartHeight - deltaY;
                        newTop = resizeStartTop + deltaY;
                        break;
                    case ResizeEdge.BottomRight:
                        // Proportional resize - maintain aspect ratio
                        double aspectRatio = resizeStartWidth / resizeStartHeight;
                        double avgDelta = (deltaX + deltaY) / 2;
                        newWidth = resizeStartWidth + avgDelta;
                        newHeight = newWidth / aspectRatio;
                        break;
                    case ResizeEdge.BottomLeft:
                        newWidth = resizeStartWidth - deltaX;
                        newLeft = resizeStartLeft + deltaX;
                        newHeight = resizeStartHeight + deltaY;
                        break;
                    case ResizeEdge.TopRight:
                        newWidth = resizeStartWidth + deltaX;
                        newHeight = resizeStartHeight - deltaY;
                        newTop = resizeStartTop + deltaY;
                        break;
                    case ResizeEdge.TopLeft:
                        newWidth = resizeStartWidth - deltaX;
                        newLeft = resizeStartLeft + deltaX;
                        newHeight = resizeStartHeight - deltaY;
                        newTop = resizeStartTop + deltaY;
                        break;
                }
                
                // Apply minimum size constraints
                if (newWidth < minPanelWidth)
                {
                    if (currentResizeEdge == ResizeEdge.Left || currentResizeEdge == ResizeEdge.TopLeft || currentResizeEdge == ResizeEdge.BottomLeft)
                        newLeft = resizeStartLeft + (resizeStartWidth - minPanelWidth);
                    newWidth = minPanelWidth;
                }
                if (newHeight < minPanelHeight)
                {
                    if (currentResizeEdge == ResizeEdge.Top || currentResizeEdge == ResizeEdge.TopLeft || currentResizeEdge == ResizeEdge.TopRight)
                        newTop = resizeStartTop + (resizeStartHeight - minPanelHeight);
                    newHeight = minPanelHeight;
                }
                
                // Apply boundary constraints
                if (newLeft < 0) 
                {
                    newWidth = newWidth + newLeft;  // Reduce width by the amount we went over
                    newLeft = 0;
                }
                if (newTop < 0)
                {
                    newHeight = newHeight + newTop;  // Reduce height by the amount we went over
                    newTop = 0;
                }
                if (newLeft + newWidth > parent.ActualWidth)
                {
                    newWidth = parent.ActualWidth - newLeft;
                }
                if (newTop + newHeight > parent.ActualHeight)
                {
                    newHeight = parent.ActualHeight - newTop;
                }
                
                // Re-apply minimum constraints after boundary adjustments
                newWidth = Math.Max(newWidth, minPanelWidth);
                newHeight = Math.Max(newHeight, minPanelHeight);
                
                // Apply changes
                panelWidth = newWidth;
                panelHeight = newHeight;
                controlPanel.Width = newWidth;
                controlPanel.Height = newHeight;
                panelTransform.X = newLeft;
                panelTransform.Y = newTop;
                
                e.Handled = true;
            }
            else if (isDragging)
            {
                double newX = currentMousePos.X - dragStartPoint.X;
                double newY = currentMousePos.Y - dragStartPoint.Y;
                
                double w = controlPanel.ActualWidth > 0 ? controlPanel.ActualWidth : panelWidth;
                double h = controlPanel.ActualHeight > 0 ? controlPanel.ActualHeight : panelHeight;
                
                // Constrain to chart boundaries
                newX = Math.Max(0, Math.Min(parent.ActualWidth - w, newX));
                newY = Math.Max(0, Math.Min(parent.ActualHeight - h, newY));
                
                panelTransform.X = newX;
                panelTransform.Y = newY;
                e.Handled = true;
            }
            else
            {
                // Update cursor based on mouse position
                Point mousePos = e.GetPosition(controlPanel);
                ResizeEdge edge = GetResizeEdge(mousePos);
                controlPanel.Cursor = GetCursorForEdge(edge);
            }
        }

        private void ApplyPanelConstraints()
        {
            var parent = ChartControl?.Parent as FrameworkElement;
            if (parent == null || controlPanel == null) return;
            
            double maxX = Math.Max(0, parent.ActualWidth - panelWidth);
            double maxY = Math.Max(0, parent.ActualHeight - panelHeight);
            
            panelTransform.X = Math.Max(0, Math.Min(maxX, panelTransform.X));
            panelTransform.Y = Math.Max(0, Math.Min(maxY, panelTransform.Y));
            
            controlPanel.Width = panelWidth;
            controlPanel.Height = panelHeight;
        }

        private void SavePanelSettings()
        {
            try
            {
                if (string.IsNullOrEmpty(panelSettingsFile)) return;
                string dir = System.IO.Path.GetDirectoryName(panelSettingsFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                
                double w = controlPanel.ActualWidth > 0 ? controlPanel.ActualWidth : panelWidth;
                double h = controlPanel.ActualHeight > 0 ? controlPanel.ActualHeight : panelHeight;
                
                // Save: X, Y, Width, Height
                File.WriteAllText(panelSettingsFile, $"{panelTransform.X},{panelTransform.Y},{w},{h}");
            }
            catch { }
        }

        private void LoadPanelSettings()
        {
            try
            {
                if (string.IsNullOrEmpty(panelSettingsFile) || !File.Exists(panelSettingsFile)) return;
                string content = File.ReadAllText(panelSettingsFile);
                string[] parts = content.Split(',');
                
                if (parts.Length >= 2 && double.TryParse(parts[0], out double x) && double.TryParse(parts[1], out double y))
                {
                    panelTransform.X = x;
                    panelTransform.Y = y;
                }
                if (parts.Length >= 4 && double.TryParse(parts[2], out double w) && double.TryParse(parts[3], out double h))
                {
                    panelWidth = Math.Max(minPanelWidth, w);
                    panelHeight = Math.Max(minPanelHeight, h);
                }
            }
            catch { }
        }
        
        private void UpdatePanel()
        {
            if (!panelActive || ChartControl == null) return;
            ChartControl.Dispatcher.InvokeAsync(() =>
            {
                int enabled = GetEnabledCount();
                if (lblSubtitle != null)
                    lblSubtitle.Text = enabled == 0 ? "No indicators" : $"Min {MinConfluenceRequired}/{enabled} for signal";

                // Update labels (alphabetical order for consistency)
                // AAA TrendSync - show N/A if not available, otherwise show UP/DN
                if (lblAAASync != null)
                {
                    if (!UseAAATrendSync)
                    {
                        lblAAASync.Text = "OFF";
                        lblAAASync.Foreground = Brushes.Gray;
                    }
                    else if (!AAA_Available)
                    {
                        lblAAASync.Text = "N/A";
                        lblAAASync.Foreground = Brushes.DarkGray;
                    }
                    else
                    {
                        lblAAASync.Text = AAA_IsUp ? "UP" : "DN";
                        lblAAASync.Foreground = AAA_IsUp ? Brushes.Lime : Brushes.Red;
                    }
                }
                // AIQ SuperBands - show N/A if not available, otherwise show UP/DN
                if (lblSuperBands != null)
                {
                    if (!UseAIQSuperBands)
                    {
                        lblSuperBands.Text = "OFF";
                        lblSuperBands.Foreground = Brushes.Gray;
                    }
                    else if (!SB_Available)
                    {
                        lblSuperBands.Text = "N/A";
                        lblSuperBands.Foreground = Brushes.DarkGray;
                    }
                    else
                    {
                        lblSuperBands.Text = SB_IsUp ? "UP" : "DN";
                        lblSuperBands.Foreground = SB_IsUp ? Brushes.Lime : Brushes.Red;
                    }
                }
                UpdLbl(lblDragonTrend, DT_IsUp, UseDragonTrend);
                UpdLbl(lblEasyTrend, ET_IsUp, UseEasyTrend);
                UpdLbl(lblRubyRiver, RR_IsUp, UseRubyRiver);
                UpdLbl(lblSolarWave, SW_IsUp, UseSolarWave);
                UpdLbl(lblT3Pro, T3P_IsUp, UseT3Pro);
                UpdLbl(lblVIDYA, VY_IsUp, UseVIDYAPro);
                
                if (lblAIQ1Status != null)
                {
                    lblAIQ1Status.Text = AIQ1_IsUp ? "UP" : "DN";
                    lblAIQ1Status.Foreground = AIQ1_IsUp ? Brushes.Lime : Brushes.Red;
                }
                
                // Dynamic trigger label - shows Yellow/Orange based on current state
                if (lblAIQ1Name != null)
                {
                    bool longWindowOpen = barsSinceYellowSquare >= 0 && barsSinceYellowSquare <= MaxBarsAfterYellowSquare;
                    bool shortWindowOpen = barsSinceOrangeSquare >= 0 && barsSinceOrangeSquare <= MaxBarsAfterYellowSquare;
                    var (bullConf, bearConf, _) = GetConfluence();
                    
                    if (longWindowOpen)
                    {
                        lblAIQ1Name.Text = "AIQ_1 (Yellow)";
                        lblAIQ1Name.Foreground = Brushes.Yellow;
                    }
                    else if (shortWindowOpen)
                    {
                        lblAIQ1Name.Text = "AIQ_1 (Orange)";
                        lblAIQ1Name.Foreground = Brushes.Orange;
                    }
                    else if (bearConf >= MinConfluenceRequired)
                    {
                        lblAIQ1Name.Text = "AIQ_1 (Orange)";
                        lblAIQ1Name.Foreground = Brushes.Orange;
                    }
                    else if (bullConf >= MinConfluenceRequired)
                    {
                        lblAIQ1Name.Text = "AIQ_1 (Yellow)";
                        lblAIQ1Name.Foreground = Brushes.Yellow;
                    }
                    else
                    {
                        // Low confluence - show based on current AIQ1 state
                        lblAIQ1Name.Text = AIQ1_IsUp ? "AIQ_1 (Yellow)" : "AIQ_1 (Orange)";
                        lblAIQ1Name.Foreground = Brushes.Gray;
                    }
                }
                
                if (lblWindowStatus != null)
                {
                    bool inCooldown = false;
                    string cooldownText = "";
                    
                    if (UseTimeBasedCooldown && lastSignalTime != DateTime.MinValue)
                    {
                        double secondsSinceSignal = (DateTime.Now - lastSignalTime).TotalSeconds;
                        inCooldown = secondsSinceSignal < CooldownSeconds;
                        if (inCooldown)
                            cooldownText = $"Cooldown ({secondsSinceSignal:F0}s/{CooldownSeconds}s)";
                    }
                    else
                    {
                        inCooldown = CooldownBars > 0 && barsSinceLastSignal >= 0 && barsSinceLastSignal < CooldownBars;
                        if (inCooldown)
                            cooldownText = $"Cooldown ({barsSinceLastSignal}/{CooldownBars})";
                    }
                    
                    if (inCooldown)
                    {
                        lblWindowStatus.Text = cooldownText;
                        lblWindowStatus.Foreground = Brushes.Yellow;
                    }
                    else if (barsSinceYellowSquare >= 0 && barsSinceYellowSquare <= MaxBarsAfterYellowSquare)
                    {
                        lblWindowStatus.Text = $"LONG Window ({barsSinceYellowSquare}/{MaxBarsAfterYellowSquare})";
                        lblWindowStatus.Foreground = Brushes.Lime;
                    }
                    else if (barsSinceOrangeSquare >= 0 && barsSinceOrangeSquare <= MaxBarsAfterYellowSquare)
                    {
                        lblWindowStatus.Text = $"SHORT Window ({barsSinceOrangeSquare}/{MaxBarsAfterYellowSquare})";
                        lblWindowStatus.Foreground = Brushes.Orange;
                    }
                    else
                    {
                        lblWindowStatus.Text = "Window: CLOSED";
                        lblWindowStatus.Foreground = Brushes.Gray;
                    }
                }

                var (bull, bear, total) = GetConfluence();
                string dailyPnLText = (EnableDailyLossLimit || EnableDailyProfitTarget) ? $" | Day: ${dailyPnL:F0}" : "";
                string limitHitText = dailyLossLimitHit ? " STOPPED" : (dailyProfitTargetHit ? " TARGET" : "");
                if (lblSessionStats != null) lblSessionStats.Text = $"Signals: {signalCount} | Bull:{bull} Bear:{bear}/{total}{dailyPnLText}{limitHitText}";

                if (lblLastSignal != null && signalBorder != null)
                {
                    bool longWindowOpen = barsSinceYellowSquare >= 0 && barsSinceYellowSquare <= MaxBarsAfterYellowSquare;
                    bool shortWindowOpen = barsSinceOrangeSquare >= 0 && barsSinceOrangeSquare <= MaxBarsAfterYellowSquare;
                    
                    if (total == 0)
                    {
                        lblLastSignal.Text = "No indicators selected";
                        lblLastSignal.Foreground = Brushes.Gray;
                        lblLastSignal.FontWeight = FontWeights.Normal;
                        signalBorder.BorderBrush = Brushes.Transparent;
                        signalBorder.Background = Brushes.Transparent;
                    }
                    else if (longWindowOpen && RR_IsUp && bull >= MinConfluenceRequired)
                    {
                        lblLastSignal.Text = $"READY: LONG ({bull}/{total})";
                        lblLastSignal.FontWeight = FontWeights.Bold;
                        lblLastSignal.Foreground = Brushes.Lime;
                        signalBorder.BorderBrush = Brushes.Lime;
                        signalBorder.Background = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
                    }
                    else if (shortWindowOpen && !RR_IsUp && bear >= MinConfluenceRequired)
                    {
                        lblLastSignal.Text = $"READY: SHORT ({bear}/{total})";
                        lblLastSignal.FontWeight = FontWeights.Bold;
                        lblLastSignal.Foreground = Brushes.Orange;
                        signalBorder.BorderBrush = Brushes.Orange;
                        signalBorder.Background = new SolidColorBrush(Color.FromArgb(60, 255, 165, 0));
                    }
                    else if (longWindowOpen)
                    {
                        string waiting = !RR_IsUp ? "RR not UP" : $"Bull {bull}/{MinConfluenceRequired}";
                        lblLastSignal.Text = $"LONG window - {waiting}";
                        lblLastSignal.FontWeight = FontWeights.Normal;
                        lblLastSignal.Foreground = Brushes.Yellow;
                        signalBorder.BorderBrush = Brushes.Yellow;
                        signalBorder.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 0));
                    }
                    else if (shortWindowOpen)
                    {
                        string waiting = RR_IsUp ? "RR not DN" : $"Bear {bear}/{MinConfluenceRequired}";
                        lblLastSignal.Text = $"SHORT window - {waiting}";
                        lblLastSignal.FontWeight = FontWeights.Normal;
                        lblLastSignal.Foreground = Brushes.Orange;
                        signalBorder.BorderBrush = Brushes.Orange;
                        signalBorder.Background = new SolidColorBrush(Color.FromArgb(30, 255, 165, 0));
                    }
                    else if (bull >= MinConfluenceRequired)
                    {
                        lblLastSignal.Text = $"Bull OK ({bull}/{total})\nWaiting for Yellow...";
                        lblLastSignal.FontWeight = FontWeights.Normal;
                        lblLastSignal.Foreground = Brushes.Lime;
                        signalBorder.BorderBrush = Brushes.Lime;
                        signalBorder.Background = Brushes.Transparent;
                    }
                    else if (bear >= MinConfluenceRequired)
                    {
                        lblLastSignal.Text = $"Bear OK ({bear}/{total})\nWaiting for Orange...";
                        lblLastSignal.FontWeight = FontWeights.Normal;
                        lblLastSignal.Foreground = Brushes.Orange;
                        signalBorder.BorderBrush = Brushes.Orange;
                        signalBorder.Background = Brushes.Transparent;
                    }
                    else
                    {
                        lblLastSignal.Text = $"Low confluence (Bull:{bull} Bear:{bear})";
                        lblLastSignal.FontWeight = FontWeights.Normal;
                        lblLastSignal.Foreground = Brushes.Gray;
                        signalBorder.BorderBrush = Brushes.Gray;
                        signalBorder.Background = Brushes.Transparent;
                    }
                }
            });
        }
        
        private void UpdateSignalDisplay(string trigger, int confluenceCount, int total, DateTime t, bool isLong)
        {
            signalCount++;
            string dir = isLong ? "LONG" : "SHORT";
            lastSignalText = $"{dir} @ {confluenceCount}/{total} [{trigger}] {t:HH:mm:ss}";
            if (EnableSoundAlert)
                try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
        }
        
        private void UpdLbl(TextBlock l, bool? v, bool en)
        {
            if (l == null) return;
            if (!en) { l.Text = "OFF"; l.Foreground = Brushes.Gray; }
            else if (!v.HasValue) { l.Text = "MIX"; l.Foreground = Brushes.Yellow; }
            else { l.Text = v.Value ? "UP" : "DN"; l.Foreground = v.Value ? Brushes.Lime : Brushes.Red; }
        }
        #endregion
    }
}
