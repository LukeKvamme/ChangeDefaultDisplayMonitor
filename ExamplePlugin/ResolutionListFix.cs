using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RoR2.UI;
using TMPro;
using UnityEngine;

namespace MonitorSwitcher
{
    // Unity snapshots Screen.resolutions from the monitor the window was on at
    // startup and never refreshes it when the window moves to another display, so
    // the vanilla Resolution dropdown is stuck on the startup monitor's mode list.
    // This hook rebuilds the resolution options from the CURRENT monitor's Win32
    // display modes (EnumDisplaySettings) so 4K becomes selectable after moving to
    // a 4K monitor without restarting the game.
    internal static class ResolutionListFix
    {
        private static readonly FieldInfo ResolutionOptionsField =
            typeof(ResolutionControl).GetField("resolutionOptions", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Type ResolutionOptionType =
            typeof(ResolutionControl).GetNestedType("ResolutionOption", BindingFlags.NonPublic);

        private static readonly ConstructorInfo ResolutionOptionCtor =
            ResolutionOptionType?.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

        private static readonly FieldInfo SizeField =
            ResolutionOptionType?.GetField("size", BindingFlags.Instance | BindingFlags.Public);

        private static readonly FieldInfo RefreshRatesField =
            ResolutionOptionType?.GetField("supportedRefreshRates", BindingFlags.Instance | BindingFlags.Public);

        public static void Hook()
        {
            On.RoR2.UI.ResolutionControl.GenerateResolutionOptions += OnGenerateResolutionOptions;
        }

        public static void Unhook()
        {
            On.RoR2.UI.ResolutionControl.GenerateResolutionOptions -= OnGenerateResolutionOptions;
        }

        // Called after a monitor change while the settings panel is open, so the
        // resolution dropdown reflects the new monitor immediately instead of only
        // when the panel is re-enabled.
        public static void RefreshActiveControls()
        {
            foreach (var control in UnityEngine.Object.FindObjectsOfType<ResolutionControl>())
            {
                try
                {
                    RebuildFromCurrentMonitor(control);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        private static void OnGenerateResolutionOptions(On.RoR2.UI.ResolutionControl.orig_GenerateResolutionOptions orig, ResolutionControl self)
        {
            orig(self);
            try
            {
                RebuildFromCurrentMonitor(self);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        private static void RebuildFromCurrentMonitor(ResolutionControl self)
        {
            if (ResolutionOptionsField == null || ResolutionOptionType == null || ResolutionOptionCtor == null
                || SizeField == null || RefreshRatesField == null || !self.resolutionDropdown)
            {
                return;
            }

            string device = MonitorManager.GetCurrentDisplayFullName();
            if (string.IsNullOrEmpty(device))
            {
                return;
            }

            var modes = MonitorManager.GetModesForMonitor(device);
            if (modes.Count == 0)
            {
                return;
            }

            var ordered = modes.OrderByDescending(m => (long)m.Width * m.Height).ThenByDescending(m => m.RefreshRate).ToList();
            var sizes = new List<Vector2Int>();
            var ratesBySize = new Dictionary<Vector2Int, List<int>>();
            foreach (var mode in ordered)
            {
                var size = new Vector2Int(mode.Width, mode.Height);
                List<int> rates;
                if (!ratesBySize.TryGetValue(size, out rates))
                {
                    rates = new List<int>();
                    ratesBySize[size] = rates;
                    sizes.Add(size);
                }
                if (!rates.Contains(mode.RefreshRate))
                {
                    rates.Add(mode.RefreshRate);
                }
            }

            var options = Array.CreateInstance(ResolutionOptionType, sizes.Count);
            var optionDatas = new TMP_Dropdown.OptionData[sizes.Count];
            for (int i = 0; i < sizes.Count; i++)
            {
                object option = ResolutionOptionCtor.Invoke(null);
                SizeField.SetValue(option, sizes[i]);
                var rates = (List<int>)RefreshRatesField.GetValue(option);
                rates.AddRange(ratesBySize[sizes[i]]);
                options.SetValue(option, i);
                optionDatas[i] = new TMP_Dropdown.OptionData($"{sizes[i].x}x{sizes[i].y}");
            }

            ResolutionOptionsField.SetValue(self, options);
            self.resolutionDropdown.ClearOptions();
            self.resolutionDropdown.AddOptions(optionDatas.ToList());

            var current = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            int selected = sizes.FindIndex(s => s == current);
            if (selected < 0)
            {
                var currentMode = MonitorManager.GetCurrentMode(device);
                if (currentMode.HasValue)
                {
                    selected = sizes.FindIndex(s => s == new Vector2Int(currentMode.Value.Width, currentMode.Value.Height));
                }
            }
            if (selected < 0)
            {
                selected = 0;
            }
            self.resolutionDropdown.value = selected;

            var largest = ordered[0];
            Log.Info($"MonitorSwitcher: resolution list rebuilt for {device} ({modes.Count} modes, largest {largest.Width}x{largest.Height}@{largest.RefreshRate})");
        }
    }
}
