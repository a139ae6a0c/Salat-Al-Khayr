using System;
using System.IO;
using System.Text.Json;

namespace Al_Khayr_Salat.Functions;

public class settings
{
    public static string Mawaqit_URL { get; set; }
    public static int Volume { get; set; }
    public static string Adhan { get; set; }

    public static void Loader()
    {
        LoadConfig();
    }

    public static void UpdateVolume(int newVolume)
    {
        Volume = Math.Clamp(newVolume, 0, 100);
        SaveConfig();
    }

    public static void UpdateMawaqitURL(string newUrl)
    {
        if (string.IsNullOrEmpty(newUrl)) throw new ArgumentException("URL cannot be null or empty.", nameof(newUrl));

        Mawaqit_URL = newUrl;
        SaveConfig();
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
            var filePath = Path.Combine("assets", "config.json");

            // Check if the config file exists
            if (!File.Exists(filePath))
            {
                var defaultConfig = new Config
                { 
                    Mawaqit_URL = "https://mawaqit.net/en/al-haram-makkah-saudi-arabia", 
                    Volume = 50,
                    Adhan = "adhan.mp3" // Default Adhan file
                };
                
                var defaultJsonContent = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory("assets");
                File.WriteAllText(filePath, defaultJsonContent);
                
                Mawaqit_URL = defaultConfig.Mawaqit_URL;
                Volume = defaultConfig.Volume;
                Adhan = defaultConfig.Adhan;
                
                Console.WriteLine("Config file not found. Created a new config.json with default URL and Adhan.");
                return;
            }

            var jsonContent = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<Config>(jsonContent);

            if (config != null && !string.IsNullOrEmpty(config.Mawaqit_URL))
            {
                Mawaqit_URL = config.Mawaqit_URL;
                Volume = config.Volume;
                
                // Set the Adhan, defaulting to "adhan.mp3" if the config file is old and doesn't have it yet
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
            Volume = 50;
            Adhan = "adhan.mp3"; // Fallback safety
        }
    }

    private static void SaveConfig()
    {
        try
        {
            var filePath = Path.Combine("assets", "config.json");
            var config = new Config 
            { 
                Mawaqit_URL = Mawaqit_URL, 
                Volume = Volume,
                Adhan = Adhan // Add Adhan to the saved properties
            };
            
            var jsonContent = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, jsonContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
        }
    }
}