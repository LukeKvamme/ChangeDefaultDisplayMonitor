using System.Reflection;
using RoR2;
using RoR2.ConVar;

namespace MonitorSwitcher
{
    internal static class DisplayMonitorConVar
    {
        public const string Name = "display_monitor";
        public static readonly BaseConVar Instance = new MonitorConVarImpl();

        private static bool _registered;

        public static void Register(RoR2.Console console)
        {
            if (_registered)
            {
                return;
            }
            if (console.FindConVar(Name) != null)
            {
                _registered = true;
                return;
            }
            _registered = true;

            MethodInfo method = typeof(RoR2.Console).GetMethod("RegisterConVarInternal", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                Log.Error("MonitorSwitcher: could not find Console.RegisterConVarInternal");
                return;
            }
            method.Invoke(console, new object[] { Instance });
            Log.Info($"MonitorSwitcher: registered convar '{Name}'");
        }

        private sealed class MonitorConVarImpl : BaseConVar
        {
            public MonitorConVarImpl()
                : base(Name, ConVarFlags.Archive, null,
                    "Which display the game window is shown on. Valid values are DISPLAY1, DISPLAY2, ... as listed by the Display Monitor option in the settings menu.")
            {
            }

            public override string GetString()
            {
                return MonitorManager.GetCurrentDisplayDeviceName() ?? string.Empty;
            }

            public override void SetString(string newValue)
            {
                Log.Info($"MonitorSwitcher: SetString('{newValue}') loadFinished={RoR2Application.loadFinished}");
                if (string.IsNullOrEmpty(newValue))
                {
                    MonitorManager.TryMoveToMonitor(null, out _);
                    return;
                }

                if (!RoR2Application.loadFinished)
                {
                    // Startup: archived convars are replayed by "exec config" while the
                    // game is still loading. Defer the window move until the menu is up.
                    MonitorSwitcherPlugin.PendingStartupMonitor = newValue;
                    return;
                }

                if (!MonitorManager.TryMoveToMonitor(newValue, out string error))
                {
                    throw new ConCommandException(error ?? $"Unknown display '{newValue}'.");
                }
            }
        }
    }
}
