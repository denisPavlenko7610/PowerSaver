using System.Runtime.InteropServices;

namespace PowerSaver.App
{
    public class DisplayManager
    {
        private int? _originalRefreshRate;

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVMODE
        {
            private const int CCHDEVICENAME = 32;
            private const int CCHFORMNAME = 32;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;

            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;

            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
        }

        // P/Invoke declarations
        [DllImport("user32.dll")] private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);
        [DllImport("user32.dll")] private static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

        /// <summary>
        /// Returns the current display refresh rate in Hz.
        /// </summary>
        public int GetCurrentRefreshRate()
        {
            var vDevMode = new DEVMODE();
            vDevMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (EnumDisplaySettings(null, -1, ref vDevMode))
            {
                return vDevMode.dmDisplayFrequency;
            }
            return -1;
        }

        /// <summary>
        /// Sets the refresh rate to the specified Hz if it's supported by the system.
        /// </summary>
        public void SetRefreshRate(int hz)
        {
            if (!IsRefreshRateSupported(hz))
            {
                Console.WriteLine($"[DisplayManager] {hz} Hz is not supported. Skipping refresh rate change.");
                return;
            }

            if (!_originalRefreshRate.HasValue)
                _originalRefreshRate = GetCurrentRefreshRate();

            var dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            EnumDisplaySettings(null, -1, ref dm);

            dm.dmDisplayFrequency = hz;
            dm.dmFields = 0x400000; // DM_DISPLAYFREQUENCY

            ChangeDisplaySettings(ref dm, 0);
        }

        /// <summary>
        /// Restores the refresh rate to the one saved before changes.
        /// </summary>
        public void RestoreRefreshRate()
        {
            if (_originalRefreshRate.HasValue)
            {
                //SetRefreshRate(_originalRefreshRate.Value);
                _originalRefreshRate = null;
            }
        }

        /// <summary>
        /// Checks whether a given refresh rate is supported by the current display.
        /// </summary>
        public bool IsRefreshRateSupported(int targetHz)
        {
            var dm = new DEVMODE();
            int modeNum = 0;
            var seenRates = new HashSet<int>();

            while (EnumDisplaySettings(null, modeNum++, ref dm))
            {
                if (dm.dmDisplayFrequency > 1)
                    seenRates.Add(dm.dmDisplayFrequency);
            }

            return seenRates.Contains(targetHz);
        }
    }
}
