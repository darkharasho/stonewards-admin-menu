using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace AdminMenu
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.darkharasho.stonewards.adminmenu";
        public const string PluginName = "AdminMenu";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
