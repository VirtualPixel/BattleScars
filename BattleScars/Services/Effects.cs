using UnityEngine;

namespace BattleScars.Services
{
    // Per-tier movement nerfs on the local player. Speed and stamina are
    // applied locally; the game's position sync carries the slower movement to
    // peers, so there is no mod network traffic.
    public static class Effects
    {
        // ApplySpeedTick runs every Update(), so the override call itself is
        // throttled: re-fire on a multiplier change, or every RefreshInterval
        // to keep the game's own timer topped up, well inside the 0.2s window
        // it decays on. Keeps the reflection lookup off the per-frame path.
        private const float RefreshInterval = 0.1f;
        private static float _lastMultiplier = -1f;
        private static float _refreshTimer;

        public static void ApplySpeedTick(PlayerAvatar avatar, Tier tier)
        {
            if (!ConfigService.NerfsEnabled() || tier == Tier.Healthy)
            {
                _lastMultiplier = -1f;
                return;
            }
            if (avatar == null || !avatar.isLocal) return;
            var controller = PlayerController.instance;
            if (controller == null) return;

            float multiplier = ConfigService.SpeedMultiplierFor(tier);
            _refreshTimer -= Time.deltaTime;
            if (multiplier == _lastMultiplier && _refreshTimer > 0f) return;

            _lastMultiplier = multiplier;
            _refreshTimer = RefreshInterval;
            GameCall.Invoke(controller, typeof(PlayerController), nameof(PlayerController.OverrideSpeed), multiplier, 0.2f);
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
