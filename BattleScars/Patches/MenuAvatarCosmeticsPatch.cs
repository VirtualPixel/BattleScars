using BattleScars.Services;
using HarmonyLib;

namespace BattleScars.Patches
{
    // The pause menu spins up its own PlayerAvatarMenu with its own PlayerCosmetics,
    // and so does the little expression-preview Semibot at the bottom of the screen.
    // When either refreshes it pulls from MetaManager.cosmeticEquipped (the player's
    // real saved list) and runs SetupCosmeticsLogic. Without this prefix those
    // previews would show the saved loadout while the in-game body wears scars.
    //
    // Both of these previews reflect "you", so both get scars. The cosmetic shop
    // machine, the icon maker and the world avatar are menu avatars too, but
    // they exist to show a cosmetic on its own, not your current condition, so
    // they stay excluded. PlayerAvatarMenu registers the loadout avatar as its
    // singleton instance and the expression avatar carries an expressionAvatar
    // flag. The rest have neither, so the gate is the singleton OR the flag.
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
            var menu = visuals.playerAvatarMenu;
            if (menu == null) return;
            bool isLoadout = menu == PlayerAvatarMenu.instance;
            bool isExpression = menu.expressionAvatar;
            if (!isLoadout && !isExpression) return;

            var local = PlayerLookup.LocalAvatar();
            if (local == null || string.IsNullOrEmpty(local.steamID)) return;

            int hp = Driver.EffectiveHealthFor(local);
            var forced = Cosmetics.ForcedSetForHealth(local.steamID, hp, Cosmetics.PlayerWearsHat(local));
            ConfigService.LogDiag($"{(isExpression ? "expression" : "menu")} preview hp={hp} {Cosmetics.Describe(forced)}");
            if (forced.Count == 0) return;

            var combined = Cosmetics.Merge(MetaManager.instance?.cosmeticAssets, _cosmeticEquipped, forced);
            _cosmeticEquipped = combined.ToArray();
        }
    }
}
