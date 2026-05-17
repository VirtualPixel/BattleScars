using BepInEx.Configuration;

namespace BattleScars.Configuration
{
    public enum RunMode
    {
        Off = 0,
        VisualOnly = 1,
        Full = 2
    }

    internal static class PluginConfig
    {
        // Scar policy. The first scar shows at FirstScarHP and another body slot
        // joins it every SlotStepHP below that. Each slot worsens one stage
        // (bandages -> cracks -> damaged -> broken) every SeverityStepHP, and
        // SlotStaggerHP delays each later slot's worsening so the bot wears a
        // mix of stages at once instead of flipping wholesale. The broken-mesh
        // head is held back until BrokenHeadHP. Tweak by recompiling; not config
        // because most players never touch these.
        public const int FirstScarHP = 75;
        public const int SlotStepHP = 8;
        public const int SeverityStepHP = 15;
        public const int SlotStaggerHP = 7;
        public const int BrokenHeadHP = 9;

        // Multiplier hit at the Wrecked tier (1 HP). Lighter tiers interpolate
        // from 1.0 toward these in equal steps.
        public const float SpeedNerfMax = 0.70f;
        public const float StaminaNerfMax = 0.45f;

        // Peak alpha of the screen-edge vignette, hit at the corners on the
        // Wrecked tier. The middle of the screen stays clear.
        public const float ScreenOverlayMaxAlpha = 0.50f;

        // Substring tokens matched against asset.name and assetName,
        // case-insensitive. One token like "Bandages" picks up the whole set
        // since every member shares an assetName. The broken set is split into
        // head and body by CosmeticType after discovery, so it needs no
        // separate token.
        public const string BandagesAllowList = "Bandages";
        public const string CracksAllowList = "Cracks";
        public const string DamagedAllowList = "Damaged";
        public const string BrokenAllowList = "Broken";

        public static ConfigEntry<RunMode> Mode = null!;
        public static ConfigEntry<bool> EnableCosmetics = null!;
        public static ConfigEntry<bool> EnableSpeedNerf = null!;
        public static ConfigEntry<bool> EnableStaminaNerf = null!;
        public static ConfigEntry<bool> EnableVoiceEffects = null!;
        public static ConfigEntry<bool> EnableSparkParticles = null!;
        public static ConfigEntry<bool> EnableScreenOverlay = null!;
        public static ConfigEntry<int> TestHealth = null!;

        public static void Init(ConfigFile config)
        {
            Mode = config.Bind(
                "General", "Mode", RunMode.VisualOnly,
                "Off: mod inactive. VisualOnly: scars + screen overlay + sparks, no nerfs. Full: everything per the Effects toggles below."
            );

            EnableCosmetics = config.Bind(
                "Effects", "ForceBrokenCosmetics", true,
                "Force-apply broken cosmetics as damage stacks. Nothing is unlocked or saved."
            );
            EnableSpeedNerf = config.Bind(
                "Effects", "SlowWhenHurt", true,
                "Reduce move and sprint speed as you take damage. Ignored in VisualOnly."
            );
            EnableStaminaNerf = config.Bind(
                "Effects", "DrainStamina", true,
                "Cap max stamina as you take damage. Ignored in VisualOnly."
            );
            EnableVoiceEffects = config.Bind(
                "Effects", "BreakVoice", false,
                "Pitch wobble and distortion on your voice. Currently not working reliably; default off until fixed."
            );
            EnableSparkParticles = config.Bind(
                "Effects", "SpawnSparks", true,
                "Spark particles on hit. Other players need the mod to see them."
            );
            EnableScreenOverlay = config.Bind(
                "Effects", "ScreenOverlay", true,
                "Red damage vignette on your own screen as you take damage."
            );

            TestHealth = config.Bind(
                "Testing", "TestHealth", -1,
                new ConfigDescription(
                    "Preview a synthetic HP value. -1 disables. 0-100 forces that HP through the tier pipeline without touching real health or networked state. Numpad 0-9 in-game also drives this (0=off, 1=HP 1, 2=HP 20, etc).",
                    new AcceptableValueRange<int>(-1, 100))
            );
        }
    }
}
