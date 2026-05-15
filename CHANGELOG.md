# Changelog

## 1.0.0

Initial release.

- Scars stack as your HP drops. Pool flips at 75 (rust), 60 (bandages), 50 (damaged), 25 (broken-mesh body parts on top). At 1 HP you're maxed out across body slots.
- Per-player. Scars broadcast through vanilla `SetupCosmeticsRPC`, so unmodded teammates still see them. Sparks ride Photon `RaiseEvent` and only reach modded peers.
- Mode setting: Off / VisualOnly / Full. VisualOnly is the default and skips the speed and stamina nerfs.
- `MetaSave.es3` copied once per session to `BepInEx/config/BattleScars/backups/<your name>/`, 5 newest kept. The mod never writes to your save.
- Numpad 0-9 previews HP thresholds without taking damage (0 off, 1 jumps to HP 1, 2-9 to HP 20-90).
