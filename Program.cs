using System;
using System.Threading;
using Avalonia;

namespace Al_Khayr_Salat;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    
    private static Mutex? _appMutex;
    
    [STAThread]
    public static void Main(string[] args)
    {

        _appMutex = new Mutex(true, "Global\\AlKhayr_{0d47d8d4-41fb-46af-8670-c9f153671b9f}", out bool createdNew);

        if (!createdNew)
        {
            return; 
        }

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _appMutex.ReleaseMutex();
            _appMutex.Dispose();
        }
    }


    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();
    }
}