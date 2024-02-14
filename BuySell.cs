﻿namespace ATAS.Indicators.Technical
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Drawing;
    using System.Net;
    using ATAS.Indicators;
    using ATAS.Indicators.Drawing;
    using ATAS.Indicators.Technical.Properties;
    using OFT.Attributes.Editors;
    using Newtonsoft.Json.Linq;
    using OFT.Rendering.Context;
    using OFT.Rendering.Tools;
    using static ATAS.Indicators.Technical.SampleProperties;

    using Color = System.Drawing.Color;
    using MColor = System.Windows.Media.Color;
    using MColors = System.Windows.Media.Colors;
    using Pen = System.Drawing.Pen;
    using String = String;
    using System.Runtime.ConstrainedExecution;
    using static ATAS.Indicators.Technical.BarTimer;
    using System.Globalization;
    using OFT.Rendering.Settings;
    using System.Windows.Ink;

    [DisplayName("TraderOracle Buy/Sell")]
    public class BuySell : Indicator
    {
        private const String sVersion = "1.41";
        private int iJunk = 0;

        #region PRIVATE FIELDS

        private struct bars
        {
            public String s;
            public int bar;
            public bool top;
        }

        private RenderStringFormat _format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        private List<bars> lsBar = new List<bars>();
        private List<string> lsH = new List<string>();
        private List<string> lsM = new List<string>();

        private readonly PaintbarsDataSeries _paintBars = new("Paint bars");

        private int _lastBar = -1;
        private bool _lastBarCounted;
        private Color lastColor = Color.White;
        private String lastEvil = "";

        // Default TRUE
        private bool bShowTramp = true;          // SHOW
        private bool bShowNews = true;
        private bool bShowUp = true;
        private bool bShowDown = true;
        private bool bUseFisher = true;          // USE
        private bool bUseWaddah = true;
        private bool bUseT3 = true;
        private bool bUseSuperTrend = true;
        private bool bUsePSAR = true;
        private bool bVolumeImbalances = true;

        // Default FALSE
        private bool bShowPNL = false;
        private bool bNewsProcessed = false;     // USE
        private bool bUseSqueeze = false;
        private bool bUseMACD = false;
        private bool bUseKAMA = false;
        private bool bUseMyEMA = false;
        private bool bUseAO = false;

        private bool bShow921 = false;
        private bool bShowSqueeze = false;
        private bool bShowRevPattern = false;
        private bool bShowTripleSupertrend = false;
        private bool bShowCloud = false;
        private bool bAdvanced = false;
        private bool bShowStar = false;
        private bool bShowEvil = true;

        private int iMinDelta = 0;
        private int iMinDeltaPercent = 0;
        private int iMinADX = 0;
        private int iMyEMAPeriod = 21;
        private int iKAMAPeriod = 9;
        private int iOffset = 1;
        private int iFontSize = 10;
        private int iBigTrades = 25000;
        private int iNewsFont = 10;
        private int iWaddaSensitivity = 120;
        private int CandleColoring = 0;

        #endregion

        #region CONSTRUCTOR

        public BuySell() :
            base(true)
        {
            EnableCustomDrawing = true;
            DenyToChangePanel = true;
            SubscribeToDrawingEvents(DrawingLayouts.Historical);

            DataSeries[0] = _posSeries;
            DataSeries.Add(_negSeries);
            DataSeries.Add(_negWhite);
            DataSeries.Add(_posWhite);
            DataSeries.Add(_nine21);
            DataSeries.Add(_squeezie);
            DataSeries.Add(_paintBars);
            DataSeries.Add(_dnTrend);
            DataSeries.Add(_upTrend);
            DataSeries.Add(_upCloud);
            DataSeries.Add(_dnCloud);
            DataSeries.Add(_kamanine);

            Add(_ao);
            Add(_ft);
            Add(_sq);
            Add(_psar);
            Add(_st1);
            Add(_st2);
            Add(_st3);
            Add(_adx);
            Add(_kama9);
            Add(_kama21);
            Add(_atr);
        }

        #endregion

        #region INDICATORS

        private readonly RSI _rsi = new() { Period = 14 };
        private readonly ATR _atr = new() { Period = 14 };
        private readonly AwesomeOscillator _ao = new AwesomeOscillator();
        private readonly ParabolicSAR _psar = new ParabolicSAR();
        private readonly ADX _adx = new ADX() { Period = 10 };
        private readonly EMA _myEMA = new EMA() { Period = 21 };
        private readonly EMA _9 = new EMA() { Period = 9 };
        private readonly EMA _21 = new EMA() { Period = 21 };
        private readonly EMA fastEma = new EMA() { Period = 20 };
        private readonly EMA slowEma = new EMA() { Period = 40 };
        private readonly FisherTransform _ft = new FisherTransform() { Period = 10 };
        private readonly SuperTrend _st1 = new SuperTrend() { Period = 10, Multiplier = 1m };
        private readonly SuperTrend _st2 = new SuperTrend() { Period = 11, Multiplier = 2m };
        private readonly SuperTrend _st3 = new SuperTrend() { Period = 12, Multiplier = 3m };
        private readonly BollingerBands _bb = new BollingerBands() { Period = 20, Shift = 0, Width = 2 };
        private readonly KAMA _kama9 = new KAMA() { ShortPeriod = 2, LongPeriod = 109, EfficiencyRatioPeriod = 9 };
        private readonly KAMA _kama21 = new KAMA() { ShortPeriod = 2, LongPeriod = 109, EfficiencyRatioPeriod = 21 };
        private readonly MACD _macd = new MACD() { ShortPeriod = 12, LongPeriod = 26, SignalPeriod = 9 };
        private readonly T3 _t3 = new T3() { Period = 10, Multiplier = 1 };
        private readonly SqueezeMomentum _sq = new SqueezeMomentum() { BBPeriod = 20, BBMultFactor = 2, KCPeriod = 20, KCMultFactor = 1.5m, UseTrueRange = false };

        #endregion

        #region RENDER CONTEXT

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            if (ChartInfo is null || InstrumentInfo is null)
                return;

            FontSetting Font = new("Arial", iFontSize);
            var renderString = "Howdy";
            var stringSize = context.MeasureString(renderString, Font.RenderObject);
            int x4 = 0;
            int y4 = 0;

            for (var bar = FirstVisibleBarNumber; bar <= LastVisibleBarNumber; bar++)
            {
                renderString = bar.ToString(CultureInfo.InvariantCulture);
                stringSize = context.MeasureString(renderString, Font.RenderObject);

                foreach (bars ix in lsBar)
                {
                    String Evil = EvilTimes(bar);
                    if (Evil != "" && lastEvil != Evil && bShowEvil)
                    {
                        Font.Bold = false;
                        stringSize = context.MeasureString(Evil, Font.RenderObject);
                        x4 = ChartInfo.GetXByBar(bar, false);
                        y4 = Container.Region.Height - stringSize.Height - 40;
                        context.DrawString(Evil, Font.RenderObject, Color.AliceBlue, x4, y4, _format);
                        lastEvil = Evil;
                        Font.Bold = false;
                    }

                    Color bitches = StarTimes(bar);
                    if (bitches != Color.White && lastColor != bitches && bShowStar)
                    {
                        Font.Bold = true;
                        if (bitches == Color.FromArgb(252, 58, 58))
                            renderString = "MANIPULATION";
                        else
                            renderString = "DISTRIBUTION";
                        stringSize = context.MeasureString(renderString, Font.RenderObject);
                        x4 = ChartInfo.GetXByBar(bar, false);
                        y4 = Container.Region.Height - stringSize.Height - 10;
                        context.DrawString(renderString, Font.RenderObject, bitches, x4, y4, _format);
                        lastColor = bitches;
                        Font.Bold = false;
                    }

                    if (ix.bar == bar)
                    {
                        renderString = ix.s.ToString(CultureInfo.InvariantCulture);
                        stringSize = context.MeasureString(renderString, Font.RenderObject);
                        x4 = ChartInfo.GetXByBar(bar, false);
                        y4 = Offset;
                        if (ix.top)
                        {
                            var high = GetCandle(bar).High;
                            y4 += ChartInfo.GetYByPrice(high + (InstrumentInfo.TickSize * Offset) * 2, false);
                            context.DrawString(renderString, Font.RenderObject, Color.Orange, x4, y4, _format);
                        }
                        else
                        {
                            var low = GetCandle(bar).Low;
                            y4 += ChartInfo.GetYByPrice(low - InstrumentInfo.TickSize * Offset, false);
                            context.DrawString(renderString, Font.RenderObject, Color.Lime, x4, y4, _format);
                        }
                        break;
                    }
                }
            }

            var font2 = new RenderFont("Arial", iNewsFont);
            var fontB = new RenderFont("Arial", iNewsFont, FontStyle.Bold);
            int upY = 50;
            int upX = ChartArea.Width - 350;
            int iTrades = 0;

            if (TradingManager.Portfolio != null && bShowPNL)
            {
                var txt1 = $"Account: {TradingManager.Portfolio.AccountID}";
                context.DrawString(txt1, font2, Color.Gray, upX, upY);
                var tsize = context.MeasureString(txt1, font2);

                upY += tsize.Height + 6;
                if (TradingManager.Position != null)
                {
                    tsize = context.MeasureString(txt1, fontB);
                    txt1 = $"Total PNL: {TradingManager.Position.RealizedPnL}";
                    if (TradingManager.Position.RealizedPnL > 0)
                        context.DrawString(txt1, fontB, Color.Lime, upX, upY);
                    else
                        context.DrawString(txt1, fontB, Color.Red, upX, upY);
                }
                upY += tsize.Height + 6;
                var myTrades = TradingManager.MyTrades;
                if (myTrades.Any())
                {
                    foreach (var myTrade in myTrades)
                        iTrades++;
                    tsize = context.MeasureString(txt1, font2);
                    txt1 = $"Total Trades: " + iTrades;
                    context.DrawString(txt1, font2, Color.Gray, upX, upY);
                }
            }

            if (! bShowNews)
                return;

            RenderFont font;
            Size textSize;
            int currY = 40;

            font = new RenderFont("Arial", iNewsFont + 2);
            textSize = context.MeasureString("Today's News:", font);
            context.DrawString("Today's News:", font, Color.YellowGreen, 50, currY);
            currY += textSize.Height + 10;
            font = new RenderFont("Arial", iNewsFont);

            foreach (string s in lsH)
            {
                textSize = context.MeasureString(s, font);
                context.DrawString("High - " + s, font, Color.DarkOrange, 50, currY);
                currY += textSize.Height;
            }
            currY += 9;
            foreach (string s in lsM)
            {
                textSize = context.MeasureString(s, font);
                context.DrawString("Med  - " + s, font, Color.Gray, 50, currY);
                currY += textSize.Height;
            }
        }

        protected void DrawText(int bBar, String strX, Color cI, Color cB, bool bOverride = false, bool bSwap = false)
        {
            var candle = GetCandle(bBar);
            bars ty;

            ty.s = strX;
            ty.bar = bBar;
            if (candle.Close > candle.Open || bOverride)
                ty.top = true;
            else
                ty.top = false;

            if (candle.Close > candle.Open && bSwap)
                ty.top = false;
            else if (candle.Close < candle.Open && bSwap)
                ty.top = true;

            lsBar.Add(ty);

            return;

            decimal _tick = ChartInfo.PriceChartContainer.Step;
            decimal loc = 0;

            if (candle.Close > candle.Open || bOverride)
                loc = candle.High + (_tick * iOffset);
            else
                loc = candle.Low - (_tick * iOffset);

            if (candle.Close > candle.Open && bSwap)
                loc = candle.Low - (_tick * (iOffset * 2));
            else if (candle.Close < candle.Open && bSwap)
                loc = candle.High + (_tick * iOffset);

            AddText("Aver" + bBar, strX, true, bBar, loc, cI, cB, iFontSize, DrawingText.TextAlign.Center);
        }

        #endregion

        #region DATA SERIES

        [Display(Name = "Font Size", GroupName = "Drawing", Order = int.MaxValue)]
        [Range(1, 90)]
        public int TextFont { get => iFontSize; set { iFontSize = value; RecalculateValues(); } }

        [Display(Name = "Text Offset", GroupName = "Drawing", Order = int.MaxValue)]
        [Range(0, 900)]
        public int Offset { get => iOffset; set { iOffset = value; RecalculateValues(); } }

        private ValueDataSeries _kamanine = new("KAMA NINE") { VisualType = VisualMode.Line, Color = DefaultColors.Yellow.Convert(), Width = 5 };
        private RangeDataSeries _upCloud = new("Up Cloud") { RangeColor = MColor.FromArgb(73, 0, 255, 0), DrawAbovePrice = false };
        private RangeDataSeries _dnCloud = new("Down Cloud") { RangeColor = MColor.FromArgb(73, 255, 0, 0), DrawAbovePrice = false };
        private ValueDataSeries _dnTrend = new("Down SuperTrend") { VisualType = VisualMode.Square, Color = DefaultColors.Red.Convert(), Width = 2 };
        private ValueDataSeries _upTrend = new("Up SuperTrend") { Color = DefaultColors.Blue.Convert(), Width = 2, VisualType = VisualMode.Square, ShowZeroValue = false };
        private readonly ValueDataSeries _squeezie = new("Squeeze Relaxer") { Color = MColors.Yellow, VisualType = VisualMode.Dots, Width = 3 };
        private readonly ValueDataSeries _nine21 = new("9 21 cross") { Color = MColor.FromArgb(255, 0, 255, 0), VisualType = VisualMode.Block, Width = 4 };
        private readonly ValueDataSeries _posSeries = new("Regular Buy Signal") { Color = MColor.FromArgb(255, 0, 255, 0), VisualType = VisualMode.UpArrow, Width = 2 };
        private readonly ValueDataSeries _negSeries = new("Regular Sell Signal") { Color = MColor.FromArgb(255, 255, 0, 0), VisualType = VisualMode.DownArrow, Width = 2 };
        private readonly ValueDataSeries _posWhite = new("Vol Imbalance Sell") { Color = MColors.White, VisualType = VisualMode.DownArrow, Width = 1 };
        private readonly ValueDataSeries _negWhite = new("Vol Imbalance Buy") { Color = MColors.White, VisualType = VisualMode.UpArrow, Width = 1 };
        private readonly ValueDataSeries _posRev = new("Top Reversal") { Color = MColors.LightPink, VisualType = VisualMode.Block, Width = 2 };
        private readonly ValueDataSeries _negRev = new("Bottom Reversal") { Color = MColors.LightGreen, VisualType = VisualMode.Block, Width = 2 };

        #endregion

        #region SETTINGS

        private class candleColor : Collection<Entity>
        {
            public candleColor()
                : base(new[]
                {
                    new Entity { Value = 1, Name = "None" },
                    new Entity { Value = 2, Name = "Waddah Explosion" },
                    new Entity { Value = 3, Name = "Squeeze" },
                    new Entity { Value = 4, Name = "Delta" }
                })
            { }
        }
        [Display(Name = "Candle Color", GroupName = "Colored Candles")]
        [ComboBoxEditor(typeof(candleColor), DisplayMember = nameof(Entity.Name), ValueMember = nameof(Entity.Value))]
        public int canColor
        {
            get => CandleColoring; set { if (value < 0) return; CandleColoring = value; RecalculateValues(); }
        }

        [Display(GroupName = "Colored Candles", Name = "Show Reversal Patterns")]
        public bool ShowRevPattern { get => bShowRevPattern; set { bShowRevPattern = value; RecalculateValues(); } }
        [Display(GroupName = "Colored Candles", Name = "Show Advanced Ideas")]
        public bool ShowBrooks { get => bAdvanced; set { bAdvanced = value; RecalculateValues(); } }
        [Display(GroupName = "Colored Candles", Name = "Waddah Sensitivity")]
        [Range(0, 9000)]
        public int WaddaSensitivity
        {
            get => iWaddaSensitivity; set { if (value < 0) return; iWaddaSensitivity = value; RecalculateValues(); }
        }

        [Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "UseAlerts")]
        public bool UseAlerts { get; set; }
        [Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "AlertFile")]
        public string AlertFile { get; set; } = "alert1";

        [Display(GroupName = "Extras", Name = "Show Triple Supertrend")]
        public bool ShowTripleSupertrend { get => bShowTripleSupertrend; set { bShowTripleSupertrend = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show 9/21 EMA Cross")]
        public bool Show_9_21_EMA_Cross { get => bShow921; set { bShow921 = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show Squeeze Relaxer")]
        public bool Show_Squeeze_Relax { get => bShowSqueeze; set { bShowSqueeze = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show Volume Imbalances", Description = "Show gaps between two candles, indicating market strength")] 
        public bool Use_VolumeImbalances { get => bVolumeImbalances; set { bVolumeImbalances = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show Nebula Cloud", Description = "Show cloud containing KAMA 9 and 21")]
        public bool Use_Cloud { get => bShowCloud; set { bShowCloud = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show Trampoline", Description = "Trampoline is the ultimate reversal indicator")]
        public bool Use_Tramp { get => bShowTramp; set { bShowTramp = value; RecalculateValues(); } }

        [Display(GroupName = "Extras", Name = "Show Evil Times", Description = "Market timing from FighterOfEvil, on Discord")]
        public bool ShowEvil { get => bShowEvil; set { bShowEvil = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show Star Times", Description = "Market timing from Star, on Discord")]
        public bool ShowStar { get => bShowStar; set { bShowStar = value; RecalculateValues(); } }

        [Display(GroupName = "High Impact News", Name = "Show today's news")]
        public bool Show_News { get => bShowNews; set { bShowNews = value; RecalculateValues(); } }
        [Display(GroupName = "High Impact News", Name = "Show PNL on screen")]
        public bool Show_PNL { get => bShowPNL; set { bShowPNL = value; RecalculateValues(); } }
        [Display(GroupName = "High Impact News", Name = "News font")]
        [Range(1, 900)]
        public int NewsFont
        { get => iNewsFont; set { iNewsFont = value; RecalculateValues(); } }

        // ========================================================================
        // =======================    FILTER INDICATORS    ========================
        // ========================================================================

        [Display(GroupName = "Buy/Sell Filters", Name = "Waddah Explosion", Description = "The Waddah Explosion must be the correct color, and have a value")]
        public bool Use_Waddah_Explosion { get => bUseWaddah; set { bUseWaddah = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Awesome Oscillator", Description = "AO is positive or negative")]
        public bool Use_Awesome { get => bUseAO; set { bUseAO = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Parabolic SAR", Description = "The PSAR must be signaling a buy/sell signal same as the arrow")]
        public bool Use_PSAR { get => bUsePSAR; set { bUsePSAR = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Squeeze Momentum", Description = "The squeeze must be the correct color")]
        public bool Use_Squeeze_Momentum { get => bUseSqueeze; set { bUseSqueeze = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "MACD", Description = "Standard 12/26/9 MACD crossing in the correct direction")]
        public bool Use_MACD { get => bUseMACD; set { bUseMACD = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "SuperTrend", Description = "Price must align to the current SuperTrend trend")]
        public bool Use_SuperTrend { get => bUseSuperTrend; set { bUseSuperTrend = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "T3", Description = "Price must cross the T3")]
        public bool Use_T3 { get => bUseT3; set { bUseT3 = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Fisher Transform", Description = "Fisher Transform must cross to the correct direction")]
        public bool Use_Fisher_Transform { get => bUseFisher; set { bUseFisher = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Minimum ADX", Description = "Minimum ADX value before showing buy/sell")]
        [Range(0, 100)]
        public int Min_ADX { get => iMinADX; set { if (value < 0) return; iMinADX = value; RecalculateValues(); } }

        [Display(GroupName = "Custom MA Filter", Name = "Use Custom EMA", Description = "Price crosses your own EMA period")]
        public bool Use_Custom_EMA { get => bUseMyEMA; set { bUseMyEMA = value; RecalculateValues(); } }
        [Display(GroupName = "Custom MA Filter", Name = "Custom EMA Period", Description = "Price crosses your own EMA period")]
        [Range(1, 1000)]
        public int Custom_EMA_Period
        {
            get => iMyEMAPeriod;
            set
            {
                if (value < 1)
                    return;
                iMyEMAPeriod = _myEMA.Period = value;
                RecalculateValues();
            }
        }

        [Display(GroupName = "Custom MA Filter", Name = "Use KAMA", Description = "Price crosses KAMA")]
        public bool Use_KAMA { get => bUseKAMA; set { bUseKAMA = value; RecalculateValues(); } }
        [Display(GroupName = "Custom MA Filter", Name = "KAMA Period", Description = "Price crosses KAMA")]
        [Range(1, 1000)]
        public int Custom_KAMA_Period { get => iKAMAPeriod; set { if (value < 1) return; iKAMAPeriod = _kama9.EfficiencyRatioPeriod = value; RecalculateValues(); } }

        private decimal VolSec(IndicatorCandle c) { return c.Volume / Convert.ToDecimal((c.LastTime - c.Time).TotalSeconds); }

        #endregion

        #region Stock HTTP Fetch

        private void ParseStockEvents(String result, int bar)
        {
            int iJSONStart = 0;
            int iJSONEnd = -1;
            String sFinalText = String.Empty; String sNews = String.Empty; String name = String.Empty; String impact = String.Empty; String time = String.Empty; String actual = String.Empty; String previous = String.Empty; String forecast = String.Empty;

            try
            {
                iJSONStart = result.IndexOf("window.calendarComponentStates[1] = ");
                iJSONEnd = result.IndexOf("\"}]}],", iJSONStart);
                sFinalText = result.Substring(iJSONStart, iJSONEnd - iJSONStart);
                sFinalText = sFinalText.Replace("window.calendarComponentStates[1] = ", "");
                sFinalText += "\"}]}]}";

                var jsFile = JObject.Parse(sFinalText);
                foreach (JToken j3 in (JArray)jsFile["days"])
                {
                    JToken j2 = j3.SelectToken("events");
                    foreach (JToken j in j2)
                    {
                        name = j["name"].ToString();
                        impact = j["impactTitle"].ToString();
                        time = j["timeLabel"].ToString();
                        actual = j["actual"].ToString();
                        previous = j["previous"].ToString();
                        forecast = j["forecast"].ToString();
                        sNews = time + "     " + name;
                        if (previous.ToString().Trim().Length > 0)
                            sNews += " (Prev: " + previous + ", Forecast: " + forecast + ")";
                        if (impact.Contains("High"))
                            lsH.Add(sNews);
                        if (impact.Contains("Medium"))
                            lsM.Add(sNews);
                    }
                }
            }
            catch { }
        }

        private void LoadStock(int bar)
        {
            try
            {
                HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create("https://www.forexfactory.com/calendar?day=today");
                myRequest.Method = "GET";
                myRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.150 Safari/537.36";
                WebResponse myResponse = myRequest.GetResponse();
                StreamReader sr = new StreamReader(myResponse.GetResponseStream(), System.Text.Encoding.UTF8);
                string result = sr.ReadToEnd();
                sr.Close();
                myResponse.Close();
                ParseStockEvents(result, bar);
                bNewsProcessed = true;
            }
            catch { }
        }

        #endregion

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0)
            {
                DataSeries.ForEach(x => x.Clear());
                HorizontalLinesTillTouch.Clear();
                _lastBarCounted = false;
                return;
            }
            if (bar < 6)
                return;

            #region CANDLE CALCULATIONS

            var candle = GetCandle(bar - 1);
            var pbar = bar - 1;
            value = candle.Close;
            var chT = ChartInfo.ChartType;

            bShowDown = true;
            bShowUp = true;

            decimal _tick = ChartInfo.PriceChartContainer.Step;
            var p1C = GetCandle(pbar - 1);
            var p2C = GetCandle(pbar - 2);
            var p3C = GetCandle(pbar - 3);
            var p4C = GetCandle(pbar - 4);

            var red = candle.Close < candle.Open;
            var green = candle.Close > candle.Open;
            var c0G = candle.Open < candle.Close;
            var c0R = candle.Open > candle.Close;
            var c1G = p1C.Open < p1C.Close;
            var c1R = p1C.Open > p1C.Close;
            var c2G = p2C.Open < p2C.Close;
            var c2R = p2C.Open > p2C.Close;
            var c3G = p3C.Open < p3C.Close;
            var c3R = p3C.Open > p3C.Close;
            var c4G = p4C.Open < p4C.Close;
            var c4R = p4C.Open > p4C.Close;

            var c0Body = Math.Abs(candle.Close - candle.Open);
            var c1Body = Math.Abs(p1C.Close - p1C.Open);
            var c2Body = Math.Abs(p2C.Close - p2C.Open);
            var c3Body = Math.Abs(p3C.Close - p3C.Open);
            var c4Body = Math.Abs(p4C.Close - p4C.Open);

            var upWickLarger = c0R && Math.Abs(candle.High - candle.Open) > Math.Abs(candle.Low - candle.Close);
            var downWickLarger = c0G && Math.Abs(candle.Low - candle.Open) > Math.Abs(candle.Close - candle.High);

            var ThreeOutUp = c2R && c1G && c0G && p1C.Open < p2C.Close && p2C.Open < p1C.Close && Math.Abs(p1C.Open - p1C.Close) > Math.Abs(p2C.Open - p2C.Close) && candle.Close > p1C.Low;

            var ThreeOutDown = c2G && c1R && c0R && p1C.Open > p2C.Close && p2C.Open > p1C.Close && Math.Abs(p1C.Open - p1C.Close) > Math.Abs(p2C.Open - p2C.Close) && candle.Close < p1C.Low;

            if (bVolumeImbalances)
            {
                var highPen = new Pen(new SolidBrush(Color.RebeccaPurple)) { Width = 2 };
                if (green && c1G && candle.Open > p1C.Close)
                {
                    HorizontalLinesTillTouch.Add(new LineTillTouch(pbar, candle.Open, highPen));
                    _negWhite[pbar] = candle.Low - (_tick * 2);
                }
                if (red && c1R && candle.Open < p1C.Close)
                {
                    HorizontalLinesTillTouch.Add(new LineTillTouch(pbar, candle.Open, highPen));
                    _posWhite[pbar] = candle.High + (_tick * 2);
                }
            }

            #endregion

            #region INDICATORS CALCULATE

            _myEMA.Calculate(pbar, value);
            _t3.Calculate(pbar, value);
            fastEma.Calculate(pbar, value);
            slowEma.Calculate(pbar, value);
            _9.Calculate(pbar, value);
            _21.Calculate(pbar, value);
            _macd.Calculate(pbar, value);
            _bb.Calculate(pbar, value);
            _rsi.Calculate(pbar, value);

            var ao = ((ValueDataSeries)_ao.DataSeries[0])[pbar];
            var kama9 = ((ValueDataSeries)_kama9.DataSeries[0])[pbar];
            var kama21 = ((ValueDataSeries)_kama9.DataSeries[0])[pbar];
            var m1 = ((ValueDataSeries)_macd.DataSeries[0])[pbar];
            var m2 = ((ValueDataSeries)_macd.DataSeries[1])[pbar];
            var m3 = ((ValueDataSeries)_macd.DataSeries[2])[pbar];
            var t3 = ((ValueDataSeries)_t3.DataSeries[0])[pbar];
            var fast = ((ValueDataSeries)fastEma.DataSeries[0])[pbar];
            var fastM = ((ValueDataSeries)fastEma.DataSeries[0])[pbar - 1];
            var slow = ((ValueDataSeries)slowEma.DataSeries[0])[pbar];
            var slowM = ((ValueDataSeries)slowEma.DataSeries[0])[pbar - 1];
            var sq1 = ((ValueDataSeries)_sq.DataSeries[0])[pbar];
            var sq2 = ((ValueDataSeries)_sq.DataSeries[1])[pbar];
            var psq1 = ((ValueDataSeries)_sq.DataSeries[0])[pbar - 1];
            var psq2 = ((ValueDataSeries)_sq.DataSeries[1])[pbar - 1];
            var ppsq1 = ((ValueDataSeries)_sq.DataSeries[0])[pbar - 2];
            var ppsq2 = ((ValueDataSeries)_sq.DataSeries[1])[pbar - 2];
            var f1 = ((ValueDataSeries)_ft.DataSeries[0])[pbar];
            var f2 = ((ValueDataSeries)_ft.DataSeries[1])[pbar];
            var stu1 = ((ValueDataSeries)_st1.DataSeries[0])[pbar];
            var stu2 = ((ValueDataSeries)_st2.DataSeries[0])[pbar];
            var stu3 = ((ValueDataSeries)_st3.DataSeries[0])[pbar];
            var std1 = ((ValueDataSeries)_st1.DataSeries[1])[pbar];
            var std2 = ((ValueDataSeries)_st2.DataSeries[1])[pbar];
            var std3 = ((ValueDataSeries)_st3.DataSeries[1])[pbar];
            var x = ((ValueDataSeries)_adx.DataSeries[0])[pbar];
            var nn = ((ValueDataSeries)_9.DataSeries[0])[pbar];
            var prev_nn = ((ValueDataSeries)_9.DataSeries[0])[pbar - 1];
            var twone = ((ValueDataSeries)_21.DataSeries[0])[pbar];
            var prev_twone = ((ValueDataSeries)_21.DataSeries[0])[pbar - 1];
            var myema = ((ValueDataSeries)_myEMA.DataSeries[0])[pbar];
            var psar = ((ValueDataSeries)_psar.DataSeries[0])[pbar];
            var bb_mid = ((ValueDataSeries)_bb.DataSeries[0])[pbar]; // mid
            var bb_top = ((ValueDataSeries)_bb.DataSeries[1])[pbar]; // top
            var bb_bottom = ((ValueDataSeries)_bb.DataSeries[2])[pbar]; // bottom
            var rsi = ((ValueDataSeries)_rsi.DataSeries[0])[pbar];
            var rsi1 = ((ValueDataSeries)_rsi.DataSeries[0])[pbar - 1];
            var rsi2 = ((ValueDataSeries)_rsi.DataSeries[0])[pbar - 2];

            var fisherUp = (f1 < f2);
            var fisherDown = (f2 < f1);
            var macdUp = (m1 > m2);
            var macdDown = (m1 < m2);
            var psarBuy = (psar < candle.Close);
            var psarSell = (psar > candle.Close);

            #endregion

            var eqHigh = c0R && c1R && c2G && c3G && (p1C.High > bb_top || p2C.High > bb_top) &&
                candle.Close < p1C.Close &&
                (p1C.Open == p2C.Close || p1C.Open == p2C.Close + _tick || p1C.Open + _tick == p2C.Close);

            var eqLow = c0G && c1G && c2R && c3R && (p1C.Low < bb_bottom || p2C.Low < bb_bottom) &&
                candle.Close > p1C.Close &&
                (p1C.Open == p2C.Close || p1C.Open == p2C.Close + _tick || p1C.Open + _tick == p2C.Close);

            var t1 = ((fast - slow) - (fastM - slowM)) * iWaddaSensitivity;

            // ====================    SHOW/OTHER CONDITIONS    =======================

            if (bShowTripleSupertrend)
            {
                var atr = _atr[pbar];
                var median = (candle.Low + candle.High) / 2;
                var dUpperLevel = median + atr * 1.7m;
                var dLowerLevel = median - atr * 1.7m;

                if ((std1 != 0 && std2 != 0) || (std3 != 0 && std2 != 0) || (std3 != 0 && std1 != 0))
                    _dnTrend[pbar] = dUpperLevel;
                else if ((stu1 != 0 && stu2 != 0) || (stu3 != 0 && stu2 != 0) || (stu1 != 0 && stu3 != 0))
                    _upTrend[pbar] = dLowerLevel;
            }

            // Squeeze momentum relaxer show
            if (sq1 > 0 && sq1 < psq1 && psq1 > ppsq1 && bShowSqueeze)
                _squeezie[pbar] = candle.High + _tick * 4;
            if (sq1 < 0 && sq1 > psq1 && psq1 < ppsq1 && bShowSqueeze)
                _squeezie[pbar] = candle.Low - _tick * 4;

            // 9/21 cross show
            if (nn > twone && prev_nn <= prev_twone && bShow921)
                DrawText(pbar, "X", Color.Yellow, Color.Transparent, false, true);
            if (nn < twone && prev_nn >= prev_twone && bShow921)
                DrawText(pbar, "X", Color.Yellow, Color.Transparent, false, true);

            if (bAdvanced)
            {
                if (c4Body > c3Body && c3Body > c2Body && c2Body > c1Body && c1Body > c0Body)
                    if ((candle.Close > p1C.Close && p1C.Close > p2C.Close && p2C.Close > p3C.Close) ||
                    (candle.Close < p1C.Close && p1C.Close < p2C.Close && p2C.Close < p3C.Close))
                        DrawText(pbar, "Stairs", Color.Yellow, Color.Transparent);

                if (eqHigh)
                    DrawText(pbar - 1, "Equal\nHigh", Color.Lime, Color.Transparent, false, true);
                if (eqLow)
                    DrawText(pbar - 1, "Equal\nLow", Color.Yellow, Color.Transparent, false, true);
            }

            // ========================    UP CONDITIONS    ===========================

            if ((candle.Delta < iMinDelta) || (!macdUp && bUseMACD) || (psarSell && bUsePSAR) || (!fisherUp && bUseFisher) || (value < t3 && bUseT3) || (value < kama9 && bUseKAMA) || (value < myema && bUseMyEMA) || (t1 < 0 && bUseWaddah) || (ao < 0 && bUseAO) || (stu2 == 0 && bUseSuperTrend) || (sq1 < 0 && bUseSqueeze) || (x < iMinADX))
                bShowUp = false;

            if (green && bShowUp)
                _posSeries[pbar] = candle.Low - (_tick * 2);

            // ========================    DOWN CONDITIONS    =========================

            if ((candle.Delta > (iMinDelta * -1)) || (psarBuy && bUsePSAR) || (!macdDown && bUseMACD) || (!fisherDown && bUseFisher) || (value > kama9 && bUseKAMA) || (value > t3 && bUseT3) || (value > myema && bUseMyEMA) || (t1 >= 0 && bUseWaddah) || (ao > 0 && bUseAO) || (std2 == 0 && bUseSuperTrend) || (sq1 > 0 && bUseSqueeze) || (x < iMinADX))
                bShowDown = false;

            if (red && bShowDown)
                _negSeries[pbar] = candle.High + _tick * 2;

            if (_lastBar != bar)
            {
                if (_lastBarCounted && UseAlerts)
                {
                    if (bVolumeImbalances)
                        if ((green && c1G && candle.Open > p1C.Close) || (red && c1R && candle.Open < p1C.Close))
                            AddAlert(AlertFile, "Volume Imbalance");

                    if (bShowUp)
                        AddAlert(AlertFile, "BUY Signal");
                    else if (bShowDown)
                        AddAlert(AlertFile, "BUY Signal");
                }
                _lastBar = bar;
            }
            else
            {
                if (!_lastBarCounted)
                    _lastBarCounted = true;
            }

            var waddah = Math.Min(Math.Abs(t1) + 70, 255);
            if (canColor == 2) // (bWaddahCandles)
                _paintBars[pbar] = t1 > 0 ? MColor.FromArgb(255, 0, (byte)waddah, 0) : MColor.FromArgb(255, (byte)waddah, 0, 0);

            var filteredSQ = Math.Min(Math.Abs(sq1 * 25), 255);
            if (canColor == 3) // (bSqueezeCandles)
                _paintBars[pbar] = sq1 > 0 ? MColor.FromArgb(255, 0, (byte)filteredSQ, 0) : MColor.FromArgb(255, (byte)filteredSQ, 0, 0);

            var filteredDelta = Math.Min(Math.Abs(candle.Delta), 255);
            if (canColor == 4) // (bDeltaCandles)
                _paintBars[pbar] = candle.Delta > 0 ? MColor.FromArgb(255, 0, (byte)filteredDelta, 0) : MColor.FromArgb(255, (byte)filteredDelta, 0, 0);

            // =======================    REVERSAL PATTERNS    ========================

            if (bShowRevPattern)
            {
                if (c0R && candle.High > bb_top && candle.Open < bb_top && candle.Open > p1C.Close && upWickLarger)
                    DrawText(pbar, "Wick", Color.Yellow, Color.Transparent);
                if (c0G && candle.Low < bb_bottom && candle.Open > bb_bottom && candle.Open > p1C.Close && downWickLarger)
                    DrawText(pbar, "Wick", Color.Yellow, Color.Transparent);

                if (c0G && c1R && c2R && VolSec(p1C) > VolSec(p2C) && VolSec(p2C) > VolSec(p3C) && candle.Delta < 0)
                    DrawText(pbar, "Vol\nRev", Color.Yellow, Color.Transparent, false, true);
                if (c0R && c1G && c2G && VolSec(p1C) > VolSec(p2C) && VolSec(p2C) > VolSec(p3C) && candle.Delta > 0)
                    DrawText(pbar, "Vol\nRev", Color.Lime, Color.Transparent, false, true);

                // _paintBars[bar] = Colors.Yellow;
                if (ThreeOutUp)
                    DrawText(pbar, "3oU", Color.Yellow, Color.Transparent);
                if (ThreeOutDown && bShowRevPattern)
                    DrawText(pbar, "3oD", Color.Yellow, Color.Transparent);
            }

            // Nebula cloud
            if (bShowCloud)
                if (_kama9[pbar] > _kama21[pbar])
                {
                    _upCloud[pbar].Upper = _kama9[pbar];
                    _upCloud[pbar].Lower = _kama21[pbar];
                }
                else
                {
                    _dnCloud[pbar].Upper = _kama21[pbar];
                    _dnCloud[pbar].Lower = _kama9[pbar];
                }

            // Trampoline
            if (bShowTramp)
            {
                if (c0R && c1R && candle.Close < p1C.Close && (rsi >= 70 || rsi1 >= 70 || rsi2 >= 70) &&
                    c2G && p2C.High >= (bb_top - (_tick * 30)))
                    DrawText(pbar, "TR", Color.Yellow, Color.BlueViolet, false, true);
                if (c0G && c1G && candle.Close > p1C.Close && (rsi < 25 || rsi1 < 25 || rsi2 < 25) &&
                    c2R && p2C.Low <= (bb_bottom + (_tick * 30)))
                    DrawText(pbar, "TR", Color.Yellow, Color.BlueViolet, false, true);
            }

            if (!bNewsProcessed && bShowNews)
                LoadStock(pbar);

        }

        private String EvilTimes(int bar)
        {
            var candle = GetCandle(bar);
            var diff = InstrumentInfo.TimeZone;
            var time = candle.Time.AddHours(diff);

            if (time.Hour == 9 && time.Minute >= 00 && time.Minute <= 59)
                return "Market Pivot";

            if (time.Hour == 10 && time.Minute >= 00 && time.Minute <= 29)
                return "Euro Move";

            if (time.Hour == 10 && time.Minute >= 30 && time.Minute <= 59)
                return "Inverse";

            if (time.Hour == 11 && time.Minute >= 00 && time.Minute <= 59)
                return "Inverse ";

            if (time.Hour == 12 && time.Minute >= 00 && time.Minute <= 59)
                return "Bond Auctions";

            if (time.Hour == 13 && time.Minute >= 29 && time.Minute <= 59)
                return "Capital Injection";

            if (time.Hour == 14 && time.Minute >= 29 && time.Minute <= 59)
                return "Capital Injection";

            if (time.Hour == 14 && time.Minute >= 49 && time.Minute <= 59)
                return "Rug Pull";

            return "";
        }

        private Color StarTimes(int bar)
        {
            var candle = GetCandle(bar);
            var diff = InstrumentInfo.TimeZone;
            var time = candle.Time.AddHours(diff);

            // Manipulation
            if (
                (time.Hour == 8 && time.Minute >= 47 && time.Minute <= 59) ||
                (time.Hour == 9 && time.Minute >= 00 && time.Minute <= 11) ||
                (time.Hour == 10 && time.Minute >= 10 && time.Minute <= 26) ||
                (time.Hour == 11 && time.Minute >= 07 && time.Minute <= 19) ||
                (time.Hour == 11 && time.Minute >= 55 && time.Minute <= 59) ||
                (time.Hour == 12 && time.Minute >= 00 && time.Minute <= 07)
                )
                return Color.FromArgb(252, 58, 58);

            // Distribution
            if (
                (time.Hour == 9 && time.Minute >= 11 && time.Minute <= 47) ||
                (time.Hour == 10 && time.Minute >= 26 && time.Minute <= 50) ||
                (time.Hour == 11 && time.Minute >= 19 && time.Minute <= 37) ||
                (time.Hour == 12 && time.Minute >= 07 && time.Minute <= 25)
                )
                return Color.FromArgb(78, 152, 242);

            return Color.White;
        }

    }
}
