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
        private const float VoiceTickInterval = 0.5f;

        private float _slowTickTimer;
        private float _voiceTickTimer;
        private readonly Dictionary<string, AppliedCosmeticState> _applied = new();

        private struct AppliedCosmeticState
        {
            public int Count;
            public CosmeticPool Pool;
            public bool Face;
        }

        private void Awake()
        {
            Instance = this;
            Cosmetics.DiscoverIfNeeded();
        }

        private void Update()
        {
            HandleDevHotkeys();

            if (StatsManager.instance == null) return;

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

            _voiceTickTimer -= Time.deltaTime;
            if (_voiceTickTimer <= 0f && local != null)
            {
                _voiceTickTimer = VoiceTickInterval;
                if (tier == Tier.Healthy) Effects.CancelVoice(local);
                else Effects.ApplyVoiceTick(local, tier);
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

            int hp = EffectiveHealthFor(local);
            bool effectsOn = enabled && !deadOrDisabled;
            int count = effectsOn ? ConfigService.CosmeticCountForHealth(hp) : 0;
            CosmeticPool pool = effectsOn ? ConfigService.PoolForHealth(hp) : CosmeticPool.Rusty;
            bool face = effectsOn && ConfigService.WreckedFaceActive(hp);

            _applied.TryGetValue(local.steamID, out var was);
            if (was.Count == count && was.Pool == pool && was.Face == face) return;
            _applied[local.steamID] = new AppliedCosmeticState { Count = count, Pool = pool, Face = face };

            if (!ConfigService.CosmeticsEnabled()) return;
            if (count <= 0) Cosmetics.RestoreToLocal(local);
            else Cosmetics.ApplyForState(local, count, pool, face);
        }

        public void InvalidateAppliedCosmetics() => _applied.Clear();

        // Called by SetupCosmeticsReassertPatch when a vanilla cosmetic refresh
        // (preset switch, lobby init, late MetaManager save load) has already
        // wiped the forced state. Re-fire immediately, without waiting for the
        // slow tick.
        public void ReassertLocalCosmeticsImmediate()
        {
            var local = PlayerLookup.LocalAvatar();
            if (local == null || string.IsNullOrEmpty(local.steamID)) return;
            if (!ConfigService.IsEnabled() || !ConfigService.CosmeticsEnabled()) return;
            if (local.deadSet || local.isDisabled) return;

            int hp = EffectiveHealthFor(local);
            int count = ConfigService.CosmeticCountForHealth(hp);
            if (count <= 0) return;
            CosmeticPool pool = ConfigService.PoolForHealth(hp);
            bool face = ConfigService.WreckedFaceActive(hp);
            _applied[local.steamID] = new AppliedCosmeticState { Count = count, Pool = pool, Face = face };
            Cosmetics.ApplyForState(local, count, pool, face);
        }
    }
}
