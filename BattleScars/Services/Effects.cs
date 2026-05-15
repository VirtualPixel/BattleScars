using UnityEngine;

namespace BattleScars.Services
{
    // Per-tier nerfs and spark VFX. Speed and stamina are local; the game's
    // position sync carries the slower movement to peers without mod traffic.
    // Voice and sparks ride NetEvents so modded peers see and hear them.
    public static class Effects
    {
        public static void ApplySpeedTick(PlayerAvatar avatar, Tier tier)
        {
            if (!ConfigService.SpeedNerfEnabled() || tier == Tier.Healthy) return;
            if (avatar == null || !avatar.isLocal) return;
            var controller = PlayerController.instance;
            if (controller == null) return;

            // 0.2s override keeps the value live across two FixedUpdates, which
            // is how the vanilla override loop expects to be fed.
            controller.OverrideSpeed(ConfigService.SpeedMultiplierFor(tier), 0.2f);
        }

        public static void ApplyStaminaTick(PlayerAvatar avatar, Tier tier)
        {
            if (!ConfigService.StaminaNerfEnabled() || tier == Tier.Healthy) return;
            if (avatar == null || !avatar.isLocal) return;
            var controller = PlayerController.instance;
            if (controller == null) return;

            float cap = controller.EnergyStart * ConfigService.StaminaMultiplierFor(tier);
            if (controller.EnergyCurrent > cap)
                controller.EnergyCurrent = cap;
        }

        public static void ApplyVoiceTick(PlayerAvatar avatar, Tier tier)
        {
            if (!ConfigService.VoiceEffectsEnabled() || tier == Tier.Healthy) return;
            if (avatar == null || !avatar.isLocal) return;
            if (avatar.voiceChat == null || !avatar.voiceChatFetched) return;

            float pitch = tier switch
            {
                Tier.Scratched => 0.95f,
                Tier.Damaged => 0.88f,
                Tier.Battered => 0.78f,
                Tier.Wrecked => 0.68f,
                _ => 1f,
            };
            float oscAmount = tier switch
            {
                Tier.Scratched => 0.02f,
                Tier.Damaged => 0.05f,
                Tier.Battered => 0.10f,
                Tier.Wrecked => 0.16f,
                _ => 0f,
            };
            float oscSpeed = tier switch
            {
                Tier.Scratched => 2f,
                Tier.Damaged => 4f,
                Tier.Battered => 7f,
                Tier.Wrecked => 11f,
                _ => 0f,
            };

            // pitch, priority, lerpIn, duration, oscAmount, oscSpeed.
            // Short duration lets the 0.5s repeat cadence keep the override alive
            // without stacking.
            avatar.voiceChat.OverridePitch(pitch, 0.4f, 0.4f, 0.6f, oscAmount, oscSpeed);
            if (tier >= Tier.Battered) avatar.voiceChat.OverrideVoiceDistortion(0.6f);
            if (tier >= Tier.Wrecked) avatar.voiceChat.OverrideVolumeStutter(0.6f);

            if (SemiFunc.IsMultiplayer() && avatar.photonView != null)
                NetEvents.SendVoicePitch(avatar.photonView.ViewID, pitch, oscAmount, oscSpeed, 0.6f);
        }

        public static void CancelVoice(PlayerAvatar avatar)
        {
            if (avatar?.voiceChat == null) return;
            avatar.voiceChat.OverridePitchCancel();
            if (SemiFunc.IsMultiplayer() && avatar.photonView != null)
                NetEvents.SendVoiceCancel(avatar.photonView.ViewID);
        }

        // Spawns locally then broadcasts the world position. Damaged players are
        // in first-person and don't see their own sparks; peers do.
        public static void SpawnSparks(PlayerAvatar avatar, int hitDamage)
        {
            if (!ConfigService.SparkParticlesEnabled() || avatar == null) return;
            var pos = avatar.transform.position + Vector3.up;
            SpawnSparksAt(pos, hitDamage);
            if (SemiFunc.IsMultiplayer())
                NetEvents.SendSparkSpawn(pos, hitDamage);
        }

        public static void SpawnSparksAt(Vector3 pos, int hitDamage)
        {
            if (!ConfigService.SparkParticlesEnabled()) return;

            var go = new GameObject("BattleScars_Sparks") { transform = { position = pos } };
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = 0.5f;
            main.startSpeed = 4f;
            main.startSize = 0.06f;
            main.startColor = new Color(1f, 0.7f, 0.1f);
            main.gravityModifier = 0.8f;
            main.maxParticles = 64;
            main.duration = 0.2f;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            int burst = Mathf.Clamp(8 + hitDamage / 3, 8, 60);
            emission.SetBurst(0, new ParticleSystem.Burst(0f, burst));

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            // Uses the default sprite shader the game already loads so no custom
            // material ships with the mod.
            var mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.8f, 0.2f) };
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
            Object.Destroy(go, 1.5f);
        }
    }
}
