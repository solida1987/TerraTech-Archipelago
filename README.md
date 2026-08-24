# TerraTech Archipelago

An [Archipelago](https://archipelago.gg) multiworld integration for
[TerraTech](https://store.steampowered.com/app/285920/TerraTech/).

Every block in TerraTech is locked until the multiworld sends you the right to
use it. You can still find blocks, buy them and carry them — you just cannot
bolt them on.

**[Setup guide](worlds/terratech/docs/setup_en.md)** ·
**[Releases](https://github.com/solida1987/TerraTech-Archipelago/releases)**

## What is in this repository

| Path | What it is |
|---|---|
| `worlds/terratech/` | The Archipelago world: items, locations, logic, options, client |
| `mod/TerraTechArchipelago/` | The game mod (C#, Harmony) |

## Building it yourself

The mod:

```
dotnet build mod/TerraTechArchipelago -c Release
```

Both are attached to every [release](https://github.com/solida1987/TerraTech-Archipelago/releases) already built.

The world is plain Python — zip `worlds/terratech/` as `terratech.apworld`
with the folder inside it.

**No part of TerraTech is redistributed here.** The mod resolves everything it
needs from the game by name at runtime (`mod/TerraTechArchipelago/Reflect.cs`),
so this repository builds from a clean clone without a copy of the game's
assemblies.

## How it works

The mod runs inside TerraTech and talks to an Archipelago client over
`127.0.0.1:24601`, one JSON object per line. The client speaks Archipelago on
the other side.

The lock itself is the game's own: `TankBlock.LockBlockAttach()`, the same
mechanism the tutorial uses to hold back blocks you have not been shown yet.
A Harmony patch on `ManTechBuilder.CanBlockAttach` sits behind it as a
chokepoint nothing can route around.

## Credits

- The Archipelago world, the game mod and this repository: **solida1987**
- **TerraTech** is by [Payload Studios](https://payloadstudios.com/). This
  project is not affiliated with them, and nothing of theirs ships here.
- [Archipelago](https://github.com/ArchipelagoMW/Archipelago) — MIT
- [Harmony](https://github.com/pardeike/Harmony) by Andreas Pardeike — MIT

## Before you install

Please read the **[disclaimer](DISCLAIMER.md)**. The short version: this is a
fan project, you need your own copy of the game, the mod changes a running
game in memory and nothing on disk, and it may break when TerraTech updates —
in which case it turns itself off and says so.

## Licence

MIT — see [LICENSE](LICENSE) and [NOTICE](NOTICE).
