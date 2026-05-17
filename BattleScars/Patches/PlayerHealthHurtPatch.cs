using BattleScars.Services;
using HarmonyLib;

namespace BattleScars.Patches
{
    // Spark burst on damage. Tier transitions are HP-polled by the Driver, so
    // this patch exists purely to hang a one-shot effect off the actual hit
    // rather than poll the avatar's HP every frame.
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    internal static class PlayerHealthHurtPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerHealth __instance, out int __state)
        {
            __state = __instance != null ? __instance.health : 0;
        }

        [HarmonyPostfix]
        public static void Postfix(PlayerHealth __instance, int __state)
        {
            if (!ConfigService.IsEnabled() || !ConfigService.InActiveScene() || __instance == null) return;
            int actualDelta = __state - __instance.health;
            if (actualDelta <= 0) return;

            var avatar = __instance.GetComponent<PlayerAvatar>();
            if (avatar == null || string.IsNullOrEmpty(avatar.steamID)) return;
            Effects.SpawnSparks(avatar, actualDelta);
        }
    }
}
