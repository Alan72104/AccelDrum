using AccelDrum.Game;
using System;
using System.Diagnostics;

namespace AccelDrum;

class Program
{
    [STAThread]
    public static void Main()
    {
        using Window game = new Window();
        game.Run();
    }
}