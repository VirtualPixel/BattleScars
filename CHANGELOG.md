# Changelog

## 1.2.2

- The little expression-preview Semibot under the screen reflects your scars too. It used to show your saved loadout no matter how beat up you were, since vanilla only sets that preview's cosmetics up once per level. Now it mirrors the in-game body as your HP changes.

## 1.2.1

- The dead head you leave behind reads as broken now. The mod was wiping back to your saved loadout the instant the death sequence started, so the dead head used to show up untouched no matter how badly you were hurt.
- Scars push onto the dead head as well as the live body. Vanilla only sets the dead head's cosmetics up once per level so it was sitting at whatever your saved loadout looked like; the mod now keeps both in sync.
- Fixed scars stripping mid-run when the level handed you off to the truck or shop. The mod was treating any `isDisabled` flip as a tumble and wiping back to a clean Semibot, but the level-end transition also flips that flag, so the truck heal that followed left you with no scars on the wire.
- Fixed the host rendering one scar-update behind everyone else. Photon Cloud's buffered fan-out delivers cosmetic RPCs from a non-master sender to the master a step late; the mod now duplicates the apply with a direct-to-master RPC so the host stays in sync with what the rest of the lobby is seeing.
- Scars react about five times faster. Slow tick dropped from 1s to 0.2s, so the cosmetics catch the HP you're actually at instead of the one you were at last second.
- Default `ScarIntensity` is Heavy now. It was Normal; Heavy feels closer to what the mod is for.
- `Vignette` and `CameraGlitches` toggles. The vignette and the camera-fault flashes used to share the VignetteIntensity slider; now you can turn either off independently while keeping the other.

## 1.2.0

- Reworked how scars sit on the Semibot. Each limb stacks layers as it worsens now instead of swapping one look for the next: a crack first, then a bandage, then dented plating, then a broken-mesh limb. Which limbs scar, and in what order, is reseeded every level, so the same HP wears differently from one level to the next.
- Toned the low-health red overlay right down. It's an edge vignette around the screen now instead of a flat red wash over everything, closer to the low-health look in CoD.
- The vignette tracks your HP on a continuous curve and eases in and out instead of stepping. It fades on smoothly even when you load straight into a run that's already hurt, and fades back off as you heal.
- Screen glitches hold off until you're badly hurt. The scars and the vignette still start from the first scratch; only the camera faults wait for real damage.
- Dropped the sparks. They never sold as electrical arcing next to the rest of the effects, just generic particles, so out they came.
- Fixed scars bleeding onto the cosmetic shop machine's preview Semibot. That one's for showing what you'd unlock, it shouldn't look beat up.
- Scoped the whole mod to an active run. Scars, the overlay and the glitches used to carry over into the main menu after you died and left. Now it runs across levels, the truck and the shop, and only strips the Semibot clean once the run ends.
- New ScarIntensity setting. Light holds scars off until you're badly hurt, Heavy piles them on after the first couple of hits, Normal is the tuned default.
- New VignetteIntensity slider, so you can dial the red vignette to taste or turn it off.
- The overlay and the screen glitches now respect REPO's photosensitivity setting: steady glow, no pulsing or flashing when it's on.
- Dropped the voice-distortion effect. It never worked reliably and a half-finished toggle wasn't worth keeping.
- Added a DebugLogging toggle that traces scar changes to the console, for bug reports.

## 1.1.0

Reworked how scars escalate. It used to be a hard pool swap at each HP threshold, so every scarred part looked the same at once. Now each part worsens on its own track (bandages, cracks, dented plating, broken-mesh) and the tracks are staggered, so the Semibot wears a mix instead of one uniform stage.

- Rust dropped from the pool. It read as wear, not battle damage.
- Broken-mesh limbs start showing around 30 HP instead of the whole set snapping in at 25.
- The broken head is now the last scar to land, held back until you're under 10 HP.
- Lots more overlap between stages. A bandaged arm and a broken-mesh leg at the same time is normal now.
- Removed Herobrine.

Nerfs, sparks and the save backup are untouched.

## 1.0.0

Initial release.

- Scars stack as your HP drops. Pool flips at 75 (rust), 60 (bandages), 50 (damaged), 25 (broken-mesh body parts on top). At 1 HP you're maxed out across body slots.
- Per-player. Scars broadcast through vanilla `SetupCosmeticsRPC`, so unmodded teammates still see them. Sparks ride Photon `RaiseEvent` and only reach modded peers.
- Mode setting: Off / VisualOnly / Full. VisualOnly is the default and skips the speed and stamina nerfs.
- `MetaSave.es3` copied once per session to `BepInEx/config/BattleScars/backups/<your name>/`, 5 newest kept. The mod never writes to your save.
- Numpad 0-9 previews HP thresholds without taking damage (0 off, 1 jumps to HP 1, 2-9 to HP 20-90).
