using System;
using System.Collections.Generic;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MonitorSwitcher
{
    internal class MonitorDropdownControl : BaseSettingsControl
    {
        // BaseSettingsControl.Awake logs "Null convar {0} detected in options" if
        // settingName is empty when the component is added. The injector sets this
        // just before AddComponent so Awake sees a valid convar name.
        internal static string PendingSettingName;

        public MPDropdown dropdown;

        private DisplayInfo[] _displays = Array.Empty<DisplayInfo>();
        private bool _syncing;

        protected new void Awake()
        {
            if (string.IsNullOrEmpty(settingName))
            {
                settingName = PendingSettingName;
            }
            base.Awake();
        }

        protected override void OnUpdateControls()
        {
            RefreshOptions();
        }

        internal void Init(MPDropdown targetDropdown)
        {
            dropdown = targetDropdown;
            if (dropdown)
            {
                dropdown.onValueChanged.RemoveAllListeners();
                dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            }
            RefreshOptions();
        }

        private void RefreshOptions()
        {
            if (!dropdown)
            {
                return;
            }

            _displays = MonitorManager.Enumerate().ToArray();
            string current = GetCurrentValue();
            int selected = 0;
            var options = new List<TMP_Dropdown.OptionData>(_displays.Length);
            for (int i = 0; i < _displays.Length; i++)
            {
                options.Add(new TMP_Dropdown.OptionData(_displays[i].Label));
                if (string.Equals(_displays[i].DeviceName, current, StringComparison.OrdinalIgnoreCase))
                {
                    selected = i;
                }
            }

            _syncing = true;
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(selected);
            _syncing = false;
        }

        private void OnDropdownValueChanged(int value)
        {
            if (_syncing)
            {
                return;
            }
            if (value < 0 || value >= _displays.Length)
            {
                return;
            }
            SubmitSetting(_displays[value].DeviceName);
        }
    }
}
