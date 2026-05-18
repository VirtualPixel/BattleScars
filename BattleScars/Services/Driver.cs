using System.Collections.Generic;
using BattleScars.Configuration;
using UnityEngine;

namespace BattleScars.Services
{
    // DontDestroyOnLoad MonoBehaviour that holds runtime state. Spawned once
    // from StatsManagerStartPatch. Three responsibilities:
    //  1. Per-frame nerfs on the local player (re-apply OverrideSpeed every tick).
    //  2. Slow tick that reapplies cosmetics when the HP-derived state changes.
    //  3. Dev-only Numpad 0-9 hotkeys that drive PluginConfig.TestHealth.
    public class Driver : MonoBehaviour
    {
        public static Driver? Instance { get; private set; }

        private const float SlowTickInterval = 1f;

        private float _slowTickTimer;
        private bool _wasActive;

        // Last scar set forced onto each player, keyed by steamID. The slow tick
        // only re-applies when the set actually changes.
        private readonly Dictionary<string, List<int>> _applied = new();

        private void Awake()
        {
            Instance = this;
            Cosmetics.DiscoverIfNeeded();
        }

        private void Update()
        {
            HandleDevHotkeys();

            // DontDestroyOnLoad keeps this ticking through the menus. Outside an
            // active level/shop, strip the local bot back to clean and stop.
            if (StatsManager.instance == null || !ConfigService.InActiveScene())
            {
                if (_wasActive)
                {
                    ConfigService.LogDiag("left the active scene, tearing down");
                    TeardownLocal();
                    _wasActive = false;
                }
                return;
            }
            if (!_wasActive)
                ConfigService.LogDiag($"entered active scene level={RunManager.instance?.levelCurrent?.name}");
            _wasActive = true;

            var local = PlayerLookup.LocalAvatar();
            bool enabled = ConfigService.IsEnabled();
            bool deadOrDisabled = local != null && (local.deadSet || local.isDisabled);

            Tier tier = Tier.Healthy;
            if (enabled && !deadOrDisabled && local != null)
                tier = ConfigService.TierForHealth(EffectiveHealthFor(local));

            if (local != null)
            {
                Effects.ApplySpeedTick(local, tier);
                Effects.ApplyStaminaTick(local, tier);
            }

            _slowTickTimer -= Time.deltaTime;
            if (_slowTickTimer > 0f) return;
            _slowTickTimer = SlowTickInterval;
            SlowTick(local, enabled, deadOrDisabled);
        }

        // Numpad 0 disables, 1 jumps to HP 1, 2-9 jump to HP 20, 30, ... 90.
        private void HandleDevHotkeys()
        {
            int? target = null;
            if (Input.GetKeyDown(KeyCode.Keypad0)) target = -1;
            else if (Input.GetKeyDown(KeyCode.Keypad1)) target = 1;
            else if (Input.GetKeyDown(KeyCode.Keypad2)) target = 20;
            else if (Input.GetKeyDown(KeyCode.Keypad3)) target = 30;
            else if (Input.GetKeyDown(KeyCode.Keypad4)) target = 40;
            else if (Input.GetKeyDown(KeyCode.Keypad5)) target = 50;
            else if (Input.GetKeyDown(KeyCode.Keypad6)) target = 60;
            else if (Input.GetKeyDown(KeyCode.Keypad7)) target = 70;
            else if (Input.GetKeyDown(KeyCode.Keypad8)) target = 80;
            else if (Input.GetKeyDown(KeyCode.Keypad9)) target = 90;
            if (!target.HasValue) return;
            if (PluginConfig.TestHealth.Value == target.Value) return;
            PluginConfig.TestHealth.Value = target.Value;
            BattleScars.Log.LogInfo($"[Dev] TestHealth -> {(target.Value < 0 ? "off" : target.Value.ToString())}");
            InvalidateAppliedCosmetics();
        }

        public static int EffectiveHealthFor(PlayerAvatar avatar)
        {
            int test = PluginConfig.TestHealth.Value;
            if (test >= 0) return test;
            return avatar.playerHealth != null ? avatar.playerHealth.health : 0;
        }

        private void SlowTick(PlayerAvatar? local, bool enabled, bool deadOrDisabled)
        {
            SaveBackup.TryBackupOnce(local);
            if (local == null || string.IsNullOrEmpty(local.steamID)) return;
            if (!ConfigService.CosmeticsEnabled()) return;

            int hp = EffectiveHealthFor(local);
            var forced = enabled && !deadOrDisabled
                ? Cosmetics.ForcedSetForHealth(local.steamID, hp)
                : new List<int>();

            if (_applied.TryGetValue(local.steamID, out var was) && SameSet(was, forced))
            {
                ConfigService.LogDiag($"tick hp={hp} dead={deadOrDisabled} scars={forced.Count} -> skip (no change)");
                return;
            }

            var previous = was ?? new List<int>();
            _applied[local.steamID] = forced;

            ConfigService.LogDiag(
                $"tick hp={hp} dead={deadOrDisabled} scars={forced.Count} -> {(forced.Count == 0 ? "restore" : "apply")}");
            ConfigService.LogDiag($"  diff: {Cosmetics.DescribeDiff(previous, forced)}");
            ConfigService.LogDiag($"  set:  {Cosmetics.Describe(forced)}");

            if (forced.Count == 0) Cosmetics.RestoreToLocal(local);
            else Cosmetics.Apply(local, forced);
        }

        public void InvalidateAppliedCosmetics() => _applied.Clear();

        // Leaving an active scene: pull scars and the voice override off the
        // local bot so it returns to the lobby looking like itself.
        private void TeardownLocal()
        {
            var local = PlayerLookup.LocalAvatar();
            ConfigService.LogDiag($"teardown localAvatar={(local != null ? "found" : "null")}");
            if (local != null)
                Cosmetics.RestoreToLocal(local);
            _applied.Clear();
        }

        // Called by SetupCosmeticsReassertPatch when a vanilla cosmetic refresh
        // (preset switch, lobby init, late MetaManager save load) has already
        // wiped the forced state. Re-fire immediately, without waiting for the
        // slow tick.
        public void ReassertLocalCosmeticsImmediate()
        {
            var local = PlayerLookup.LocalAvatar();
            if (local == null || string.IsNullOrEmpty(local.steamID)) return;
            if (!ConfigService.IsEnabled() || !ConfigService.CosmeticsEnabled())
            {
                ConfigService.LogDiag("reassert skipped: mod or cosmetics disabled");
                return;
            }
            if (!ConfigService.InActiveScene())
            {
                ConfigService.LogDiag("reassert skipped: not in an active scene");
                return;
            }
            if (local.deadSet || local.isDisabled)
            {
                ConfigService.LogDiag("reassert skipped: local avatar dead or disabled");
                return;
            }

            var forced = Cosmetics.ForcedSetForHealth(local.steamID, EffectiveHealthFor(local));
            if (forced.Count == 0)
            {
                ConfigService.LogDiag("reassert: nothing to re-apply at this HP");
                return;
            }
            _applied[local.steamID] = forced;
            ConfigService.LogDiag($"reassert -> apply {Cosmetics.Describe(forced)}");
            Cosmetics.Apply(local, forced);
        }

        private static bool SameSet(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
