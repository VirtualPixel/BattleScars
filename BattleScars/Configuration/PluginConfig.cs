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
        // Tier policy. The cosmetic pool flips at each HP threshold and one more
        // scar is added per CosmeticStepHP below the rusty cutoff. Tweak by
        // recompiling; not config because most players never touch these.
        public const int RustyAtOrBelowHP = 75;
        public const int BandagesAtOrBelowHP = 60;
        public const int DamagedAtOrBelowHP = 50;
        public const int WreckedFaceAtOrBelowHP = 25;
        public const int CosmeticStepHP = 8;

        // Multiplier hit at the Wrecked tier (1 HP). Lighter tiers interpolate
        // from 1.0 toward these in equal steps.
        public const float SpeedNerfMax = 0.70f;
        public const float StaminaNerfMax = 0.45f;

        public const float ScreenOverlayMaxAlpha = 0.20f;

        // Substring tokens matched against asset.name and assetName,
        // case-insensitive. One token like "Rusty" picks up the whole set since
        // every member shares an assetName.
        public const string RustyAllowList = "Rusty";
        public const string BandagesAllowList = "Bandages";
        public const string DamagedAllowList = "Damaged,Cracks";
        public const string WreckedFaceCosmetic = "Broken";

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
