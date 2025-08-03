using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PowerSaver.App;

public class PowerManager
{
    private readonly string _ecoGuid;
    private readonly string _perfGuid;
    private readonly DisplayManager _displayManager;

    public PowerManager(string ecoGuid, string perfGuid, DisplayManager displayManager)
    {
        _ecoGuid = ecoGuid;
        _perfGuid = perfGuid;
        _displayManager = displayManager;
    }

    // Enable Eco mode depending on selected power level
    public void EnableEco(PowerMode mode)
    {
        SetScheme(_ecoGuid); // Always switch to Eco scheme
        DisableDisplayTimeout(Guid.Parse(_ecoGuid));
        
        if (mode == PowerMode.Medium || mode == PowerMode.Hard)
        {
            //_displayManager.SetRefreshRate(60); // Reduce refresh rate to 60 Hz
        }

        if (mode == PowerMode.Hard)
        {
            // Always-on energy saver
                RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD 100");
                RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD 100");
                RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTENABLED 1");
                RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTENABLED 1");

                // CPU throttling
                RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 20");
                RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 20");
                RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 5");
                RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 5");

                // USB suspend
                RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_USB USBSUSPEND 1");
                RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_USB USBSUSPEND 1");

                // Disk sleep after 5 min
                RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_DISK DISKIDLE 300000");
                RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_DISK DISKIDLE 300000");

                // PCIe ASPM aggressive
                RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 1");
                RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 1");

                // Cooling policy passive
                RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_SYSTEMCOOLING POLICY 1");
                RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_SYSTEMCOOLING POLICY 1");

                // Hybrid sleep ON
                // RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_SLEEP HYBRIDSLEEP 1");
                // RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_SLEEP HYBRIDSLEEP 1");

                RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_DISK DISKIDLE 60");

                Console.WriteLine("[EnergySaver] Hard-mode energy-saving parameters applied.");
        }
    }

    // Restore performance mode settings
    public void DisableEco(PowerMode mode)
    {
        SetScheme(_perfGuid); // Always switch back to Performance scheme

        if (mode == PowerMode.Medium || mode == PowerMode.Hard)
        {
            _displayManager.RestoreRefreshRate(); // Restore saved refresh rate
        }

        if (mode == PowerMode.Hard)
        {
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

            // RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_SLEEP HYBRIDSLEEP 0");
            // RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_SLEEP HYBRIDSLEEP 0");
            
            RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_DISK DISKIDLE 1200");

            Console.WriteLine("[EnergySaver] Hard-mode parameters reverted.");
        }
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

    // Change active power scheme
    private void SetScheme(string schemeGuid)
    {
        RunPowerCfg($"/s {schemeGuid}");
    }

    // Run powercfg command
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
