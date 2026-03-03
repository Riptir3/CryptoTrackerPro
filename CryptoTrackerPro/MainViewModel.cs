using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CryptoTrackerPro;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Windows;

public partial class MainViewModel : ObservableObject
{
    private readonly ICryptoProvider _cryptoProvider;
    public ObservableCollection<double> PriceHistory { get; } = new();
    private DateTime _lastNotificationTime = DateTime.MinValue;
    private bool _isSwitching = false;

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

        _cryptoProvider.PriceUpdated += OnPriceReceived;

        _ = _cryptoProvider.StartStreamingAsync(SelectedSymbol);
    }

    private void OnPriceReceived(double price)
    {
        if (_isSwitching) return;

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            CurrentPrice = price;

            if (_isFirstPrice)
            {
                AlertLevel = Math.Round(price * 0.9, 2);
                _isFirstPrice = false;
            }

            PriceHistory.Add(CurrentPrice);

            if (PriceHistory.Count > 100) PriceHistory.RemoveAt(0);

            if (CurrentPrice < AlertLevel)
            {
                ShowNotification($"{SelectedSymbol} ára a beállított limit ({AlertLevel}) alá esett!");
            }
        });
    }

    partial void OnSelectedSymbolChanged(string value)
    {
        _isSwitching = true; 

        Task.Run(async () =>
        {
            try
            {
                await _cryptoProvider.StopStreamingAsync();

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    PriceHistory.Clear();
                    _isFirstPrice = true;
                    CurrentPrice = 0;
                }));

                await Task.Delay(200);
                await _cryptoProvider.StartStreamingAsync(value);

                _isSwitching = false; 
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hiba váltáskor: {ex.Message}");
                _isSwitching = false;
            }
        });
    }

    partial void OnAlertLevelChanged(double value)
    {
        if (Sections?.Count > 0)
        {
            Sections[0].Yj = value;
        }
    }

    private void ShowNotification(string message)
    {
        if ((DateTime.Now - _lastNotificationTime).TotalMinutes < 5)
            return;

        WeakReferenceMessenger.Default.Send(new CryptoAlertMessage(message));
        _lastNotificationTime = DateTime.Now;
    }
}