using BattleScars.Services;
using HarmonyLib;

namespace BattleScars.Patches
{
    // The pause menu spins up its own PlayerAvatarMenu with its own PlayerCosmetics.
    // When that avatar refreshes it pulls from MetaManager.cosmeticEquipped (the
    // player's real saved list) and runs SetupCosmeticsLogic. Without this
    // prefix the menu preview would show the saved loadout while the in-game
    // body wears scars.
    //
    // Only the pause-menu loadout avatar gets scars. The cosmetic shop machine,
    // the icon maker and the expression preview are all menu avatars too, but
    // they exist to show a cosmetic on its own, not your current condition.
    // PlayerAvatarMenu registers exactly the loadout avatar as its singleton
    // instance and never the others, so that's the gate.
    //
    // The prefix augments the input array, never mutating cosmeticEquippedRaw
    // or MetaManager state, so the saved loadout stays untouched.
    [HarmonyPatch(typeof(PlayerCosmetics), "SetupCosmeticsLogic")]
    internal static class MenuAvatarCosmeticsPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerCosmetics __instance, ref int[] _cosmeticEquipped)
        {
            if (!ConfigService.IsEnabled() || !ConfigService.InActiveScene()) return;
            if (__instance == null) return;

            var visuals = __instance.playerAvatarVisuals;
            if (visuals == null || !visuals.isMenuAvatar) return;
            if (visuals.playerAvatarMenu == null || visuals.playerAvatarMenu != PlayerAvatarMenu.instance) return;

            var local = PlayerLookup.LocalAvatar();
            if (local == null || string.IsNullOrEmpty(local.steamID)) return;

            int hp = Driver.EffectiveHealthFor(local);
            var forced = Cosmetics.ForcedSetForHealth(local.steamID, hp, Cosmetics.PlayerWearsHat(local));
            ConfigService.LogDiag($"menu preview hp={hp} {Cosmetics.Describe(forced)}");
            if (forced.Count == 0) return;

            var combined = Cosmetics.Merge(MetaManager.instance?.cosmeticAssets, _cosmeticEquipped, forced);
            _cosmeticEquipped = combined.ToArray();
        }
    }
}
