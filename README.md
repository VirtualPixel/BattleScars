# BattleScars

Your bot looks more wrecked the closer you get to dying. Rust spots at 75 HP, bandages at 60, cracks and damaged plating at 50, broken-mesh limbs at 25. Heal up and the scars come off in reverse.

**Default is visuals only.** No movement penalties, just the look. Flip `Mode = Full` if you want the challenge: slower walk, capped stamina, and screen glitches that scale with how close to dead you are.

Client-side, per-player. Your scars broadcast to everyone in the lobby through the game's normal cosmetic system, so unmodded teammates still see your damage. They just can't have their own without installing. Nothing gets unlocked or saved; the scars are a temporary overlay that comes off when you patch yourself back up.

## Install

Per-player. Every player who wants the effects on their own bot needs the mod. Your scars broadcast to everyone via the game's normal cosmetic RPC, so unmodded teammates still see them; they just can't have their own.

If only the host installs, only the host gets scars. There's no "host enforces on everyone" path. Unmodded clients never run the code that drives `SetupCosmeticsRPC`, so nothing can be forced onto them.

## How it works

Scar count is a continuous function of your current HP. Each pool transition swaps the cosmetic set to something visibly worse.

| HP at or below | Pool | What you see |
|---|---|---|
| 75 | Rusty | Rust overlays. Looks like wear. |
| 60 | Bandages | Wrapped body parts. Patched up. |
| 50 | Damaged | Cracks, damaged overlays. Visibly bashed. |
| 25 | + Wrecked Face | Broken-mesh body parts on top of the damaged pool. Limbs visibly destroyed. |

One scar appears at 75 HP, one more for every 8 HP lost below, up to whatever each pool can fill with distinct CosmeticTypes. At 1 HP you're maxed out across body slots with the broken-mesh set on top.

Scars are seeded by your Steam ID, so identities don't shuffle inside a run. Healing peels them off in reverse order. Same player always gets the same look.

Max-HP upgrades buy a real buffer. 160 max HP means you can take 85 damage before hitting the first scar.

## What broadcasts

Different effects travel different paths:

| Effect | Reaches | How |
|---|---|---|
| Scars | Everyone in the lobby | Vanilla `SetupCosmeticsRPC` |
| Speed / stamina nerfs | Everyone | Local nerfs ride position sync |
| Sparks on hit | Modded peers | Photon RaiseEvent 189 |
| Red screen vignette | You only | Local |

Sparks pick an arbitrary event code in the user range. Collisions with other mods are rare.

## Mode

One setting decides the overall feel:

- **Off** : Mod inactive.
- **VisualOnly** (default) : Scars, screen vignette, sparks. No movement or stamina changes.
- **Full** : Everything per the individual Effects toggles below.

In VisualOnly the speed and stamina toggles are ignored. Flip Mode to Full to use them granularly.

## Config

Lives at `BepInEx/config/Vippy.BattleScars.cfg`. REPOConfig picks it up as an in-game settings page, edits apply live.

```
[General]
Mode = VisualOnly

[Effects]
ForceBrokenCosmetics = true
SlowWhenHurt = true
DrainStamina = true
SpawnSparks = true
ScreenOverlay = true

[Testing]
TestHealth = -1
```

That's all of it. Seven settings. Most players never need to touch anything except `Mode`.

`TestHealth` is for previewing thresholds without taking damage. -1 disables. 0-100 forces that HP value through the tier pipeline, no real damage taken. Numpad 0-9 in-game drives the same field: Numpad 0 disables, Numpad 1 jumps to HP 1, Numpad 2-9 jumps to HP 20, 30, ... 90.

## Save backup

First time the mod loads each session it copies `MetaSave.es3` to `BepInEx/config/BattleScars/backups/<your name>/`. Five newest kept. BattleScars never writes to your save, but the copy is cheap insurance.

## Compatibility

- R.E.P.O. v0.4.x
- BepInEx 5.4.2305
- MoreHead and other cosmetic mods add to the pool the discovery pass can find. Defaults match the vanilla "Rusty / Bandages / Damaged / Cracks / Broken" sets.

Harmony targets: `PlayerCosmetics.SetupCosmetics`, `PlayerCosmetics.SetupCosmeticsLogic`, `PlayerHealth.Hurt`, `StatsManager.Start`. Should coexist with any mod that doesn't replace those entirely.

## Issues

[GitHub Issues](https://github.com/VirtualPixel/BattleScars/issues) for bugs. Other mods at [Vippy on Thunderstore](https://thunderstore.io/c/repo/p/Vippy/).
