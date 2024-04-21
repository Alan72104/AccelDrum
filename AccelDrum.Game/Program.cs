using AccelDrum.Game;
using Serilog;
using System;

namespace AccelDrum;

class Program
{
    [STAThread]
    public static void Main()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("log.txt",
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true)
            .CreateLogger();
        try
        {
            using Window game = new Window();
            game.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception");
        }
    }
}