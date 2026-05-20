using BattleScars.Configuration;
using UnityEngine;

namespace BattleScars.Services
{
    public static class ConfigService
    {
        public static bool IsEnabled() => PluginConfig.Mode.Value != RunMode.Off;
        public static bool IsVisualOnly() => PluginConfig.Mode.Value == RunMode.VisualOnly;

        public static bool ScreenOverlayEnabled() => IsEnabled() && PluginConfig.VignetteIntensity.Value > 0f;

        // The move-speed and stamina nerfs ride together, on only in Full mode.
        public static bool NerfsEnabled() => IsEnabled() && !IsVisualOnly();

        // REPO's photosensitivity accessibility setting. When it's on, the
        // overlay holds steady instead of pulsing and the screen glitches are
        // suppressed.
        public static bool PhotosensitivityOn() =>
            GameplayManager.instance != null && GameplayManager.instance.photosensitivity;

        // The mod runs for a whole active run: levels, the shop, and the truck.
        // RunIsLobby is the truck ("In Truck" in the vanilla status text), not
        // the pre-game lobby menu. The lobby menu, main menu, splash, tutorial
        // and arena get nothing, which keeps scars and the overlay out of the
        // menus.
        public static bool InActiveScene()
        {
            var run = RunManager.instance;
            if (run == null || run.levelCurrent == null) return false;
            return SemiFunc.RunIsLevel() || SemiFunc.RunIsShop() || SemiFunc.RunIsLobby();
        }

        // Verbose cosmetic-sync trace. Silent unless the player turns on
        // DebugLogging for a bug report.
        public static void LogDiag(string msg)
        {
            if (PluginConfig.DebugLogging.Value)
                BattleScars.Log.LogInfo("[Scars] " + msg);
        }

        // Damage taken below the first-scar threshold, floored at zero.
        public static int DamageDepth(int currentHP) =>
            Mathf.Max(0, PluginConfig.Curve.FirstScarHP - currentHP);

        // How many body slots carry a scar at this HP. One at the threshold,
        // one more for every SlotStepHP lost after.
        public static int ScarSlotCount(int currentHP)
        {
            var curve = PluginConfig.Curve;
            if (currentHP > curve.FirstScarHP) return 0;
            return DamageDepth(currentHP) / curve.SlotStepHP + 1;
        }

        // Worsening stage for a given slot. Earlier slots (lower index) have
        // been scarred longer, so they sit further down the ladder; SlotStaggerHP
        // is how much each later slot lags the one before it.
        // At zero HP every slot collapses to Broken. The stagger is there to
        // tell a damage-in-progress story; at the bottom there is no story
        // left, and pinning the head region(s) at Broken is what gets a broken
        // mesh into the slot the dead head actually renders.
        public static ScarSeverity SeverityForSlot(int currentHP, int slotIndex)
        {
            if (currentHP <= 0) return ScarSeverity.Broken;
            var curve = PluginConfig.Curve;
            int depth = DamageDepth(currentHP) - slotIndex * curve.SlotStaggerHP;
            int stage = depth / curve.SeverityStepHP;
            return (ScarSeverity)Mathf.Clamp(stage, 0, (int)ScarSeverity.Broken);
        }

        public static bool BrokenHeadActive(int currentHP) =>
            currentHP <= PluginConfig.Curve.BrokenHeadHP;

        public static Tier TierForHealth(int currentHP)
        {
            int count = ScarSlotCount(currentHP);
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
