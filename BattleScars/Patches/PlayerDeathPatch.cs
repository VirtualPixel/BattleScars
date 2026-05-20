using BattleScars.Services;
using HarmonyLib;

namespace BattleScars.Patches
{
    // Force the broken scar set onto the local Semibot the instant death is
    // declared. The slow tick would catch up within a second, but the dead head
    // is teleported in by PlayerDeathDone before then, and it reads cosmetics
    // off its own PlayerCosmetics rather than the body's. Without this prefire
    // the dead head wears the saved loadout for the first second.
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeathRPC))]
    internal static class PlayerDeathPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance)
        {
            if (__instance == null || !__instance.isLocal) return;
            ConfigService.LogDiag("local death: reasserting broken set");
            Driver.Instance?.ReassertLocalCosmeticsImmediate();
        }
    }
}
