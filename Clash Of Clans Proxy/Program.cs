using System;

namespace ClashOfClansProxy
{
    class Program
    {
        static void Main()
        {
            // Check whether the proxy runs more than once
            if (Helper.OpenedInstances > 1)
            {
                Logger.Log("You seem to run this proxy more than once.", LogType.WARNING);
                Logger.Log("Aborting..", LogType.WARNING);
                System.Threading.Thread.Sleep(3500);
                Environment.Exit(0);
            }

            // UI
            Console.Title = "Clash Of Clans Proxy " + Helper.AssemblyVersion + " | © " + DateTime.UtcNow.Year;
            Console.SetCursorPosition(0, 0);
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(
                @"
888     888 888    88888888888 8888888b.         d8888 8888888b.   .d88888b.  888       888        d8888
888     888 888        888     888   Y88b       d88888 888   Y88b d88P' 'Y88b 888   o   888       d88888
888     888 888        888     888    888      d88P888 888    888 888     888 888  d8b  888      d88P888
888     888 888        888     888   d88P     d88P 888 888   d88P 888     888 888 d888b 888     d88P 888
888     888 888        888     8888888P'     d88P  888 8888888P'  888     888 888d88888b888    d88P  888
888     888 888        888     888 T88b     d88P   888 888        888     888 88888P Y88888   d88P   888
Y88b. .d88P 888        888     888  T88b   d8888888888 888        Y88b. .d88P 8888P   Y8888  d8888888888
 'Y88888P'  88888888   888     888   T88b d88P     888 888         'Y88888P'  888P     Y888 d88P     888
                  ");
            Logger.Log("This Program is created for learning Clash Of Clans Protocol", LogType.INFO);
            Logger.Log("You can find the up to date source at www.github.com/ultrapowadev/ucs", LogType.INFO);
            Logger.Log("Don't forget to visit www.ultrapowa.com daily for news update !", LogType.INFO);
            // Proxy
            Proxy.Start();
        }
    }
}