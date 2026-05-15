using BattleScars.Configuration;
using UnityEngine;

namespace BattleScars.Services
{
    public static class ConfigService
    {
        public static bool IsEnabled() => PluginConfig.Mode.Value != RunMode.Off;
        public static bool IsVisualOnly() => PluginConfig.Mode.Value == RunMode.VisualOnly;

        public static bool CosmeticsEnabled() => IsEnabled() && PluginConfig.EnableCosmetics.Value;
        public static bool SparkParticlesEnabled() => IsEnabled() && PluginConfig.EnableSparkParticles.Value;
        public static bool ScreenOverlayEnabled() => IsEnabled() && PluginConfig.EnableScreenOverlay.Value;

        public static bool SpeedNerfEnabled() => IsEnabled() && !IsVisualOnly() && PluginConfig.EnableSpeedNerf.Value;
        public static bool StaminaNerfEnabled() => IsEnabled() && !IsVisualOnly() && PluginConfig.EnableStaminaNerf.Value;
        public static bool VoiceEffectsEnabled() => IsEnabled() && !IsVisualOnly() && PluginConfig.EnableVoiceEffects.Value;

        public static int TestHealthOverride() => PluginConfig.TestHealth.Value;

        public static int CosmeticCountForHealth(int currentHP)
        {
            if (currentHP > PluginConfig.RustyAtOrBelowHP) return 0;
            return ((PluginConfig.RustyAtOrBelowHP - currentHP) / PluginConfig.CosmeticStepHP) + 1;
        }

        public static CosmeticPool PoolForHealth(int currentHP)
        {
            if (currentHP <= PluginConfig.DamagedAtOrBelowHP) return CosmeticPool.Damaged;
            if (currentHP <= PluginConfig.BandagesAtOrBelowHP) return CosmeticPool.Bandages;
            return CosmeticPool.Rusty;
        }

        public static bool WreckedFaceActive(int currentHP) =>
            currentHP <= PluginConfig.WreckedFaceAtOrBelowHP
            && !string.IsNullOrWhiteSpace(PluginConfig.WreckedFaceCosmetic);

        public static Tier TierForHealth(int currentHP)
        {
            int count = CosmeticCountForHealth(currentHP);
            if (count <= 0) return Tier.Healthy;
            if (count <= 2) return Tier.Scratched;
            if (count <= 4) return Tier.Damaged;
            if (count == 5) return Tier.Battered;
            return Tier.Wrecked;
        }

        public static float SpeedMultiplierFor(Tier tier) =>
            Mathf.Lerp(1f, PluginConfig.SpeedNerfMax, (int)tier / 4f);

        public static float StaminaMultiplierFor(Tier tier) =>
            Mathf.Lerp(1f, PluginConfig.StaminaNerfMax, (int)tier / 4f);
    }
}
