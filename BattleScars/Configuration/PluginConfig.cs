using BepInEx.Configuration;

namespace BattleScars.Configuration
{
    public enum RunMode
    {
        Off = 0,
        VisualOnly = 1,
        Full = 2
    }

    // How early and how hard scars pile on. Normal is the tuned baseline.
    public enum ScarIntensity
    {
        Light = 0,
        Normal = 1,
        Heavy = 2
    }

    // One scar curve. The first scar shows at FirstScarHP and another body slot
    // joins it every SlotStepHP below that. Each slot worsens one stage
    // (bandages -> cracks -> damaged -> broken) every SeverityStepHP, and
    // SlotStaggerHP delays each later slot's worsening so the bot wears a mix of
    // stages at once instead of flipping wholesale. The broken-mesh head is held
    // back until BrokenHeadHP.
    public sealed class ScarCurve
    {
        public readonly int FirstScarHP;
        public readonly int SlotStepHP;
        public readonly int SeverityStepHP;
        public readonly int SlotStaggerHP;
        public readonly int BrokenHeadHP;

        public ScarCurve(int firstScarHP, int slotStepHP, int severityStepHP, int slotStaggerHP, int brokenHeadHP)
        {
            FirstScarHP = firstScarHP;
            SlotStepHP = slotStepHP;
            SeverityStepHP = severityStepHP;
            SlotStaggerHP = slotStaggerHP;
            BrokenHeadHP = brokenHeadHP;
        }
    }

    internal static class PluginConfig
    {
        // One curve per intensity. Light holds scars off longer and worsens
        // them slowly. Heavy starts them after the first few hits and races up
        // the stages.
        private static readonly ScarCurve LightCurve  = new(60, 11, 22, 9, 5);
        private static readonly ScarCurve NormalCurve = new(75, 8, 15, 7, 9);
        private static readonly ScarCurve HeavyCurve  = new(95, 6, 11, 5, 14);

        public static ScarCurve Curve => Intensity.Value switch
        {
            ScarIntensity.Light => LightCurve,
            ScarIntensity.Heavy => HeavyCurve,
            _ => NormalCurve,
        };

        // Multiplier hit at the Wrecked tier (1 HP). Lighter tiers interpolate
        // from 1.0 toward these in equal steps.
        public const float SpeedNerfMax = 0.70f;
        public const float StaminaNerfMax = 0.45f;

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
        public static ConfigEntry<ScarIntensity> Intensity = null!;
        public static ConfigEntry<bool> EnableCosmetics = null!;
        public static ConfigEntry<bool> EnableSpeedNerf = null!;
        public static ConfigEntry<bool> EnableStaminaNerf = null!;
        public static ConfigEntry<bool> EnableSparkParticles = null!;
        public static ConfigEntry<bool> EnableScreenOverlay = null!;
        public static ConfigEntry<float> OverlayIntensity = null!;
        public static ConfigEntry<int> TestHealth = null!;
        public static ConfigEntry<bool> DebugLogging = null!;

        public static void Init(ConfigFile config)
        {
            Mode = config.Bind(
                "General", "Mode", RunMode.VisualOnly,
                "Off: mod inactive. VisualOnly: scars + screen overlay + sparks, no nerfs. Full: everything per the Effects toggles below."
            );
            Intensity = config.Bind(
                "General", "ScarIntensity", ScarIntensity.Normal,
                "How early and how hard scars build up. Light starts late and worsens slowly, Heavy starts after the first few hits."
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
            EnableSparkParticles = config.Bind(
                "Effects", "SpawnSparks", true,
                "Spark particles on hit. Other players need the mod to see them."
            );
            EnableScreenOverlay = config.Bind(
                "Effects", "ScreenOverlay", true,
                "Red damage vignette around the edges of your own screen as you take damage."
            );
            OverlayIntensity = config.Bind(
                "Effects", "OverlayIntensity", 0.35f,
                new ConfigDescription(
                    "How strong the damage vignette gets at its worst. 0 hides it, 1 is intense.",
                    new AcceptableValueRange<float>(0f, 1f))
            );

            TestHealth = config.Bind(
                "Testing", "TestHealth", -1,
                new ConfigDescription(
                    "Preview a synthetic HP value. -1 disables. 0-100 forces that HP through the tier pipeline without touching real health or networked state. Numpad 0-9 in-game also drives this (0=off, 1=HP 1, 2=HP 20, etc).",
                    new AcceptableValueRange<int>(-1, 100))
            );
            DebugLogging = config.Bind(
                "Testing", "DebugLogging", false,
                "Log every scar apply, restore and reassert to the BepInEx console. For bug reports; leave off otherwise."
            );
        }
    }
}
