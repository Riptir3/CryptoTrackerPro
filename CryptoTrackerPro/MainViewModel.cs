using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using Microsoft.ML;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoTrackerPro.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ICryptoProvider _cryptoProvider;
        private readonly MLContext _mlContext = new();

        public ObservableCollection<double> PriceHistory { get; } = new();
        public ObservableCollection<double> SmoothedPrices { get; } = new();
        public ObservableCollection<LogEntry> EventLog { get; } = new();

        private DateTime _lastNotificationTime = DateTime.MinValue;
        private DateTime _lastAiRun = DateTime.MinValue;
        private bool _isSwitching = false;
        private bool _isFirstPrice = true;
        private string _lastTrend = "";

        // --- VEZÉRLŐK ÉS SZIMULÁTOR ---
        [ObservableProperty] private bool _isPaused = false;
        [ObservableProperty] private int _visiblePointCount = 50;

        [ObservableProperty] private double _virtualBalance = 10000; // Kezdő egyenleg: 10,000 USDT
        [ObservableProperty] private double _cryptoInventory = 0;    // Birtokolt mennyiség
        [ObservableProperty] private string _portfolioValue = "10000.00 USDT";

        // Tengelyek
        public Axis[] XAxes { get; set; } = { new Axis { IsVisible = true, LabelsPaint = new SolidColorPaint(SKColors.Gray), TextSize = 10 } };
        public Axis[] YAxes { get; set; } = { new Axis { Labeler = v => v.ToString("N2"), TextSize = 12, LabelsPaint = new SolidColorPaint(SKColors.Gray) } };

        public ObservableCollection<string> Symbols { get; } = new() { "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT" };

        [ObservableProperty] private string _selectedSymbol = "BTCUSDT";
        [ObservableProperty] private double _alertLevel = 60000;
        [ObservableProperty] private double _currentPrice;

        // Statisztikák és AI
        [ObservableProperty] private double _averagePrice;
        [ObservableProperty] private double _volatility;
        [ObservableProperty] private double _rsiValue;
        [ObservableProperty] private string _marketSentiment = "Elemzés...";
        [ObservableProperty] private double _predictedPrice;
        [ObservableProperty] private string _trendDirection = "Várakozás...";
        [ObservableProperty] private double _upperBand;
        [ObservableProperty] private double _lowerBand;
        [ObservableProperty] private double _yMin;
        [ObservableProperty] private double _yMax;

        public ObservableCollection<ISeries> Series { get; set; }
        public ObservableCollection<RectangularSection> Sections { get; set; }

        public MainViewModel(ICryptoProvider cryptoProvider)
        {
            _cryptoProvider = cryptoProvider;

            Series = new ObservableCollection<ISeries> {
                new StepLineSeries<double> {
                    Values = SmoothedPrices,
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColors.SpringGreen, 2),
                    Fill = null,
                    AnimationsSpeed = TimeSpan.Zero
                }
            };

            Sections = new ObservableCollection<RectangularSection> {
                new RectangularSection {
                    Yi = 0, Yj = AlertLevel,
                    Fill = new SolidColorPaint(new SKColor(255, 0, 0, 30)),
                    Stroke = new SolidColorPaint(SKColors.Red, 1) { PathEffect = new DashEffect(new float[] { 5, 5 }) }
                }
            };

            _cryptoProvider.PriceUpdated += OnPriceReceived;
            _ = _cryptoProvider.StartStreamingAsync(SelectedSymbol);

            AddLog("Rendszer aktív.", "SpringGreen");
        }

        // --- PARANCSOK (COMMANDS) ---

        [RelayCommand]
        private void TogglePause()
        {
            IsPaused = !IsPaused;
            AddLog(IsPaused ? "SZÜNET: Monitorozás megállítva" : "FOLYTATÁS: Monitorozás elindítva", IsPaused ? "Orange" : "SpringGreen");
        }

        [RelayCommand]
        private void ExportLog()
        {
            var sfd = new SaveFileDialog { Filter = "Text Files (*.txt)|*.txt", FileName = $"Log_{SelectedSymbol}_{DateTime.Now:yyyyMMdd_HHmm}" };
            if (sfd.ShowDialog() == true)
            {
                var content = EventLog.Select(l => $"[{l.Time}] {l.Message}");
                File.WriteAllLines(sfd.FileName, content);
                AddLog("Napló exportálva!", "Lime");
            }
        }

        [RelayCommand]
        private void BuyCrypto()
        {
            if (VirtualBalance >= 10) // Minimum 10 USDT a vételhez
            {
                double amount = VirtualBalance / CurrentPrice;
                CryptoInventory += amount;
                VirtualBalance = 0;
                SoundHelper.PlayTrade();
                AddLog($"VÉTEL: {amount:N4} {SelectedSymbol.Replace("USDT", "")}", "SpringGreen");
                UpdatePortfolioDisplay();
            }
        }

        [RelayCommand]
        private void SellCrypto()
        {
            if (CryptoInventory > 0)
            {
                double gain = CryptoInventory * CurrentPrice;
                VirtualBalance += gain;
                AddLog($"ELADÁS: {gain:N2} USDT", "Orange");
                CryptoInventory = 0;
                SoundHelper.PlayTrade();
                UpdatePortfolioDisplay();
            }
        }

        // --- LOGIKA ---

        private void OnPriceReceived(double price)
        {
            if (_isSwitching) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentPrice = price;
                PriceHistory.Add(price);
                if (PriceHistory.Count > 200) PriceHistory.RemoveAt(0);

                if (!IsPaused)
                {
                    SmoothedPrices.Add(price);
                    if (SmoothedPrices.Count > 200) SmoothedPrices.RemoveAt(0);

                    XAxes[0].MaxLimit = SmoothedPrices.Count;
                    XAxes[0].MinLimit = Math.Max(0, SmoothedPrices.Count - VisiblePointCount);

                    double margin = price * 0.0015;
                    if (price >= YMax || price <= YMin || YMax == 0)
                    {
                        YMax = price + margin; YMin = price - margin;
                        YAxes[0].MaxLimit = YMax; YAxes[0].MinLimit = YMin;
                    }

                    if (PriceHistory.Count > 20) UpdateMarketStats();

                    if (PriceHistory.Count > 50 && (DateTime.Now - _lastAiRun).TotalSeconds > 15)
                    {
                        _lastAiRun = DateTime.Now;
                        var snapshot = PriceHistory.TakeLast(50).ToList();
                        Task.Run(() => {
                            var pred = CalculateAiPrediction(snapshot);
                            Application.Current.Dispatcher.Invoke(() => {
                                PredictedPrice = Math.Round(pred, 2);
                                TrendDirection = PredictedPrice > CurrentPrice ? "Emelkedő 📈" : "Csökkenő 📉";
                            });
                        });
                    }
                }

                if (_isFirstPrice) { AlertLevel = Math.Round(price * 0.95, 2); _isFirstPrice = false; }
                CheckAlerts(price);
                UpdatePortfolioDisplay();
            });
        }

        private void UpdateMarketStats()
        {
            var avg = CryptoStats.CalculateAverage(PriceHistory);
            var vol = CryptoStats.CalculateVolatility(PriceHistory);

            AveragePrice = Math.Round(avg, 2);
            Volatility = Math.Round(vol, 2);
            RsiValue = Math.Round(CryptoStats.CalculateRSI(PriceHistory), 1);
            UpperBand = Math.Round(avg + (vol * 2), 2);
            LowerBand = Math.Round(avg - (vol * 2), 2);

            MarketSentiment = RsiValue switch { > 70 => "Túlvett 🔴", < 30 => "Túladott 🟢", _ => "Semleges ⚪" };

            if (!IsPaused)
            {
                if (Sections.Count < 2 && LowerBand > 0)
                    Sections.Add(new RectangularSection { Fill = new SolidColorPaint(new SKColor(0, 255, 127, 15)) });

                if (Sections.Count >= 2)
                {
                    Sections[1].Yi = LowerBand;
                    Sections[1].Yj = UpperBand;
                }
            }
        }

        private void UpdatePortfolioDisplay()
        {
            double total = VirtualBalance + (CryptoInventory * CurrentPrice);
            PortfolioValue = $"{total:N2} USDT";
        }

        private void AddLog(string message, string color = "White")
        {
            if (Application.Current == null) return;
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                EventLog.Insert(0, new LogEntry { Message = message, Color = color });
                if (EventLog.Count > 30) EventLog.RemoveAt(30);
            }));
        }

        private void CheckAlerts(double price)
        {
            if (price < AlertLevel)
            {
                if ((DateTime.Now - _lastNotificationTime).TotalMinutes > 3)
                {
                    SoundHelper.PlayAlert();
                    ShowNotification($"{SelectedSymbol} limit alatt!");
                }
            }

            if (TrendDirection != _lastTrend && !string.IsNullOrEmpty(TrendDirection) && TrendDirection != "Várakozás...")
            {
                AddLog($"AI Jelzés: {TrendDirection}", "SkyBlue");
                _lastTrend = TrendDirection;
            }
        }

        private double CalculateAiPrediction(List<double> list)
        {
            try
            {
                double refP = list.Last();
                var data = list.Select((p, i) => new CryptoData { TimeIndex = (float)i, Price = (float)(p - refP) }).ToList();
                var trainingData = _mlContext.Data.LoadFromEnumerable(data);
                var pipeline = _mlContext.Transforms.Concatenate("Features", "TimeIndex").Append(_mlContext.Regression.Trainers.LbfgsPoissonRegression(labelColumnName: "Price"));
                var model = pipeline.Fit(trainingData);
                var engine = _mlContext.Model.CreatePredictionEngine<CryptoData, CryptoPrediction>(model);
                return refP + (double)engine.Predict(new CryptoData { TimeIndex = (float)data.Count }).PredictedPrice;
            }
            catch { return list.Last(); }
        }

        partial void OnSelectedSymbolChanged(string value)
        {
            _isSwitching = true;
            YMax = 0;
            Task.Run(async () => {
                await _cryptoProvider.StopStreamingAsync();
                Application.Current.Dispatcher.Invoke(() => {
                    PriceHistory.Clear(); SmoothedPrices.Clear(); _isFirstPrice = true;
                    AddLog($"Váltás: {value}", "#F7931A");
                });
                await Task.Delay(400);
                await _cryptoProvider.StartStreamingAsync(value);
                _isSwitching = false;
            });
        }

        partial void OnVisiblePointCountChanged(int value)
        {
            if (SmoothedPrices.Count > 0)
            {
                XAxes[0].MinLimit = Math.Max(0, SmoothedPrices.Count - value);
                XAxes[0].MaxLimit = SmoothedPrices.Count;
            }
        }

        partial void OnAlertLevelChanged(double value) { if (Sections?.Count > 0) Sections[0].Yj = value; }

        private void ShowNotification(string message)
        {
            if ((DateTime.Now - _lastNotificationTime).TotalMinutes < 3) return;
            WeakReferenceMessenger.Default.Send(new CryptoAlertMessage(message));
            _lastNotificationTime = DateTime.Now;
            AddLog("!!! RIASZTÁS !!!", "Red");
        }
    }

    // --- SEGÉDOSZTÁLYOK ---

    public class CryptoData { public float TimeIndex { get; set; } public float Price { get; set; } }
    public class CryptoPrediction { [Microsoft.ML.Data.ColumnName("Score")] public float PredictedPrice { get; set; } }
    public class LogEntry { public string Time { get; set; } = DateTime.Now.ToString("HH:mm:ss"); public string Message { get; set; } public string Color { get; set; } }

    public static class SoundHelper
    {
        public static void PlayAlert() { try { SystemSounds.Exclamation.Play(); } catch { } }
        public static void PlayTrade() { try { SystemSounds.Asterisk.Play(); } catch { } }
    }
}