using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Al_Khayr_Salat.Functions;

public class PrayerTimeCache
{
    public string Date { get; set; } = string.Empty;
    public List<string> Times { get; set; } = new();
}