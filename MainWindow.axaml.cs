using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using static Al_Khayr_Salat.PrayerTimesViewModel;

namespace Al_Khayr_Salat;

public partial class MainWindow : Window
{
    public static TrayIcon? trayIcon;
    public static MainWindow? Instance { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        
        // Subscription to events
        Opened += OnOpened;
        Deactivated += OnLostFocus;
        
        CreateTrayIcon();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        // Set position once
        var screen = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        Position = new PixelPoint(screen.Width - 400, screen.Height - 510);
    }

    private void OnLostFocus(object? sender, EventArgs e)
    {
        // Hide UI to stop layout/rendering cycles, saving GPU/CPU
        Hide();
    }

    private void CreateTrayIcon()
    {
        var openItem = new NativeMenuItem("Open");
        openItem.Click += (s, e) => ToggleWindow();
        
        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (s, e) => 
        {
            trayIcon?.Dispose(); // Clean up icon before exiting
            Environment.Exit(0);
        };
        
        // Reuse the icon to prevent multiple instances in memory
        trayIcon = new TrayIcon
        {
            Icon = new WindowIcon("Assets/logo.ico"), 
            ToolTipText = "Salat Time",
            IsVisible = true, 
            Menu = new NativeMenu
            {
                Items = { openItem, quitItem }
            }
        };

        trayIcon.Clicked += (sender, e) => ToggleWindow();
    }
    
    private void ToggleWindow()
    {
        Show();
        Activate();
        WindowState = WindowState.Normal;
    }

    private void StopAdhan_Click(object? sender, RoutedEventArgs e)
    {
        StopAndDispose();
    }

    private void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var settingsView = new settings();
        
        settingsView.URLInput.Text = Functions.settings.Mawaqit_URL;
        settingsView.VolumeSlider.Value = Functions.settings.Volume;

        ViewContainer.Content = settingsView;
    }

    public void NavigateBack()
    {
        ViewContainer.Content = PrayerTimesView;
    }
}