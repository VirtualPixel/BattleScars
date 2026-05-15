using BattleScars.Configuration;
using BattleScars.Services;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BattleScars
{
    [BepInPlugin("Vippy.BattleScars", "BattleScars", BuildInfo.Version)]
    public class BattleScars : BaseUnityPlugin
    {
        internal static BattleScars Instance { get; private set; } = null!;
        internal static ManualLogSource Log => Instance.Logger;

        private Harmony? _harmony;

        private void Awake()
        {
            Instance = this;

            gameObject.transform.parent = null;
            gameObject.hideFlags = HideFlags.HideAndDontSave;

            PluginConfig.Init(Config);
            BindModeRevert();

            _harmony = new Harmony(Info.Metadata.GUID);
            _harmony.PatchAll();

            Log.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} loaded.");
        }

        // Mode=Off mid-session: tear down anything currently on the local avatar.
        // Other Mode transitions are picked up by the slow tick on the next pass.
        private static void BindModeRevert()
        {
            PluginConfig.Mode.SettingChanged += (_, _) =>
            {
                var avatar = PlayerLookup.LocalAvatar();
                if (avatar == null) return;
                if (PluginConfig.Mode.Value == RunMode.Off)
                {
                    Cosmetics.RestoreToLocal(avatar);
                    Effects.CancelVoice(avatar);
                    Driver.Instance?.InvalidateAppliedCosmetics();
                }
                else if (PluginConfig.Mode.Value == RunMode.VisualOnly)
                {
                    Effects.CancelVoice(avatar);
                }
            };
        }
    }
}
