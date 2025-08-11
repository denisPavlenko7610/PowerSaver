using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers;
using LibreHardwareMonitor.Hardware;
using Timer = System.Timers.Timer;

namespace PowerSaver.App;

public class PowerManager
{
    private readonly string _ecoGuid;
    private readonly string _perfGuid;
    private readonly DisplayManager _displayManager;

    private DateTime? ecoStartTime;
    private double accumulatedSavedWh = 0;
    private double lastPerfPower = 0;
    private DateTime lastMeasurementTime;

    private Timer powerTimer;
    private readonly Computer _computer;

    public PowerManager(string ecoGuid, string perfGuid, DisplayManager displayManager)
    {
        _ecoGuid = ecoGuid;
        _perfGuid = perfGuid;
        _displayManager = displayManager;

        _computer = new Computer()
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsControllerEnabled = true,
            IsNetworkEnabled = true
        };
        _computer.Open();
    }

    public double UpdatePowerUsage()
    {
        double cpuPower = 0;
        double gpuPower = 0;
        double memoryPower = 0;
        double storagePower = 0;
        double motherboardPower = 0;

        foreach (var hw in _computer.Hardware)
        {
            hw.Update();

            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue)
                {
                    if (hw.HardwareType == HardwareType.Cpu && sensor.Name.Contains("Package"))
                        cpuPower += sensor.Value.Value;
                    else if ((hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd) &&
                             sensor.Name.Contains("Power"))
                        gpuPower += sensor.Value.Value;
                    else if (hw.HardwareType == HardwareType.Memory)
                        memoryPower += sensor.Value.Value;
                    else if (hw.HardwareType == HardwareType.Storage)
                        storagePower += sensor.Value.Value;
                    else if (hw.HardwareType == HardwareType.Motherboard)
                        motherboardPower += sensor.Value.Value;
                }
            }
        }

        double totalPower = cpuPower + gpuPower + memoryPower + storagePower + motherboardPower;
        return totalPower;
    }

    public void EnableEco()
    {
        ecoStartTime = DateTime.Now;
        accumulatedSavedWh = 0;

        lastPerfPower = UpdatePowerUsage();

        SetScheme(_ecoGuid);
        DisableDisplayTimeout(Guid.Parse(_ecoGuid));

        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD 100");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD 100");
        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTENABLED 1");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTENABLED 1");

        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 20");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 20");
        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 5");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 5");

        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_USB USBSUSPEND 1");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_USB USBSUSPEND 1");

        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_DISK DISKIDLE 300000");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_DISK DISKIDLE 300000");

        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 1");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 1");

        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_SYSTEMCOOLING POLICY 1");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_SYSTEMCOOLING POLICY 1");

        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_DISK DISKIDLE 60");

        lastMeasurementTime = DateTime.Now;

        powerTimer = new Timer(60000); // интервал 1 минута
        powerTimer.Elapsed += OnPowerTimerElapsed;
        powerTimer.AutoReset = true;
        powerTimer.Start();
    }

    private void OnPowerTimerElapsed(object sender, ElapsedEventArgs e)
    {
        double currentEcoPower = UpdatePowerUsage();
        DateTime now = DateTime.Now;
        double deltaHours = (now - lastMeasurementTime).TotalHours;

        double powerDiff = lastPerfPower - currentEcoPower;

        if (powerDiff > 0)
        {
            accumulatedSavedWh += powerDiff * deltaHours;
        }

        lastMeasurementTime = now;
    }

    public void DisableEco()
    {
        powerTimer?.Stop();
        powerTimer?.Dispose();
        powerTimer = null;

        SetScheme(_perfGuid);

        if (ecoStartTime.HasValue)
        {
            var duration = DateTime.Now - ecoStartTime.Value;
            ecoStartTime = null;

            Console.WriteLine($"[EnergySaver] Saved approx: {accumulatedSavedWh:F2} Wh over {duration.TotalMinutes:F1} min.");
        }

        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD 0");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD 0");
        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTENABLED 0");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTENABLED 0");

        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100");
        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 0");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 0");

        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_USB USBSUSPEND 0");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_USB USBSUSPEND 0");

        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_DISK DISKIDLE 0");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_DISK DISKIDLE 0");

        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 0");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 0");

        RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_SYSTEMCOOLING POLICY 0");
        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_SYSTEMCOOLING POLICY 0");

        RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_DISK DISKIDLE 1200");
    }

    private void DisableDisplayTimeout(Guid plan)
    {
        Guid videoSubgroup = new Guid("7516b95f-f776-4464-8c53-06167f40cc99");
        Guid videoTimeout = new Guid("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
        Guid scheme = plan;

        PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref videoSubgroup, ref videoTimeout, 0);
        PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref videoSubgroup, ref videoTimeout, 0);
        PowerSetActiveScheme(IntPtr.Zero, ref scheme);
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteACValueIndex(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        uint AcValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteDCValueIndex(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        uint DcValueIndex);

    private void SetScheme(string schemeGuid)
    {
        RunPowerCfg($"/s {schemeGuid}");
    }

    private void RunPowerCfg(string arguments)
    {
        var psi = new ProcessStartInfo("powercfg", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
    }
}