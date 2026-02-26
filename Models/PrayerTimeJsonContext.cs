using System.Text.Json.Serialization;
using System.Collections.Generic;
using Al_Khayr_Salat.Functions;

[JsonSerializable(typeof(PrayerTimeCache))]
[JsonSerializable(typeof(List<string>))]
internal partial class PrayerTimeJsonContext : JsonSerializerContext
{
}