using UnityEngine;

namespace BattleScars.Services
{
    // Local-only red vignette + CameraGlitch pulses. Alpha scales with tier.
    // Cadence comes from a continuous HP curve so the screen feels increasingly
    // broken on the way to 1 HP instead of stepping in tier jumps. Not
    // broadcast, peers don't see your screen.
    public class ScreenOverlay : MonoBehaviour
    {
        private float _glitchTimer;

        private void Update()
        {
            if (!ConfigService.ScreenOverlayEnabled()) return;
            var avatar = PlayerLookup.LocalAvatar();
            if (avatar == null) return;
            if (avatar.deadSet || avatar.isDisabled) return;

            var tier = ConfigService.TierForHealth(Driver.EffectiveHealthFor(avatar));
            if (tier == Tier.Healthy) return;

            _glitchTimer -= Time.deltaTime;
            if (_glitchTimer > 0f) return;
            _glitchTimer = GlitchInterval(avatar) * Random.Range(0.7f, 1.3f);
            FireGlitch(tier);
        }

        private static void FireGlitch(Tier tier)
        {
            var glitch = CameraGlitch.Instance;
            if (glitch == null) return;

            // PlayLong/Short/Tiny are the neutral variants. Avoid the variants
            // that flash red or play hurt sounds so the effect reads as
            // malfunction rather than a fresh hit.
            float r = Random.value;
            if (tier >= Tier.Wrecked)
            {
                if (r < 0.65f) glitch.PlayLong();
                else glitch.PlayShort();
            }
            else if (tier >= Tier.Battered)
            {
                if (r < 0.45f) glitch.PlayLong();
                else if (r < 0.85f) glitch.PlayShort();
                else glitch.PlayTiny();
            }
            else if (tier >= Tier.Damaged)
            {
                if (r < 0.50f) glitch.PlayShort();
                else glitch.PlayTiny();
            }
            else
            {
                glitch.PlayTiny();
            }
        }

        // 1 HP -> ~1s, 10 HP -> ~2s, 25 HP -> ~4s, 50 HP -> ~7s, 60 HP -> ~9s.
        private static float GlitchInterval(PlayerAvatar avatar)
        {
            if (avatar.playerHealth == null) return 6f;
            int hp = Mathf.Max(1, avatar.playerHealth.health);
            return Mathf.Clamp(0.5f + hp * 0.15f, 1f, 12f);
        }

        private void OnGUI()
        {
            if (!ConfigService.ScreenOverlayEnabled()) return;
            var avatar = PlayerLookup.LocalAvatar();
            if (avatar == null) return;
            var tier = ConfigService.TierForHealth(Driver.EffectiveHealthFor(avatar));
            if (tier == Tier.Healthy) return;

            float t = tier switch
            {
                Tier.Scratched => 0.25f,
                Tier.Damaged => 0.50f,
                Tier.Battered => 0.75f,
                Tier.Wrecked => 1.00f,
                _ => 0f,
            };
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * (2f + t * 3f));
            float alpha = Mathf.Lerp(0f, Configuration.PluginConfig.ScreenOverlayMaxAlpha, t) * pulse;

            var prev = GUI.color;
            GUI.color = new Color(0.6f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prev;
        }
    }
}
