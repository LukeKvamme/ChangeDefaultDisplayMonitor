using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using RoR2;
using UnityEngine;

namespace MonitorSwitcher
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfoEx
    {
        public int CbSize;
        public NativeRect RcMonitor;
        public NativeRect RcWork;
        public uint DwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string SzDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayDevice
    {
        public int Cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public uint StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    internal struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        public ushort SpecVersion;
        public ushort DriverVersion;
        public ushort Size;
        public ushort DriverExtra;
        public uint Fields;
        public int PositionX;
        public int PositionY;
        public uint DisplayOrientation;
        public uint DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FormName;

        public ushort LogPixels;
        public uint BitsPerPel;
        public uint PelsWidth;
        public uint PelsHeight;
        public uint DisplayFlags;
        public uint DisplayFrequency;
        public uint ICMethod;
        public uint ICMIntent;
        public uint MediaType;
        public uint DitherType;
        public uint Reserved1;
        public uint Reserved2;
        public uint PanningWidth;
        public uint PanningHeight;
    }

    internal struct DisplayModeInfo
    {
        public int Width;
        public int Height;
        public int RefreshRate;
    }

    internal sealed class DisplayInfo
    {
        private const string DeviceNamePrefix = "DISPLAY";

        public string DeviceName;
        public string FullName;
        public string FriendlyName;
        public NativeRect Bounds;
        public NativeRect WorkArea;
        public bool IsPrimary;

        public string Label => $"Display {DisplayNumber} ({Bounds.Width} x {Bounds.Height})";

        private string DisplayNumber
        {
            get
            {
                if (DeviceName != null
                    && DeviceName.StartsWith(DeviceNamePrefix, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(DeviceName.Substring(DeviceNamePrefix.Length), out int number))
                {
                    return number.ToString();
                }
                return "?";
            }
        }
    }

    internal static class MonitorManager
    {
        private const uint MONITORINFOF_PRIMARY = 1;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeRect lprcMonitor, IntPtr dwData);

        private static class Native
        {
            [DllImport("user32.dll")]
            internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DisplayDevice lpDisplayDevice, uint dwFlags);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern bool EnumDisplaySettings(string lpszDeviceName, uint iModeNum, ref DevMode lpDevMode);

            [DllImport("user32.dll")]
            internal static extern IntPtr GetActiveWindow();

            [DllImport("user32.dll")]
            internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

            [DllImport("user32.dll")]
            internal static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

            [DllImport("user32.dll")]
            internal static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

            [DllImport("user32.dll")]
            internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        }

        private static readonly Dictionary<string, string> FriendlyNameCache = new Dictionary<string, string>();

        public static List<DisplayInfo> Enumerate()
        {
            var list = new List<DisplayInfo>();
            MonitorEnumProc proc = delegate (IntPtr hMonitor, IntPtr hdc, ref NativeRect rc, IntPtr data)
            {
                var info = new MonitorInfoEx { CbSize = Marshal.SizeOf(typeof(MonitorInfoEx)) };
                if (Native.GetMonitorInfo(hMonitor, ref info))
                {
                    list.Add(new DisplayInfo
                    {
                        DeviceName = NormalizeDeviceName(info.SzDevice),
                        FullName = info.SzDevice,
                        Bounds = info.RcMonitor,
                        WorkArea = info.RcWork,
                        IsPrimary = (info.DwFlags & MONITORINFOF_PRIMARY) != 0,
                        FriendlyName = GetFriendlyName(info.SzDevice)
                    });
                }
                return true;
            };
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);
            return list;
        }

        public static event Action DisplayChanged;

        public static string GetCurrentDisplayDeviceName()
        {
            IntPtr hwnd = GetGameWindowHandle();
            if (hwnd != IntPtr.Zero)
            {
                IntPtr hMonitor = Native.MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (hMonitor != IntPtr.Zero)
                {
                    var info = new MonitorInfoEx { CbSize = Marshal.SizeOf(typeof(MonitorInfoEx)) };
                    if (Native.GetMonitorInfo(hMonitor, ref info))
                    {
                        return NormalizeDeviceName(info.SzDevice);
                    }
                }
            }
            return GetPrimary()?.DeviceName;
        }

        public static string GetCurrentDisplayFullName()
        {
            IntPtr hwnd = GetGameWindowHandle();
            if (hwnd != IntPtr.Zero)
            {
                IntPtr hMonitor = Native.MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (hMonitor != IntPtr.Zero)
                {
                    var info = new MonitorInfoEx { CbSize = Marshal.SizeOf(typeof(MonitorInfoEx)) };
                    if (Native.GetMonitorInfo(hMonitor, ref info))
                    {
                        return info.SzDevice;
                    }
                }
            }
            return GetPrimary()?.FullName ?? string.Empty;
        }

        public static List<DisplayModeInfo> GetModesForMonitor(string fullDeviceName)
        {
            var modes = new List<DisplayModeInfo>();
            var mode = NewDevMode();
            for (uint i = 0; Native.EnumDisplaySettings(fullDeviceName, i, ref mode); i++)
            {
                if (mode.PelsWidth > 0 && mode.PelsHeight > 0 && mode.DisplayFrequency > 0)
                {
                    modes.Add(new DisplayModeInfo
                    {
                        Width = (int)mode.PelsWidth,
                        Height = (int)mode.PelsHeight,
                        RefreshRate = (int)mode.DisplayFrequency
                    });
                }
                mode = NewDevMode();
            }
            return modes;
        }

        public static DisplayModeInfo? GetCurrentMode(string fullDeviceName)
        {
            var mode = NewDevMode();
            if (Native.EnumDisplaySettings(fullDeviceName, uint.MaxValue, ref mode)
                && mode.PelsWidth > 0 && mode.PelsHeight > 0)
            {
                return new DisplayModeInfo
                {
                    Width = (int)mode.PelsWidth,
                    Height = (int)mode.PelsHeight,
                    RefreshRate = (int)mode.DisplayFrequency
                };
            }
            return null;
        }

        private static DevMode NewDevMode()
        {
            return new DevMode { Size = (ushort)Marshal.SizeOf(typeof(DevMode)) };
        }

        public static bool TryMoveToMonitor(string deviceName, out string error)
        {
            error = null;

            string normalized = NormalizeDeviceName(deviceName);
            var displays = Enumerate();
            var target = !string.IsNullOrEmpty(normalized)
                ? displays.Find(d => string.Equals(d.DeviceName, normalized, StringComparison.OrdinalIgnoreCase))
                : null;
            target = target ?? displays.Find(d => d.IsPrimary);
            if (target == null)
            {
                error = "No displays are currently connected.";
                return false;
            }

            IntPtr hwnd = GetGameWindowHandle();
            Log.Info($"MonitorSwitcher: move to '{normalized}' hwnd={hwnd} target={target.DeviceName} bounds={target.Bounds.Left},{target.Bounds.Top} {target.Bounds.Width}x{target.Bounds.Height} mode={Screen.fullScreenMode}");
            if (hwnd == IntPtr.Zero)
            {
                error = "Could not find the game window.";
                return false;
            }

            var savedMode = Screen.fullScreenMode;
            if (savedMode == FullScreenMode.ExclusiveFullScreen)
            {
                Screen.fullScreenMode = FullScreenMode.Windowed;
                PlaceWindow(hwnd, target.Bounds);
                Screen.fullScreenMode = savedMode;
                DisplayChanged?.Invoke();
                return true;
            }

            if (savedMode == FullScreenMode.FullScreenWindow)
            {
                PlaceWindow(hwnd, target.Bounds);
                Log.Info($"MonitorSwitcher: post-move current display = '{GetCurrentDisplayFullName()}'");
                DisplayChanged?.Invoke();
                return true;
            }

            NativeRect rect;
            if (Native.GetWindowRect(hwnd, out rect))
            {
                int width = Mathf.Clamp(rect.Width, 0, target.WorkArea.Width);
                int height = Mathf.Clamp(rect.Height, 0, target.WorkArea.Height);
                int x = target.WorkArea.Left + (target.WorkArea.Width - width) / 2;
                int y = target.WorkArea.Top + (target.WorkArea.Height - height) / 2;
                Native.MoveWindow(hwnd, x, y, width, height, true);
            }
            else
            {
                Native.MoveWindow(hwnd, target.WorkArea.Left, target.WorkArea.Top, target.WorkArea.Width, target.WorkArea.Height, true);
            }

            DisplayChanged?.Invoke();
            return true;
        }

        private static void PlaceWindow(IntPtr hwnd, NativeRect bounds)
        {
            int x = bounds.Left;
            int y = bounds.Top;
            int width = bounds.Width;
            int height = bounds.Height;
            bool result = Native.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
            Log.Info($"MonitorSwitcher: SetWindowPos({x},{y},{width}x{height}) result={result}");

            // Unity may reassert the window bounds later in the same frame (e.g. right
            // after a Screen.fullScreenMode toggle); re-apply on the next update.
            RoR2Application.onNextUpdate += () =>
            {
                if (hwnd != IntPtr.Zero)
                {
                    Native.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
            };
        }

        private static DisplayInfo GetPrimary()
        {
            return Enumerate().Find(d => d.IsPrimary);
        }

        private static string NormalizeDeviceName(string deviceName)
        {
            if (deviceName != null && deviceName.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                return deviceName.Substring(4);
            }
            return deviceName;
        }

        private static IntPtr GetGameWindowHandle()
        {
            try
            {
                IntPtr main = Process.GetCurrentProcess().MainWindowHandle;
                if (main != IntPtr.Zero)
                {
                    return main;
                }
            }
            catch
            {
            }
            return Native.GetActiveWindow();
        }

        private static string GetFriendlyName(string deviceName)
        {
            string cached;
            if (FriendlyNameCache.TryGetValue(deviceName, out cached))
            {
                return cached;
            }

            var monitor = new DisplayDevice { Cb = Marshal.SizeOf(typeof(DisplayDevice)) };
            if (Native.EnumDisplayDevices(deviceName, 1, ref monitor, 0) && !string.IsNullOrEmpty(monitor.DeviceString))
            {
                FriendlyNameCache[deviceName] = monitor.DeviceString;
                return monitor.DeviceString;
            }

            for (uint i = 0; ; i++)
            {
                var adapter = new DisplayDevice { Cb = Marshal.SizeOf(typeof(DisplayDevice)) };
                if (!Native.EnumDisplayDevices(null, i, ref adapter, 0))
                {
                    break;
                }
                if (string.Equals(adapter.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(adapter.DeviceString))
                {
                    FriendlyNameCache[deviceName] = adapter.DeviceString;
                    return adapter.DeviceString;
                }
            }

            FriendlyNameCache[deviceName] = deviceName;
            return deviceName;
        }
    }
}
