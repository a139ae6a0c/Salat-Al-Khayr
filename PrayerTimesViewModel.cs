using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Al_Khayr_Salat.Functions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ManagedBass;

using static Al_Khayr_Salat.MainWindow;

namespace Al_Khayr_Salat;

public class PrayerTimesViewModel : BaseViewModel
{
    public static PrayerTimesViewModel Current { get; private set; }
    private static readonly object AudioLock = new();

    private static int _streamHandle = 0;
    private SyncProcedure _endTrackSync; // Prevents the AOT Garbage Collector from crashing the app
    private static readonly HttpClient _httpClient = new HttpClient();
    
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions 
    { 
        WriteIndented = true,
        TypeInfoResolver = PrayerTimeJsonContext.Default 
    };
    
    private static readonly ISolidColorBrush WhiteTextBrush = new SolidColorBrush(Colors.White);
    private static readonly ISolidColorBrush BlackTextBrush = new SolidColorBrush(Colors.Black);
    private static readonly ISolidColorBrush DefaultBackgroundBrush = new SolidColorBrush(Color.Parse("#191919"));
    private static readonly IBrush HighlightBackgroundBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = new GradientStops
        {
            new GradientStop { Color = Color.Parse("#f6b162"), Offset = 0 },
            new GradientStop { Color = Color.Parse("#f9f871"), Offset = 1 }
        }
    };
    
    private TextBlock? _cachedUpdaterBlock;
    private Border[]? _cachedBorders;

    private bool _adhanPlayed;
    private string _lastPrayerTime = string.Empty;
    private string _lastFetchDate = string.Empty; // Tracks when we last fetched
    private DateTime[] _prayerTimes = new DateTime[5];

    public int nextPrayerIndex = 5;
    public DateTime nextPrayerTime;
    
    private int _lastCheckedMinute = -1;
    private int _lastHighlightedIndex = -1;

    public ObservableCollection<prayerTime> PrayerTimes { get; }

    public PrayerTimesViewModel()
    {
        PrayerTimes = new ObservableCollection<prayerTime>
        {
            new("Fajr", "00:00:00"),
            new("Dhuhr", "00:00:00"),
            new("Asr", "00:00:00"),
            new("Maghrib", "00:00:00"),
            new("Isha", "00:00:00")
        };
        Current = this;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            Functions.settings.Loader();
            await FetchPrayerTimes();
            StartBackgroundTicker();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Initialization Error: {ex.Message}");
        }
    }

    public async Task FetchPrayerTimes()
    {
        try
        {
            // Prevent duplicate fetches on the same day
            var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
            if (_lastFetchDate == todayStr && !Functions.settings.new_URL) return;

            var cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "prayer_times.json");

            if (File.Exists(cachePath) && !Functions.settings.new_URL)
            {
                var cachedJson = await File.ReadAllTextAsync(cachePath);
                var cachedData = JsonSerializer.Deserialize(cachedJson, PrayerTimeJsonContext.Default.PrayerTimeCache);

                if (cachedData != null && cachedData.Date == todayStr)
                {
                    LoadPrayerTimes(cachedData.Times);
                    _lastFetchDate = todayStr;
                    Console.WriteLine("Loaded prayer times from cache.");
                    return;
                }
            }

            var url = Functions.settings.Mawaqit_URL;
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var htmlContent = await response.Content.ReadAsStringAsync();

            var timesStartIndex = htmlContent.IndexOf("\"times\":", StringComparison.Ordinal) + 8;
            var timesEndIndex = htmlContent.IndexOf("]", timesStartIndex, StringComparison.Ordinal) + 1;
            var timesJson = htmlContent.Substring(timesStartIndex, timesEndIndex - timesStartIndex);

            var times = JsonSerializer.Deserialize(timesJson, PrayerTimeJsonContext.Default.ListString);

            if (times == null || times.Count != 5)
                throw new Exception("Failed to extract prayer times.");

            var cache = new PrayerTimeCache { Date = todayStr, Times = times };
            if (Functions.settings.new_URL) Functions.settings.new_URL = false;

            // Use static JSON options
            var json = JsonSerializer.Serialize(cache, _jsonOptions);
            await File.WriteAllTextAsync(cachePath, json);

            _lastFetchDate = todayStr;
            Console.WriteLine("Fetched from web and saved to cache.");
            LoadPrayerTimes(times);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error fetching prayer times: " + ex.Message);
        }
    }

    private void LoadPrayerTimes(List<string> times)
    {
        var parsedTimes = times
            .Select(t => DateTime.ParseExact(t, "HH:mm", CultureInfo.InvariantCulture))
            .ToList();

        _prayerTimes = parsedTimes.ToArray();
        var prayerNames = new[] { "Fajr", "Dhuhr", "Asr", "Maghrib", "Isha" };

        PrayerTimes.Clear();
        for (var i = 0; i < prayerNames.Length; i++)
        {
            PrayerTimes.Add(new prayerTime(prayerNames[i], parsedTimes[i].ToShortTimeString()));
        }
    }

    private void StartBackgroundTicker()
    {
        Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync())
            {
                var currentTime = DateTime.Now;
                await HandleCountdownUpdateAsync(currentTime);
                await HandleAdhanCheckAsync(currentTime);
            }
        });
    }

    private async Task HandleCountdownUpdateAsync(DateTime currentTime)
    {
        nextPrayerIndex = -1;
        TimeSpan countdown = TimeSpan.Zero;

        for (int i = 0; i < _prayerTimes.Length; i++)
        {
            if (_prayerTimes[i] > currentTime)
            {
                nextPrayerIndex = i;
                countdown = _prayerTimes[i] - currentTime;
                break;
            }
        }

        if (nextPrayerIndex == -1 && _prayerTimes.Length > 0)
        {
            var firstPrayerTimeTomorrow = _prayerTimes[0].AddDays(1);
            countdown = firstPrayerTimeTomorrow - currentTime;
            nextPrayerIndex = 0;
        }
        
        var timeString = countdown.ToString(@"hh\:mm\:ss");

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _cachedUpdaterBlock ??= Instance.Find<TextBlock>("updater");
                if (_cachedUpdaterBlock != null)
                    _cachedUpdaterBlock.Text = timeString;

                if (nextPrayerIndex != _lastHighlightedIndex)
                {
                    HighlightNextPrayerBorder(nextPrayerIndex);
                    _lastHighlightedIndex = nextPrayerIndex;
                }
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task HandleAdhanCheckAsync(DateTime currentTime)
    {
        if (_prayerTimes.Length < 5) return;
        
        if (currentTime.Minute == _lastCheckedMinute) return;
        _lastCheckedMinute = currentTime.Minute;

        var now = currentTime.ToString("HH:mm");
        string matchedPrayerTime = null;
        foreach (var prayer in _prayerTimes)
        {
            var pTime = prayer.ToString("HH:mm");
            if (pTime == now)
            {
                matchedPrayerTime = pTime;
                break;
            }
        }
        

        if (matchedPrayerTime != null)
        {
            if (_lastPrayerTime != matchedPrayerTime)
            {
                _adhanPlayed = false;
            }
            
            var canPlay = false;
            lock (AudioLock) 
            {
                if (!_adhanPlayed)
                {
                    _adhanPlayed = true;
                    _lastPrayerTime = matchedPrayerTime;
                    canPlay = true;
                }
            }

            if (canPlay)
            {
                _ = Task.Run(() => PlayAdhanAudio());
            }
        }
        
        var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        if ((now == "00:00" || _prayerTimes[4].ToString("HH:mm") == now) && _lastFetchDate != todayStr)
        {
            await FetchPrayerTimes();
        }
    }

    private void PlayAdhanAudio()
    {
        try
        {
            StopAndDispose();

            // Use AppContext.BaseDirectory for AOT reliability
            var soundPath = Path.Combine(AppContext.BaseDirectory, "assets", Functions.settings.Adhan);

            if (!File.Exists(soundPath))
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "assets", "error.txt"), "MP3 file not found at: " + soundPath);
                return; // Essential: exit the method to prevent crashes
            }
    
            Bass.Init(-1, 44100, DeviceInitFlags.Default);
        
            _streamHandle = Bass.CreateStream(soundPath, 0, 0, BassFlags.Default);
            if (_streamHandle == 0)
            {
                Console.WriteLine("BASS Error: " + Bass.LastError);
                return;
            }
            Bass.ChannelSetAttribute(_streamHandle, ChannelAttribute.Volume, Functions.settings.Volume / 100.0);
            _endTrackSync = new SyncProcedure((handle, channel, data, user) => 
            {
                StopAndDispose();
            });
            Bass.ChannelSetSync(_streamHandle, SyncFlags.End, 0, _endTrackSync);
    
            Bass.ChannelPlay(_streamHandle);
            
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error playing MP3: " + ex.Message);
            StopAndDispose();
        }
    }

    public static void StopAndDispose()
    {
        try
        {
            lock (AudioLock)
            {
                if (_streamHandle != 0)
                {
                    Bass.ChannelStop(_streamHandle); // Stop playing
                    Bass.StreamFree(_streamHandle);  // Free memory
                    _streamHandle = 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error during audio cleanup: " + ex.Message);
        }
    }

    private void HighlightNextPrayerBorder(int prayerIndex)
    {
        if (_cachedBorders == null)
        {
            _cachedBorders = new[]
            {
                Instance.Find<Border>("FajrBorder"),
                Instance.Find<Border>("DhuhrBorder"),
                Instance.Find<Border>("AsrBorder"),
                Instance.Find<Border>("MaghribBorder"),
                Instance.Find<Border>("IshaBorder")
            };
        }

        for (int i = 0; i < _cachedBorders.Length; i++)
        {
            var border = _cachedBorders[i];
            if (border == null) continue;

            if (i == prayerIndex)
            {
                border.Background = HighlightBackgroundBrush;
                SetTextColor(border, BlackTextBrush);
            }
            else
            {
                border.Background = DefaultBackgroundBrush;
                SetTextColor(border, WhiteTextBrush);
            }
        }
    }

    private void SetTextColor(Border border, ISolidColorBrush brush)
    {
        if (border.Child is Panel panel)
        {
            foreach (var child in panel.Children.OfType<TextBlock>())
                child.Foreground = brush;
        }
        else if (border.Child is Grid grid)
        {
            foreach (var textBlock in grid.Children.OfType<TextBlock>())
                textBlock.Foreground = brush;
        }
    }
}