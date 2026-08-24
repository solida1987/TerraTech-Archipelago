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
| `worlds/terratech/` | The apworld: items, locations, logic, options, client |
| `mod/TerraTechArchipelago/` | The game mod (C#, Harmony) |
| `tools/` | The generators that build the block table from the game's own data |
| `test/` | Generation test cases |

## Building

The apworld needs nothing but Python:

```
py -3.13 tools/generate_block_table.py
py -3.13 tools/build_apworld.py
```

The mod needs the .NET SDK:

```
dotnet build mod/TerraTechArchipelago -c Release
```

**No part of TerraTech is redistributed here.** The mod resolves everything it
needs from the game by name at runtime, so this repository builds from a clean
clone without a copy of the game's assemblies. The block table is generated
from the game's own `BlockTypes` enum on the machine that has it installed.

## How it works

The mod runs inside TerraTech and talks to an Archipelago client over
`127.0.0.1:24601`, one JSON object per line. The client speaks Archipelago on
the other side.

The lock itself is the game's own: `TankBlock.LockBlockAttach()`, the same
mechanism the tutorial uses to hold back blocks you have not been shown yet.
A Harmony patch on `ManTechBuilder.CanBlockAttach` sits behind it as a
chokepoint nothing can route around.

## Credits

- The world, the mod and this repository: **solida1987**
- TerraTech is by **Payload Studios** and is not affiliated with this project
- Built on [Archipelago](https://github.com/ArchipelagoMW/Archipelago) (MIT)
  and [Harmony](https://github.com/pardeike/Harmony) (MIT)

## Licence

MIT — see [LICENSE](LICENSE).
