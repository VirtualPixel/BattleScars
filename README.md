# BattleScars

Your bot looks more wrecked the closer you get to dying. First scar around 75 HP, then it piles on: bandages, cracks, dented plating, and broken-mesh limbs by the time you're scraping bottom. Heal up and the scars come off in reverse.

**Default is visuals only.** No movement penalties, just the look. Flip `Mode = Full` if you want the challenge: slower walk, capped stamina, and screen glitches that scale with how close to dead you are.

Client-side, per-player. Your scars broadcast to everyone in the lobby through the game's normal cosmetic system, so unmodded teammates still see your damage. They just can't have their own without installing. Nothing gets unlocked or saved; the scars are a temporary overlay that comes off when you patch yourself back up.

## Install

Per-player. Every player who wants the effects on their own bot needs the mod. Your scars broadcast to everyone via the game's normal cosmetic RPC, so unmodded teammates still see them; they just can't have their own.

If only the host installs, only the host gets scars. There's no "host enforces on everyone" path. Unmodded clients never run the code that drives `SetupCosmeticsRPC`, so nothing can be forced onto them.

## How it works

The first scar shows up at 75 HP, and another body part joins it every 8 HP you lose after that.

Each scarred part has its own condition that worsens as you keep dropping: bandages, then cracks, then dented plating, then a full broken-mesh limb. The parts don't worsen in lockstep. Whatever got scarred first runs a stage or two ahead of the newest scar, so most of the time the bot wears a mix. A bandaged arm next to a cracked leg next to a broken-mesh shoulder.

Rough landmarks: the first broken-mesh limb turns up around 30 HP. The broken head holds out the longest, until you're under 10. Everything between those overlaps. Those numbers are the Normal intensity; `ScarIntensity` in the config shifts the whole curve earlier or later.

Scars are seeded by your Steam ID. Same player, same HP, same look, and healing walks it back in reverse.

Max-HP upgrades buy a real buffer. 160 max HP means you can take 85 damage before the first scar.

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
ScarIntensity = Normal

[Effects]
ForceBrokenCosmetics = true
SlowWhenHurt = true
DrainStamina = true
SpawnSparks = true
ScreenOverlay = true
OverlayIntensity = 0.5

[Testing]
TestHealth = -1
DebugLogging = false
```

That's all of it. Ten settings. Most players never need to touch anything except `Mode`.

`ScarIntensity` shifts the whole curve. Light holds scars off until you're badly hurt, Heavy starts them after the first couple of hits, Normal is the tuned default. `OverlayIntensity` is how strong the red screen vignette gets, 0 to 1.

`TestHealth` is for previewing thresholds without taking damage. -1 disables. 0-100 forces that HP value through the tier pipeline, no real damage taken. Numpad 0-9 in-game drives the same field: Numpad 0 disables, Numpad 1 jumps to HP 1, Numpad 2-9 jumps to HP 20, 30, ... 90.

`DebugLogging` dumps every scar apply and restore to the BepInEx console. Off by default; turn it on if you're filing a bug report about scars not showing up right.

## Save backup

First time the mod loads each session it copies `MetaSave.es3` to `BepInEx/config/BattleScars/backups/<your name>/`. Five newest kept. BattleScars never writes to your save, but the copy is cheap insurance.

## Compatibility

- R.E.P.O. v0.4.x
- BepInEx 5.4.2305
- MoreHead and other cosmetic mods add to the pool the discovery pass can find. Defaults match the vanilla Bandages / Cracks / Damaged / Broken sets.
- Respects REPO's photosensitivity setting. With it on, the screen vignette holds steady instead of pulsing and the glitch flashes are dropped.

Harmony targets: `PlayerCosmetics.SetupCosmetics`, `PlayerCosmetics.SetupCosmeticsLogic`, `PlayerHealth.Hurt`, `StatsManager.Start`. Should coexist with any mod that doesn't replace those entirely.

## Issues

[GitHub Issues](https://github.com/VirtualPixel/BattleScars/issues) for bugs. Other mods at [Vippy on Thunderstore](https://thunderstore.io/c/repo/p/Vippy/).
