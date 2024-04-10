using AccelDrum.Game;
using System;
using System.Diagnostics;

namespace AccelDrum;

class Program
{
    [STAThread]
    public static void Main()
    {
        if (!Debugger.IsAttached)
        {
            //try
            //{
                using (Window game = new Window())
                    game.Run();
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex);
            //}
        }
        else
        {
            using (Window game = new Window())
                game.Run();
        }
    }
}