using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Al_Khayr_Salat;

public partial class settings : UserControl
{
    private readonly string _assetsFolderPath;
    
    public settings()
    {
        InitializeComponent();

        URLInput.Text = Functions.settings.Mawaqit_URL;
        VolumeSlider.Value = Functions.settings.Volume;
        
        _assetsFolderPath = Functions.settings.AppDataPath;

        if (!Directory.Exists(_assetsFolderPath))
        {
            Directory.CreateDirectory(_assetsFolderPath);
        }

        LoadMp3Files();

        BackButton.Click += BackButton_Click;
        ApplyButton.Click += Apply_Click;
    }

    private void LoadMp3Files()
    {
        var mp3Files = Directory.GetFiles(Functions.settings.AppDataPath, "*.mp3")
            .Select(Path.GetFileName)
            .ToList();

        AdhanComboBox.ItemsSource = mp3Files;

        if (mp3Files.Any())
        {
            string savedAdhan = Al_Khayr_Salat.Functions.settings.Adhan;

            if (!string.IsNullOrEmpty(savedAdhan) && mp3Files.Contains(savedAdhan))
            {
                AdhanComboBox.SelectedItem = savedAdhan;
            }
            else
            {
                AdhanComboBox.SelectedIndex = 0;
                if (AdhanComboBox.SelectedItem is string fallbackAdhan)
                {
                    Al_Khayr_Salat.Functions.settings.UpdateAdhan(fallbackAdhan);
                }
            }
        }
    }
    
    private async void UploadAdhanButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Adhan MP3",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("MP3 Audio") { Patterns = new[] { "*.mp3" } }
            }
        });

        if (files.Count >= 1)
        {
            var file = files[0];
            
            if (file.TryGetLocalPath() is string sourcePath)
            {
                string fileName = Path.GetFileName(sourcePath);
                string destinationPath = Path.Combine(_assetsFolderPath, fileName);

                try
                {
                    File.Copy(sourcePath, destinationPath, overwrite: true);
                    LoadMp3Files();
                    AdhanComboBox.SelectedItem = fileName;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error uploading file: {ex.Message}");
                }
            }
        }
    }
    
    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        MainWindow.Instance.NavigateBack();
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        var url = URLInput.Text;
        if (!string.IsNullOrEmpty(url)) Functions.settings.UpdateMawaqitURL(url);

        var newVolume = (int)VolumeSlider.Value;
        Functions.settings.UpdateVolume(newVolume);

        Functions.settings.SaveConfig();

        MainWindow.Instance.NavigateBack();
    }
}