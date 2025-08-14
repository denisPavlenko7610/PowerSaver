using System.Security.Principal;
using Timer = System.Timers.Timer;

namespace PowerSaver.App;

class Program
{
    private static int _idleMinutes = 5;

    static bool isEco;
    static Timer checkTimer;
    private static uint _idleThresholdMs => (uint)(_idleMinutes * 60 * 1000);
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
        
        var powerUsageTimer = new System.Timers.Timer(60000);
        powerUsageTimer.Elapsed += (s, e) => powerManager.UpdatePowerUsage();
        powerUsageTimer.Start();

        checkTimer = new Timer(1000);
        checkTimer.Elapsed += (s, e) =>
        {
            uint idle = IdleWatcher.GetIdleTime();

            if (idle >= _idleThresholdMs && !isEco)
            {
                Console.WriteLine($"Eco mode started at {DateTime.Now:T}. Idle time: {idle / 1000 / 60} min.");
                powerManager.EnableEco();
                isEco = true;
            }

            if (isEco && idle < _lastIdle)
            {
                powerManager.DisableEco();
                isEco = false;
            }

            _lastIdle = idle;
        };
        checkTimer.Start();

        Console.ReadLine();
    }

    static bool IsAdministrator()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }
}