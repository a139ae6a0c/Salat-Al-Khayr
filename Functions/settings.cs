using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization; // Added for AOT Source Generation
using System.Threading.Tasks;

namespace Al_Khayr_Salat.Functions;

// 1. Define the JSON Context for AOT compilation
[JsonSerializable(typeof(Config))]
internal partial class ConfigJsonContext : JsonSerializerContext
{
}

public class settings
{
    public static string Mawaqit_URL { get; set; }
    public static int Volume { get; set; }
    public static string Adhan { get; set; }
    
    public static bool new_URL;
    
    public static string AppDataPath 
    {
        get 
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Al_Khayr_Salat");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }
    }

    public static void Loader()
    {
        LoadConfig();
    }

    public static void UpdateVolume(int newVolume)
    {
        Volume = Math.Clamp(newVolume, 0, 100);
        SaveConfig();
    }

    public static async Task UpdateMawaqitURL(string newUrl)
    {
        if (string.IsNullOrEmpty(newUrl)) throw new ArgumentException("URL cannot be null or empty.", nameof(newUrl));
        if (Mawaqit_URL != newUrl)
        {
            new_URL = true;
            Mawaqit_URL = newUrl;
            // Assuming PrayerTimesViewModel is defined elsewhere
            await PrayerTimesViewModel.Current.FetchPrayerTimes();
            SaveConfig();
        }
    }
    
    public static void UpdateAdhan(string newAdhan)
    {
        if (string.IsNullOrEmpty(newAdhan)) throw new ArgumentException("Adhan file cannot be null or empty.", nameof(newAdhan));

        Adhan = newAdhan;
        SaveConfig();
    }
    
    private static void LoadConfig()
    {
        try
        {
            var filePath = Path.Combine(AppDataPath, "config.json");
            
            // 2. Setup AOT-friendly options
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                TypeInfoResolver = ConfigJsonContext.Default
            };

            if (!File.Exists(filePath))
            {
                var defaultConfig = new Config
                { 
                    Mawaqit_URL = "https://mawaqit.net/en/al-haram-makkah-saudi-arabia", 
                    Volume = 50,
                    Adhan = "adhan.mp3"
                };
                
                // 3. Serialize using the AOT options
                var defaultJsonContent = JsonSerializer.Serialize(defaultConfig, options);
                File.WriteAllText(filePath, defaultJsonContent);
                
                Mawaqit_URL = defaultConfig.Mawaqit_URL;
                Volume = defaultConfig.Volume;
                Adhan = defaultConfig.Adhan;
                
                var defaultAdhanDest = Path.Combine(AppDataPath, "adhan.mp3");
                var defaultIconDest = Path.Combine(AppDataPath, "favicon.ico");
                if (!File.Exists(defaultAdhanDest))
                {
                    Console.WriteLine("Downloading default adhan.mp3...");
                    Task.Run(async () =>
                    {
                        try
                        {
                            using var client = new System.Net.Http.HttpClient();
                            var downloadUrl = "https://github.com/a139ae6a0c/Salat-Al-Khayr/raw/refs/heads/master/Assets/adhan.mp3";
                            var Bytes = await client.GetByteArrayAsync(downloadUrl);
                            await File.WriteAllBytesAsync(defaultAdhanDest, Bytes);
                            Console.WriteLine("Successfully downloaded default adhan.mp3.");
                            
                            // Icon
                            downloadUrl = "https://github.com/a139ae6a0c/Salat-Al-Khayr/raw/refs/heads/master/Assets/favicon.ico";
                            Bytes = await client.GetByteArrayAsync(downloadUrl);
                            await File.WriteAllBytesAsync(defaultIconDest, Bytes);
                            Console.WriteLine("Successfully downloaded default ICON.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to download default adhan.mp3: {ex.Message}");
                        }
                    }).Wait(); 
                }
                
                Console.WriteLine("Config file not found. Created a new config.json with default URL and Adhan.");
                return;
            }

            var jsonContent = File.ReadAllText(filePath);
            
            // 4. Deserialize using the AOT context
            var config = JsonSerializer.Deserialize(jsonContent, ConfigJsonContext.Default.Config);

            if (config != null && !string.IsNullOrEmpty(config.Mawaqit_URL))
            {
                Mawaqit_URL = config.Mawaqit_URL;
                Volume = config.Volume;
                Adhan = string.IsNullOrEmpty(config.Adhan) ? "adhan.mp3" : config.Adhan; 
            }
            else
            {
                throw new Exception("Mawaqit_URL is not found in config.json or is empty.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            Mawaqit_URL = "https://mawaqit.net/en/al-haram-makkah-saudi-arabia";
            Volume = 66;
            Adhan = "adhan.mp3"; 
        }
    }
    
    public static void SaveConfig()
    {
        try
        {
            var filePath = Path.Combine(AppDataPath, "config.json");
            var config = new Config 
            { 
                Mawaqit_URL = Mawaqit_URL, 
                Volume = Volume,
                Adhan = Adhan 
            };
            
            // 5. Setup AOT-friendly options for saving
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                TypeInfoResolver = ConfigJsonContext.Default
            };
            
            // 6. Serialize using AOT options
            var jsonContent = JsonSerializer.Serialize(config, options);
            File.WriteAllText(filePath, jsonContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
        }
    }
}


