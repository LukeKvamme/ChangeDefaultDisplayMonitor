using BepInEx;
using RoR2;

namespace MonitorSwitcher
{
    [BepInDependency(R2API.LanguageAPI.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class MonitorSwitcherPlugin : BaseUnityPlugin
    {
        public const string PluginAuthor = "Ovalsquare";
        public const string PluginName = "MonitorSwitcher";
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginVersion = "1.0.0";

        internal static string PendingStartupMonitor;

        private void Awake()
        {
            Log.Init(Logger);
            Localization.Init();

            On.RoR2.Console.CacheConVars += OnCacheConVars;
            On.RoR2.Console.LoadStartupConfigs += OnLoadStartupConfigs;
            On.RoR2.UI.SettingsPanelController.Start += SettingsRowInjector.OnSettingsPanelStart;
            On.RoR2.UI.SettingsPanelController.OnEnable += SettingsRowInjector.OnSettingsPanelEnable;
            On.RoR2.RoR2Application.OnMainMenuControllerInitialized += OnMainMenuControllerInitialized;

            Logger.LogInfo($"{PluginGUID} AWAKE_COMPLETE");
        }

        private void OnCacheConVars(On.RoR2.Console.orig_CacheConVars orig, RoR2.Console self)
        {
            orig(self);
            DisplayMonitorConVar.Register(self);
        }

        private void OnLoadStartupConfigs(On.RoR2.Console.orig_LoadStartupConfigs orig, RoR2.Console self)
        {
            // RoR2BepInExPack's FixConVar replaces CacheConVars, so the game's own
            // convar scan never runs and the CacheConVars hook does not fire. This
            // runs after the convar dictionaries exist but before "exec config"
            // replays the archived values, so the saved monitor is applied here.
            DisplayMonitorConVar.Register(self);
            orig(self);
        }

        private void OnMainMenuControllerInitialized(On.RoR2.RoR2Application.orig_OnMainMenuControllerInitialized orig, RoR2.RoR2Application self)
        {
            orig(self);
            ApplyPendingStartupMonitor();
        }

        private static void ApplyPendingStartupMonitor()
        {
            string target = PendingStartupMonitor;
            PendingStartupMonitor = null;
            if (string.IsNullOrEmpty(target))
            {
                return;
            }
            if (MonitorManager.TryMoveToMonitor(target, out string error))
            {
                Log.Info($"MonitorSwitcher: applied saved monitor {target}.");
            }
            else
            {
                Log.Warning($"MonitorSwitcher: could not apply saved monitor {target}: {error}");
            }
        }

        private void OnDestroy()
        {
            On.RoR2.Console.CacheConVars -= OnCacheConVars;
            On.RoR2.Console.LoadStartupConfigs -= OnLoadStartupConfigs;
            On.RoR2.UI.SettingsPanelController.Start -= SettingsRowInjector.OnSettingsPanelStart;
            On.RoR2.UI.SettingsPanelController.OnEnable -= SettingsRowInjector.OnSettingsPanelEnable;
            On.RoR2.RoR2Application.OnMainMenuControllerInitialized -= OnMainMenuControllerInitialized;
            Log.Info($"{PluginGUID} unloaded");
        }
    }
}
