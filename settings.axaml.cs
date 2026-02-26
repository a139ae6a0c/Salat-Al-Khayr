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

        // 1. Load current values into UI
        URLInput.Text = Functions.settings.Mawaqit_URL;
        VolumeSlider.Value = Functions.settings.Volume;
        
        _assetsFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");

        // Ensure the directory exists
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
        var mp3Files = Directory.GetFiles(_assetsFolderPath, "*.mp3")
            .Select(Path.GetFileName)
            .ToList();

        AdhanComboBox.ItemsSource = mp3Files;

        if (mp3Files.Any())
        {
            // Fetch the currently saved Adhan from your settings class
            string savedAdhan = Al_Khayr_Salat.Functions.settings.Adhan;

            // Check if the saved Adhan actually exists in the Assets folder
            if (!string.IsNullOrEmpty(savedAdhan) && mp3Files.Contains(savedAdhan))
            {
                AdhanComboBox.SelectedItem = savedAdhan;
            }
            else
            {
                // Fallback: If the saved file is missing, select the first one
                AdhanComboBox.SelectedIndex = 0;
            
                // Update the config so it doesn't look for the missing file next time
                if (AdhanComboBox.SelectedItem is string fallbackAdhan)
                {
                    Al_Khayr_Salat.Functions.settings.UpdateAdhan(fallbackAdhan);
                }
            }
        }
    }
    
    private async void UploadAdhanButton_Click(object? sender, RoutedEventArgs e)
    {
        // Get the TopLevel window to access the StorageProvider
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // Open the file picker dialog
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

            // Attempt to get the actual local file path
            if (file.TryGetLocalPath() is string sourcePath)
            {
                string fileName = Path.GetFileName(sourcePath);
                string destinationPath = Path.Combine(_assetsFolderPath, fileName);

                try
                {
                    // Copy the file to the Assets folder (overwrite if it already exists)
                    File.Copy(sourcePath, destinationPath, overwrite: true);
                        
                    // Refresh the dropdown list to show the new file
                    LoadMp3Files();

                    // Set the newly uploaded file as the active selection
                    AdhanComboBox.SelectedItem = fileName;
                }
                catch (Exception ex)
                {
                    // Ideally, show an error dialog here. For now, output to console.
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
        // 2. Update URL
        var url = URLInput.Text;
        if (!string.IsNullOrEmpty(url)) Functions.settings.UpdateMawaqitURL(url);

        // 3. Update Volume
        var newVolume = (int)VolumeSlider.Value;
        Functions.settings.UpdateVolume(newVolume);

        Functions.settings.SaveConfig();

        // Navigate back
        MainWindow.Instance.NavigateBack();
    }
}