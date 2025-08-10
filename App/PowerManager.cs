using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace PowerSaver.App;

public class PowerManager
{
    private readonly string _ecoGuid;
    private readonly string _perfGuid;
    private readonly DisplayManager _displayManager;

    private Computer _computer;
    private DateTime? ecoStartTime;
    private double lastMeasuredWattsPerf = 0;
    private double lastMeasuredWattsEco = 0;

    public PowerManager(string ecoGuid, string perfGuid, DisplayManager displayManager)
    {
        _ecoGuid = ecoGuid;
        _perfGuid = perfGuid;
        _displayManager = displayManager;

        _computer = new Computer
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

    public void UpdatePowerUsage()
    {
        try
        {
            double cpuPower = 0, gpuPower = 0, mbPower = 0, measuredOther = 0;

            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue && sensor.Value.Value > 0.5)
                    {
                        if (hw.HardwareType == HardwareType.Cpu &&
                            (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                             sensor.Name.Contains("CPU Total", StringComparison.OrdinalIgnoreCase)))
                        {
                            cpuPower += sensor.Value.Value;
                        }
                        else if ((hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd) &&
                                 (sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
                                  sensor.Name.Contains("Power", StringComparison.OrdinalIgnoreCase)))
                        {
                            gpuPower += sensor.Value.Value;
                        }
                        else if (hw.HardwareType == HardwareType.Motherboard)
                        {
                            mbPower += sensor.Value.Value;
                        }
                        else
                        {
                            measuredOther += sensor.Value.Value;
                        }
                    }
                }
            }

            double mainMeasured = cpuPower + gpuPower + mbPower;
            if (mainMeasured < 1.0)
                mainMeasured = measuredOther;

            double memoryPower = EstimateMemoryPower();
            double diskPower = EstimateDiskPower();
            int fanCount = GetFanCount();
            double fanPower = fanCount * 2.0;
            double miscPower = 10; // Chipset, USB, LEDs, etc.

            double totalRaw = mainMeasured + memoryPower + diskPower + fanPower + miscPower;
            double psuCompensated = totalRaw / 0.85;
            double errorCompensation = psuCompensated * 1.10;

            // Для оценки потребления в режиме Perf (фактически текущее замеренное)
            lastMeasuredWattsPerf = errorCompensation;

            // Для оценки Eco режима считаем примерно 40% CPU, 30% GPU и 95% остального
            lastMeasuredWattsEco = (cpuPower * 0.4) + (gpuPower * 0.3) + (mbPower * 0.95) +
                                   memoryPower * 0.95 + diskPower * 0.95 + fanPower * 0.95 + miscPower * 0.95;

            Console.WriteLine($"[PowerUsage] Estimated Perf: {lastMeasuredWattsPerf:F1} W, Estimated Eco: {lastMeasuredWattsEco:F1} W");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PowerUsage] Error: {ex.Message}");
        }
    }

    private int GetFanCount()
    {
        int count = 0;
        foreach (var hw in _computer.Hardware)
        {
            hw.Update();
            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType == SensorType.Fan)
                    count++;
            }
        }
        return count > 0 ? count : 3;
    }

    private double EstimateMemoryPower()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
            int count = 0;
            foreach (var _ in searcher.Get())
                count++;
            return count * 3.0;
        }
        catch
        {
            return 6.0;
        }
    }

    private double EstimateDiskPower()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            double total = 0;
            foreach (ManagementObject drive in searcher.Get())
            {
                string mediaType = (drive["MediaType"] ?? "").ToString().ToLower();
                if (mediaType.Contains("ssd"))
                    total += 3.0;
                else
                    total += 6.0;
            }
            return total > 0 ? total : 6.0;
        }
        catch
        {
            return 6.0;
        }
    }

    public void EnableEco()
    {
        ecoStartTime = DateTime.Now;
        SetScheme(_ecoGuid);
        DisableDisplayTimeout(Guid.Parse(_ecoGuid));
        Console.WriteLine("[EnergySaver] Hard-mode energy-saving parameters applied.");
    }

    public void DisableEco()
    {
        SetScheme(_perfGuid);

        if (ecoStartTime.HasValue)
        {
            var duration = DateTime.Now - ecoStartTime.Value;
            double hours = duration.TotalHours;
            double savedWh = (lastMeasuredWattsPerf - lastMeasuredWattsEco) * hours;
            ecoStartTime = null;

            Console.WriteLine($"[EnergySaver] Saved approx: {savedWh:F2} Wh over {duration.TotalMinutes:F1} min.");
        }

        Console.WriteLine("[EnergySaver] Hard-mode parameters reverted.");
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
