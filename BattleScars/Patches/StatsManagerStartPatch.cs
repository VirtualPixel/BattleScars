using BattleScars.Services;
using HarmonyLib;
using UnityEngine;

namespace BattleScars.Patches
{
    // Spawn the runtime MonoBehaviours once StatsManager is up, by which point
    // the local PlayerAvatar.steamID is populated.
    [HarmonyPatch(typeof(StatsManager), "Start")]
    internal static class StatsManagerStartPatch
    {
        private static GameObject? _services;

        [HarmonyPostfix]
        public static void Postfix()
        {
            if (_services != null) return;
            _services = new GameObject("BattleScars_Services");
            _services.AddComponent<Driver>();
            _services.AddComponent<ScreenOverlay>();
            Object.DontDestroyOnLoad(_services);
        }
    }
}
