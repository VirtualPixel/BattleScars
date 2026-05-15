using System.Collections.Generic;
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
    // The prefix augments the input array, never mutating cosmeticEquippedRaw
    // or MetaManager state, so the saved loadout stays untouched.
    [HarmonyPatch(typeof(PlayerCosmetics), "SetupCosmeticsLogic")]
    internal static class MenuAvatarCosmeticsPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerCosmetics __instance, ref int[] _cosmeticEquipped)
        {
            if (!ConfigService.CosmeticsEnabled()) return;
            if (__instance == null) return;
            if (__instance.playerAvatarVisuals == null || !__instance.playerAvatarVisuals.isMenuAvatar) return;

            var local = PlayerLookup.LocalAvatar();
            if (local == null || string.IsNullOrEmpty(local.steamID)) return;

            int hp = ConfigService.TestHealthOverride() >= 0
                ? ConfigService.TestHealthOverride()
                : (local.playerHealth != null ? local.playerHealth.health : 100);

            int count = ConfigService.CosmeticCountForHealth(hp);
            if (count <= 0) return;

            var pool = ConfigService.PoolForHealth(hp);
            bool face = ConfigService.WreckedFaceActive(hp);

            var picks = Cosmetics.PickForCount(local.steamID, count, pool);
            var faceIndices = face ? Cosmetics.WreckedFaceIndices() : (IReadOnlyList<int>)System.Array.Empty<int>();

            var forced = new List<int>(picks.Count + faceIndices.Count);
            foreach (var idx in faceIndices) forced.Add(idx);
            foreach (var idx in picks)
                if (!forced.Contains(idx)) forced.Add(idx);

            var combined = Cosmetics.Merge(MetaManager.instance?.cosmeticAssets, _cosmeticEquipped, forced);
            _cosmeticEquipped = combined.ToArray();
        }
    }
}
