namespace Al_Khayr_Salat.Functions;

public class prayerTime 
{
    public prayerTime(string name, string time)
    {
        Salat = name;
        Salat_Time = time;
    }

    public string Salat { get; set; }
    public string Salat_Time { get; set; }
}