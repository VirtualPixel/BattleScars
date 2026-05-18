using BattleScars.Services;
using HarmonyLib;

namespace BattleScars.Patches
{
    // Vanilla code calls PlayerCosmetics.SetupCosmetics on avatar startup,
    // lobby load, preset switch, and late-init from MetaManager.saveReady.
    // Those calls pass _forced=false, overwriting cosmeticEquippedRaw and
    // re-rendering without the forced scars.
    //
    // Before this patch existed, the Driver's "last applied" cache covered the
    // common path but a vanilla refresh between two stable HP ticks would wipe
    // the scars and leave them off until the next HP change.
    //
    // This postfix catches the vanilla refresh on the local in-game avatar
    // (skipping menu avatars and the mod's own forced calls) and re-fires the
    // scar apply immediately.
    [HarmonyPatch(typeof(PlayerCosmetics), nameof(PlayerCosmetics.SetupCosmetics))]
    internal static class SetupCosmeticsReassertPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerCosmetics __instance, bool _forced)
        {
            if (CosmeticReassertGuard.IsInside) return;
            if (_forced) return;
            if (__instance == null || __instance.playerAvatarVisuals == null) return;
            if (__instance.playerAvatarVisuals.isMenuAvatar) return;

            var avatar = __instance.playerAvatarVisuals.playerAvatar;
            if (avatar == null || !avatar.isLocal) return;

            ConfigService.LogDiag("vanilla cosmetic refresh on the local body, reasserting");
            Driver.Instance?.ReassertLocalCosmeticsImmediate();
        }
    }
}
