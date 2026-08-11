using System;
using System.Reflection;
using RoR2.UI;
using UnityEngine;

namespace MonitorSwitcher
{
    internal static class SettingsRowInjector
    {
        private static readonly FieldInfo SettingsControllersField =
            typeof(SettingsPanelController).GetField("settingsControllers", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void OnSettingsPanelStart(On.RoR2.UI.SettingsPanelController.orig_Start orig, SettingsPanelController self)
        {
            InjectRow(self);
            orig(self);
            RefreshSettingsControllersSnapshot(self);
        }

        public static void OnSettingsPanelEnable(On.RoR2.UI.SettingsPanelController.orig_OnEnable orig, SettingsPanelController self)
        {
            orig(self);
            Log.Info($"MonitorSwitcher: settings panel enabled: {self.gameObject.name}");
        }

        private static void InjectRow(SettingsPanelController panel)
        {
            try
            {
                var resolution = panel.GetComponentInChildren<ResolutionControl>(true);
                if (!resolution)
                {
                    // Not the Video tab; expected for every other settings tab.
                    return;
                }

                Transform rowTemplate = resolution.transform.parent;
                Transform list = rowTemplate != null ? rowTemplate.parent : null;
                if (list == null)
                {
                    Log.Warning("MonitorSwitcher: could not find the Video settings row list; skipping row injection.");
                    return;
                }

                GameObject clone = UnityEngine.Object.Instantiate(rowTemplate.gameObject, list);
                clone.name = "Option, Display Monitor";
                clone.transform.SetAsLastSibling();

                var cloneResolution = clone.GetComponentInChildren<ResolutionControl>(true);
                if (cloneResolution)
                {
                    UnityEngine.Object.Destroy(cloneResolution);
                }

                var label = clone.GetComponentInChildren<LanguageTextMeshController>(true);
                if (label)
                {
                    label.token = Localization.SettingNameToken;
                }

                MPDropdown keepDropdown = null;
                var dropdowns = clone.GetComponentsInChildren<MPDropdown>(true);
                for (int i = 0; i < dropdowns.Length; i++)
                {
                    if (i == 0)
                    {
                        keepDropdown = dropdowns[i];
                    }
                    else
                    {
                        dropdowns[i].gameObject.SetActive(false);
                        UnityEngine.Object.Destroy(dropdowns[i]);
                    }
                }
                if (keepDropdown == null)
                {
                    Log.Warning("MonitorSwitcher: no MPDropdown found in cloned row; skipping row injection.");
                    UnityEngine.Object.Destroy(clone);
                    return;
                }

                MonitorDropdownControl.PendingSettingName = DisplayMonitorConVar.Name;
                var control = clone.AddComponent<MonitorDropdownControl>();
                control.nameToken = Localization.SettingNameToken;
                control.nameLabel = label;
                control.settingSource = BaseSettingsControl.SettingSource.ConVar;
                control.useConfirmationDialog = true;
                control.Init(keepDropdown);

                Log.Info($"MonitorSwitcher: injected Display Monitor dropdown as last row of '{list.name}' with {MonitorManager.Enumerate().Count} display(s).");
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        private static void RefreshSettingsControllersSnapshot(SettingsPanelController panel)
        {
            try
            {
                if (SettingsControllersField != null)
                {
                    var all = panel.GetComponentsInChildren<BaseSettingsControl>(true);
                    SettingsControllersField.SetValue(panel, all);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
