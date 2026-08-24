# BattleScars

Your Semibot visibly falls apart as its health drops. A crack or two first, then bandages, dented plating, and limbs hanging off by the time you're one hit from dead. Heal up and it mends.

![A Semibot taking hits and scarring up, then healing clean again](https://raw.githubusercontent.com/VirtualPixel/BattleScars/main/media/damage_then_heal.gif)

R.E.P.O. puts a health bar on the back of everyone's neck, but you have to be behind them and close to read it. BattleScars writes the damage across the whole body instead, so you can read a teammate from across the room.

The part most cosmetic mods can't manage: it shows on every Semibot in the lobby, even your unmodded friends'. Damage rides the game's own cosmetic system, so everyone sees it whether they installed the mod or not. Nothing gets unlocked, saved, or written to your file. The scars wipe clean when you patch up.

**Visuals only by default.** No gameplay changes, just the look, plus a red low-health vignette creeping in at the screen edges. Want the Semibot to play as rough as it looks? Flip `Mode = Full` for a slower walk and capped stamina as the damage stacks up.

## Install

Per-player. Every player who wants the effects on their own Semibot needs the mod. Your scars broadcast to everyone via the game's normal cosmetic RPC, so unmodded teammates still see them; they just can't have their own.

If only the host installs, only the host gets scars. There's no "host enforces on everyone" path. Unmodded clients never run the code that drives `SetupCosmeticsRPC`, so nothing can be forced onto them.

## How it works

The first scar shows up at 95 HP, and another body part joins it every 6 HP you lose after that. Those numbers are the default Heavy intensity; `ScarIntensity` shifts the whole curve later (Normal, Light) if you want scars to hold off longer.

Each scarred part worsens by stacking layers rather than swapping one look for the next. A crack shows up first, then a bandage joins it, then the crack deepens into dented plating, and finally the limb breaks outright into a broken mesh. The parts don't worsen in lockstep. Whatever got scarred first runs a stage or two ahead of the newest scar, so most of the time the Semibot wears a mix. A bandaged arm next to a cracked leg next to a broken-mesh shoulder.

Rough landmarks at Heavy: the first broken-mesh limb shows around 40 HP. The broken head holds out the longest, until you're under 15. Everything between those overlaps.

Which limbs scar, and in what order, is reseeded every level, so the same HP wears differently from one level to the next. Within a level the layout holds steady and healing walks it back in reverse; it only reshuffles once you're patched back up to full health.

Max-HP upgrades buy a real buffer. 160 max HP means you can take 65 damage at Heavy before the first scar shows.

When you die your dead head wears the broken mesh, regardless of what HP the killing hit landed at.

## What broadcasts

Different effects travel different paths:

| Effect | Reaches | How |
|---|---|---|
| Scars | Everyone in the lobby | Vanilla `SetupCosmeticsRPC` |
| Speed / stamina nerfs | Everyone | Local nerfs ride position sync |
| Red screen vignette | You only | Local |

The mod adds no network traffic of its own. Scars travel on the game's existing cosmetic RPC, and the nerfs are local.

## Mode

One setting decides the overall feel:

- **Off**: Mod inactive.
- **VisualOnly** (default): Scars and the screen vignette. No movement or stamina changes.
- **Full**: Adds the move-speed and stamina nerfs on top.

## Config

Lives at `BepInEx/config/Vippy.BattleScars.cfg`. REPOConfig picks it up as an in-game settings page, edits apply live.

```
[General]
Mode = VisualOnly
ScarIntensity = Heavy

[Effects]
Vignette = true
VignetteIntensity = 0.25
CameraGlitches = false

[Testing]
TestHealth = -1
TestHotkeys = false
DebugLogging = false
```

Most players never need to touch anything except `Mode`.

`ScarIntensity` shifts the whole curve. Heavy (default) piles scars on after the first few hits, Normal is the calmer middle ground, Light holds them off until you're badly hurt.

`Vignette` toggles the red edge overlay at low HP. `VignetteIntensity` (0 to 1) is how strong it gets at its worst. `CameraGlitches` is an opt-in flavor extra, off by default: turn it on and the camera will fault and flicker occasionally once you're near death. REPO's photosensitivity accessibility setting also forces the camera glitches off and holds the vignette to a steady glow.

`TestHealth` previews thresholds without taking damage. -1 disables. 0-100 forces that HP value through the tier pipeline, no real damage taken. Teammates see the preview scars as if they were real, and the value sticks in the config file until you set it back to -1. `TestHotkeys` puts Numpad 0-9 on the same field: Numpad 0 disables, 1 jumps to HP 1, 2-9 to HP 20, 30, ... 90. Off by default so a stray numpad press mid-run can't pin fake scars on you.

`DebugLogging` dumps every scar apply and restore to the BepInEx console. Off by default; turn it on if you're filing a bug report about scars not showing up right.

## Save backup

First time the mod loads each session it copies `MetaSave.es3` to `BepInEx/config/BattleScars/backups/<your name>/`. Five newest kept. BattleScars never writes to your save, but the copy is cheap insurance.

## Compatibility

- R.E.P.O. v0.4.x
- BepInEx 5.4.2305
- MoreHead and other cosmetic mods add to the pool the discovery pass can find. Defaults match the vanilla Bandages / Cracks / Damaged / Broken sets.
- Respects REPO's photosensitivity setting. With it on, the screen vignette holds steady instead of pulsing and the glitch flashes are dropped.

Harmony targets: `PlayerCosmetics.SetupCosmetics`, `PlayerCosmetics.SetupCosmeticsLogic`, `PlayerAvatar.PlayerDeathRPC`, `StatsManager.Start`. Should coexist with any mod that doesn't replace those entirely.

## Contact

| Purpose | Where |
|---|---|
| Bug reports and suggestions | [GitHub Issues](https://github.com/VirtualPixel/BattleScars/issues) |
| Questions, test builds, or just hanging out | [Vippy's Discord](https://discord.gg/kKqhck2NrP) |
| R.E.P.O. modding in general | [R.E.P.O. Modding Server](https://discord.gg/9fDzZ9sk95) |

Everything I make stays free. If one of these mods made your runs better and you feel like saying thanks, there is a [Ko-fi](https://ko-fi.com/vippydev).

Other mods at [Vippy on Thunderstore](https://thunderstore.io/c/repo/p/Vippy/).
