# Changelog

## 1.2.0

- Toned the low-health red overlay right down. It's an edge vignette around the screen now instead of a flat red wash over everything, closer to the low-health look in CoD.
- Fixed scars bleeding onto the cosmetic shop machine's preview bot. That one's for showing what you'd unlock, it shouldn't look beat up.
- Scoped the whole mod to gameplay. Scars, the overlay and the glitches used to carry over into the main menu after you died and left. It only runs in levels and the shop now, and strips the bot clean on the way back to the lobby.

## 1.1.0

Reworked how scars escalate. It used to be a hard pool swap at each HP threshold, so every scarred part looked the same at once. Now each part worsens on its own track (bandages, cracks, dented plating, broken-mesh) and the tracks are staggered, so the bot wears a mix instead of one uniform stage.

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
