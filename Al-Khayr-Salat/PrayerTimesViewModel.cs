using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Al_Khayr_Salat.Functions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using NAudio.Wave;
using Newtonsoft.Json;
using static Al_Khayr_Salat.MainWindow;

namespace Al_Khayr_Salat;

public class PrayerTimesViewModel : BaseViewModel
{
    // Replaced Mutex with a lightweight object lock
    private static readonly object AudioLock = new();

    private static IWavePlayer? _outputDevice;
    private static AudioFileReader? _audioFile;

    private bool _adhanPlayed;
    private Border? _currentHighlightedBorder;
    private string _lastPrayerTime = string.Empty;
    private DateTime[] _prayerTimes = new DateTime[5];
    private string[] _prayerTimesDisplay;

    public int nextPrayerIndex = 5;
    public DateTime nextPrayerTime;
    private DateTime prayerTimeReachedLastedTime;

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

        // Fire and forget, but neatly wrapped to avoid swallowing exceptions
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            Functions.settings.Loader();
            await FetchPrayerTimes();
            
            // Start a single, unified background ticker
            StartBackgroundTicker();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Initialization Error: {ex.Message}");
        }
    }

    private async Task FetchPrayerTimes()
    {
        try
        {
            var cachePath = Path.Combine("assets", "prayer_times.json");

            if (File.Exists(cachePath))
            {
                var cachedJson = await File.ReadAllTextAsync(cachePath);
                var cachedData = JsonConvert.DeserializeObject<PrayerTimeCache>(cachedJson);

                if (cachedData != null && cachedData.Date == DateTime.Today.ToString("yyyy-MM-dd"))
                {
                    LoadPrayerTimes(cachedData.Times);
                    Console.WriteLine("Loaded prayer times from cache.");
                    return;
                }
            }

            var url = Functions.settings.Mawaqit_URL;

            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var htmlContent = await response.Content.ReadAsStringAsync();

                var timesStartIndex = htmlContent.IndexOf("\"times\":", StringComparison.Ordinal) + 8;
                var timesEndIndex = htmlContent.IndexOf("]", timesStartIndex, StringComparison.Ordinal) + 1;
                var timesJson = htmlContent.Substring(timesStartIndex, timesEndIndex - timesStartIndex);

                var times = JsonConvert.DeserializeObject<List<string>>(timesJson);

                if (times == null || times.Count != 5)
                    throw new Exception("Failed to extract prayer times.");

                var cache = new PrayerTimeCache
                {
                    Date = DateTime.Today.ToString("yyyy-MM-dd"),
                    Times = times
                };

                var json = JsonConvert.SerializeObject(cache, Formatting.Indented);
                await File.WriteAllTextAsync(cachePath, json);

                Console.WriteLine("Fetched from web and saved to cache.");
                LoadPrayerTimes(times);
            }
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
            PrayerTimes.Add(new prayerTime(
                prayerNames[i],
                parsedTimes[i].ToShortTimeString()
            ));
        }
    }

    // Consolidated Background Task
    private void StartBackgroundTicker()
    {
        Task.Run(async () =>
        {
            // PeriodicTimer is more efficient than Task.Delay and prevents thread drift
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
        nextPrayerTime = _prayerTimes.FirstOrDefault(time => time > currentTime);
        TimeSpan countdown;
        nextPrayerIndex = 5;

        if (nextPrayerTime != DateTime.MinValue)
        {
            countdown = nextPrayerTime - currentTime;
            nextPrayerIndex = _prayerTimes
                .Select((time, index) => new { time, index })
                .FirstOrDefault(pair => pair.time.TimeOfDay == nextPrayerTime.TimeOfDay)?.index ?? -1;
        }
        else
        {
            var firstPrayerTimeTomorrow = _prayerTimes.First().AddDays(1);
            countdown = firstPrayerTimeTomorrow - currentTime;
            nextPrayerIndex = 0;
        }

        var timeString = $"{countdown.Hours:D2}:{countdown.Minutes:D2}:{countdown.Seconds:D2}";

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var updaterBlock = Instance.Find<TextBlock>("updater");
                if (updaterBlock == null)
                    Console.WriteLine("TextBlock 'updater' not found.");
                else
                    updaterBlock.Text = timeString;

                HighlightNextPrayerBorder(nextPrayerIndex);
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task HandleAdhanCheckAsync(DateTime currentTime)
    {
        var adjustedIndex = nextPrayerIndex == 0 ? 4 : nextPrayerIndex - 1;
        var currentPrayerTime = _prayerTimes[adjustedIndex].ToString("HH:mm");
        var now = currentTime.ToString("HH:mm");

        // Console.WriteLine($"{currentPrayerTime} | {now}"); // Uncomment for debugging

        // Reset the flag if the prayer time has moved forward
        if (_lastPrayerTime != currentPrayerTime)
        {
            _adhanPlayed = false;
        }

        if ((currentPrayerTime == now || now == "08:15") && !_adhanPlayed)
        {
            var canPlay = false;

            // Replaced Mutex with lightweight lock
            lock (AudioLock) 
            {
                if (!_adhanPlayed)
                {
                    _adhanPlayed = true;
                    _lastPrayerTime = currentPrayerTime;
                    canPlay = true;
                }
            }

            // Only trigger the audio task if we are actually allowed to play
            if (canPlay)
            {
                _ = Task.Run(() => PlayAdhanAudio());
            }
        }

        // Re-fetch prayer times at midnight or at Isha
        if (nextPrayerTime.ToString("HH:mm") == "00:00" || _prayerTimes[4].ToString("HH:mm") == now)
        {
             await FetchPrayerTimes();
        }
    }

    private void PlayAdhanAudio()
    {
        try
        {
            StopAndDispose();

            var soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", Al_Khayr_Salat.Functions.settings.Adhan);
            if (!File.Exists(soundPath)) return;

            _outputDevice = new WaveOutEvent();
            _audioFile = new AudioFileReader(soundPath);

            _outputDevice.Volume = Functions.settings.Volume / 100f;
            _outputDevice.Init(_audioFile);
            _outputDevice.Play();

            _outputDevice.PlaybackStopped += (s, e) => StopAndDispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error playing audio: " + ex.Message);
            StopAndDispose();
        }
    }

    public static void StopAndDispose()
    {
        try
        {
            lock (AudioLock) // Also protect disposal
            {
                _outputDevice?.Stop();
                _audioFile?.Dispose();
                _audioFile = null;
                _outputDevice?.Dispose();
                _outputDevice = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error during audio cleanup: " + ex.Message);
        }
    }

    private void HighlightNextPrayerBorder(int prayerIndex)
    {
        if (_currentHighlightedBorder != null)
            if (_currentHighlightedBorder is Border border)
            {
                border.Background = new SolidColorBrush(Color.Parse("#191919"));
                if (border.Child is Panel panel)
                    foreach (var textBlock in panel.Children.OfType<TextBlock>())
                        textBlock.Foreground = new SolidColorBrush(Colors.White);
                else if (border.Child is Grid grid)
                    foreach (var textBlock in grid.Children.OfType<TextBlock>())
                        textBlock.Foreground = new SolidColorBrush(Colors.White);
            }

        _currentHighlightedBorder = prayerIndex switch
        {
            0 => Instance.Find<Border>("FajrBorder"),
            1 => Instance.Find<Border>("DhuhrBorder"),
            2 => Instance.Find<Border>("AsrBorder"),
            3 => Instance.Find<Border>("MaghribBorder"),
            4 => Instance.Find<Border>("IshaBorder"),
            _ => null
        };

        if (_currentHighlightedBorder != null)
        {
            _currentHighlightedBorder.Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop { Color = Color.Parse("#f6b162"), Offset = 0 },
                    new GradientStop { Color = Color.Parse("#f9f871"), Offset = 1 }
                }
            };

            if (_currentHighlightedBorder is Border border)
            {
                if (border.Child is Panel panel)
                    foreach (var child in panel.Children.OfType<TextBlock>())
                        child.Foreground = new SolidColorBrush(Colors.Black);
                else if (border.Child is Grid grid)
                    foreach (var textBlock in grid.Children.OfType<TextBlock>())
                        textBlock.Foreground = new SolidColorBrush(Colors.Black);
            }
        }
    }
}