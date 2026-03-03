using CommunityToolkit.Mvvm.ComponentModel;
using CryptoTrackerPro;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using System.Collections.ObjectModel;

public partial class MainViewModel : ObservableObject
{
    private readonly ICryptoProvider _cryptoProvider;
    public ObservableCollection<double> PriceHistory { get; } = new();
    private DateTime _lastNotificationTime = DateTime.MinValue;

    public ObservableCollection<string> Symbols { get; } = new() { "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT" };

    [ObservableProperty] private string _selectedSymbol = "BTCUSDT";
    [ObservableProperty] private double _alertLevel = 60000;
    [ObservableProperty] private double _currentPrice;

    private bool _isFirstPrice = true;

    public ObservableCollection<ISeries> Series { get; set; }
    public ObservableCollection<RectangularSection> Sections { get; set; }

    public MainViewModel(ICryptoProvider cryptoProvider)
    {
        _cryptoProvider = cryptoProvider;

        Series = new ObservableCollection<ISeries> {
            new LineSeries<double> {
                Values = PriceHistory,
                Fill = null,
                GeometrySize = 0,
                Stroke = new SolidColorPaint(SKColors.SpringGreen, 2)
            }
        };

        Sections = new ObservableCollection<RectangularSection> {
        new RectangularSection {
        Yi = 0,
        Yj = AlertLevel,
        Fill = new SolidColorPaint(new SKColor(255, 0, 0, 40)), 
        Stroke = new SolidColorPaint(SKColors.Red, 1) {
            PathEffect = new DashEffect(new float[] { 5, 5 })
        }
    }
};

        _ = StartDataFetch();
    }

    partial void OnSelectedSymbolChanged(string value)
    {
        PriceHistory.Clear();
        _isFirstPrice = true;
    }

    partial void OnAlertLevelChanged(double value)
    {
        if (Sections?.Count > 0)
        {
            Sections[0].Yj = value;
        }
    }

    private async Task StartDataFetch()
    {
        while (true)
        {
            try
            {
                var price = await _cryptoProvider.GetPriceAsync(SelectedSymbol);

                App.Current.Dispatcher.Invoke(() =>
                {
                    CurrentPrice = price;

                    if (_isFirstPrice)
                    {
                        AlertLevel = Math.Round(price * 0.9, 2);

                        if (Sections != null && Sections.Count > 0)
                        {
                            Sections[0].Yj = AlertLevel;
                        }

                        _isFirstPrice = false; 
                    }

                    PriceHistory.Add(CurrentPrice);
                    if (PriceHistory.Count > 50) PriceHistory.RemoveAt(0);
                });

                if (CurrentPrice < AlertLevel)
                {
                    ShowNotification($"{SelectedSymbol} ára a beállított limit ({AlertLevel}) alá esett!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hiba: {ex.Message}");
            }
            await Task.Delay(2000);
        }
    }

    private void ShowNotification(string message)
    {
        if ((DateTime.Now - _lastNotificationTime).TotalMinutes < 5)
            return;

        App.Current.Dispatcher.Invoke(() => {
            var mainWindow = App.Current.MainWindow as MainWindow;

            mainWindow?.MyNotifyIcon.ShowNotification(
                title: "Kripto Riasztás",
                message: message
            );

            _lastNotificationTime = DateTime.Now;
        });
    }
}