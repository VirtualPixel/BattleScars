using BattleScars.Configuration;
using UnityEngine;

namespace BattleScars.Services
{
    // Local-only damage feedback: a red vignette hugging the screen edges plus
    // CameraGlitch pulses. The vignette stays out of the middle of the screen so
    // it reads as "you're hurt" without smothering what you're looking at, the
    // way the low-health indicator does in Call of Duty. Cadence comes off a
    // continuous HP curve so it ramps smoothly toward 1 HP. Not broadcast, peers
    // don't see your screen.
    public class ScreenOverlay : MonoBehaviour
    {
        // Vignette gradient in normalized radius: clear inside Inner, ramping
        // toward Outer. A mid-edge sits at radius 1.0, the corners near 1.4.
        // Outer is pushed well past the corners so the red never reaches a flat
        // ceiling on screen; it always reads as a gradient, never a band.
        private const float VignetteInner = 0.48f;
        private const float VignetteOuter = 1.50f;

        private float _glitchTimer;
        private Texture2D? _vignette;

        private void Awake() => _vignette = BuildVignette();

        private void OnDestroy()
        {
            if (_vignette != null) Destroy(_vignette);
        }

        private void Update()
        {
            if (!ConfigService.ScreenOverlayEnabled() || !ConfigService.InActiveScene()) return;
            var avatar = PlayerLookup.LocalAvatar();
            if (avatar == null) return;
            if (avatar.deadSet || avatar.isDisabled) return;

            var tier = ConfigService.TierForHealth(Driver.EffectiveHealthFor(avatar));
            if (tier == Tier.Healthy) return;

            // The screen glitches flash; photosensitivity mode drops them.
            if (ConfigService.PhotosensitivityOn()) return;

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
            if (!ConfigService.ScreenOverlayEnabled() || _vignette == null) return;
            if (!ConfigService.InActiveScene()) return;
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
            // Heartbeat that quickens as the tiers climb. Photosensitivity mode
            // holds it at a steady glow.
            float pulse = ConfigService.PhotosensitivityOn()
                ? 0.9f
                : 0.82f + 0.18f * Mathf.Sin(Time.time * (3.5f + t * 4f));
            float alpha = PluginConfig.OverlayIntensity.Value * t * pulse;

            var prev = GUI.color;
            GUI.color = new Color(0.7f, 0.04f, 0.04f, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _vignette, ScaleMode.StretchToFill);
            GUI.color = prev;
        }

        // White texture carrying only the vignette's alpha falloff. OnGUI tints
        // it red and scales the alpha, so the gradient lives here and the
        // intensity lives there.
        private static Texture2D BuildVignette()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.InverseLerp(VignetteInner, VignetteOuter, d);
                    // Smoothstep: an eased S-curve, so there's no hard line
                    // where the red starts. It blends out of the clear center.
                    pixels[y * size + x] = new Color(1f, 1f, 1f, edge * edge * (3f - 2f * edge));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
