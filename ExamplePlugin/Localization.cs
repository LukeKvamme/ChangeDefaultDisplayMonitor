namespace MonitorSwitcher
{
    internal static class Localization
    {
        public const string SettingNameToken = "MONITOR_SWITCHER_SETTING_NAME";
        public const string ChoiceTokenPrefix = "MONITOR_SWITCHER_DISPLAY_";

        public static void Init()
        {
            R2API.LanguageAPI.Add(SettingNameToken, "Display Monitor");
        }

        public static string ChoiceToken(int index)
        {
            return ChoiceTokenPrefix + index;
        }
    }
}
