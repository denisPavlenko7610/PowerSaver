using System.Security.Principal;
using Timer = System.Timers.Timer;

namespace PowerSaver.App;

class Program
{
    private static int _idleMinutes = 5;
    static bool isEco;
    static Timer checkTimer;
    static Timer powerUsageTimer;
    private static uint _idleThresholdMs => (uint)(_idleMinutes * 60 * 1000);
    private static double _checkIntervalMs = 1000; // check every second
    private static uint _lastIdle = 0;

    static void Main()
    {
        if (!IsAdministrator())
        {
            Console.WriteLine("⚠ Run as Administrator.");
            return;
        }

        var displayManager = new DisplayManager();
        var powerManager = new PowerManager(
            "a1841308-3541-4fab-bc81-f71556f20b4a", // GUID Eco
            "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", // GUID Perf
            displayManager
        );

        Console.WriteLine($"Auto Power Saver started.");
        Console.WriteLine($"Idle threshold set to {_idleMinutes} minutes.");

        checkTimer = new Timer(_checkIntervalMs);
        checkTimer.Elapsed += (s, e) =>
        {
            uint idle = IdleWatcher.GetIdleTime();

            if (idle > _idleThresholdMs && !isEco)
            {
                Console.WriteLine($"Idle started at {DateTime.Now:T}. Idle time: {idle / 1000 / 60} min.");
                powerManager.EnableEco();
                isEco = true;
                Console.WriteLine($"Switched to Eco mode at {DateTime.Now:T}.");
            }

            if (isEco && idle < _lastIdle)
            {
                powerManager.DisableEco();
                isEco = false;
                Console.WriteLine($"Returned to Performance mode at {DateTime.Now:T}.");
            }

            _lastIdle = idle;
        };
        checkTimer.Start();

        powerUsageTimer = new Timer(60000);
        powerUsageTimer.Elapsed += (s, e) => powerManager.UpdatePowerUsage();
        powerUsageTimer.Start();

        Console.ReadLine();
    }

    static bool IsAdministrator()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }
}