using UnityEngine;

namespace BattleScars.Services
{
    // Per-tier movement nerfs on the local player. Speed and stamina are
    // applied locally; the game's position sync carries the slower movement to
    // peers, so there is no mod network traffic.
    public static class Effects
    {
        public static void ApplySpeedTick(PlayerAvatar avatar, Tier tier)
        {
            if (!ConfigService.NerfsEnabled() || tier == Tier.Healthy) return;
            if (avatar == null || !avatar.isLocal) return;
            var controller = PlayerController.instance;
            if (controller == null) return;

            // 0.2s override keeps the value live across two FixedUpdates, which
            // is how the vanilla override loop expects to be fed.
            controller.OverrideSpeed(ConfigService.SpeedMultiplierFor(tier), 0.2f);
        }

        public static void ApplyStaminaTick(PlayerAvatar avatar, Tier tier)
        {
            if (!ConfigService.NerfsEnabled() || tier == Tier.Healthy) return;
            if (avatar == null || !avatar.isLocal) return;
            var controller = PlayerController.instance;
            if (controller == null) return;

            float cap = controller.EnergyStart * ConfigService.StaminaMultiplierFor(tier);
            if (controller.EnergyCurrent > cap)
                controller.EnergyCurrent = cap;
        }
    }
}
