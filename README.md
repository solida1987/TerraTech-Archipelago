# TerraTech Archipelago

An [Archipelago](https://archipelago.gg/) multiworld integration for
**[TerraTech](https://store.steampowered.com/app/285920/TerraTech/)**.

Every block in the game is locked until the multiworld sends you the licence
for it. You can still find blocks, buy them and carry them around — you simply
cannot bolt them onto your tech until somebody's world hands you the right to.
Picking up a block type for the first time is itself a check, so the world
opens up as you explore it.

The game's own progression is left alone. Missions, licence grades and the way
enemies scale all work exactly as they always did. What changes is only *who
holds the keys to the blocks*.

| Component | Version |
|---|---|
| Archipelago world (`terratech.apworld`) | **0.1.0** |
| Game mod (`TerraTechArchipelago.zip`) | **0.1.11** |
| Multiworld Launcher plugin | **1.1.0** |
| Requires Archipelago | 0.5.0 or newer |
| Requires BepInEx | 5.x, 64-bit |

> **⚠ This is a prerelease.** Seeds generate, the bridge is proven in both
> directions, and every block lock is in place — but no one has played a seed
> from start to goal yet. Expect rough edges, and please report what you find.

**[Setup guide](worlds/terratech/docs/setup_en.md)** ·
**[Releases](https://github.com/solida1987/TerraTech-Archipelago/releases)** ·
**[Report a bug](https://github.com/solida1987/TerraTech-Archipelago/issues)**

---

## The Multiworld Launcher does all of this for you

This integration works standalone — the sections below describe the manual
road in full. But if you use the
[**Multiworld Launcher**](https://github.com/solida1987/Multiworld-Launcher),
there is nothing to install by hand and nothing to type:

- It installs `terratech.apworld` into Archipelago for you.
- It finds your TerraTech folder through Steam, installs BepInEx from
  BepInEx's own official release, and puts the mod where the loader looks.
- **It is the Archipelago client.** The launcher holds the connection and
  relays to the mod inside the game, so there is no second client window and
  no in-game login screen.

Point it at your copy of the game, press Install, then Play. Everything else
is wired.

---

## Download & Install

### Requirements

- **TerraTech** on Steam or Epic — you supply your own copy
- **[Archipelago](https://github.com/ArchipelagoMW/Archipelago/releases)** 0.5.0 or newer
- **[BepInEx](https://github.com/BepInEx/BepInEx/releases)** 5.x, 64-bit
- Windows 64-bit

### The three pieces

1. **`terratech.apworld`** → Archipelago's `custom_worlds` folder
2. **BepInEx** → extracted into your TerraTech folder (the one with
   `TerraTechWin64.exe`), then run the game once so it creates its folders
3. **`TerraTechArchipelago.zip`** → extracted into the same TerraTech folder;
   the zip is laid out so the files land in `BepInEx\plugins\`

The [setup guide](worlds/terratech/docs/setup_en.md) walks through each step
with the exact paths.

### Why BepInEx is needed

TerraTech loads no third-party code on its own. Its `QMods` folder is a
convention belonging to a community mod manager, not to the game, and the
official mod pipeline only loads Steam Workshop bundles. BepInEx is the one
loader that needs nothing from the game itself.

If you already run BepInEx for other TerraTech mods, this mod sits alongside
them and changes nothing about your setup.

### Antivirus & Windows SmartScreen

BepInEx works by placing a `winhttp.dll` next to the game, which some security
software treats as suspicious on principle. Both BepInEx and this mod are open
source and built from the code in their own repositories. If your antivirus
removes files from the game folder, whitelist the folder and install again.

---

## How to Play

1. Generate or join a multiworld with a TerraTech slot.
2. Start the Archipelago client for TerraTech, or press Play in the Multiworld
   Launcher, which is the client itself.
3. Start TerraTech and begin a **new single-player campaign**.

The mod and the client find each other on `127.0.0.1:24601` by themselves, in
either order.

### What you will see

- The blocks on your starting vehicle work normally.
- Every other block refuses to attach and shows the game's own damage red, so
  a locked block reads as familiar rather than broken.
- When a licence arrives, that block type flashes green and works from then
  on — everywhere in the world, including the ones already in your inventory.
- Vendors sell across every corporation from the start, including grades you
  have not earned. That is deliberate: an item you cannot obtain is not an
  item. Prices still climb steeply with grade, so an early high-grade block is
  an investment rather than a gift.

### Saving

Save and load as you always would. The mod keeps its bookkeeping in a file
beside your save, never inside it — a damaged mod file costs you nothing but
some re-sent checks, and your save is never touched. Playing offline works;
checks are queued and sent the next time the client connects.

---

## Features

- **Every block in the game is a lock and a check.** 1,144 blocks across all
  eight corporations, each with its own licence item.
- **Picking a block up is always allowed** and is itself a check. Attaching is
  what the multiworld gates, so exploration is never blocked.
- **Three more check families, each from something you already do:** the first
  time you buy a block type, destroying a hostile tech, and opening a crate.
  Their grade follows what you did — the block's own grade, the highest grade
  on the tech you destroyed, your licence grade with the crate's corporation.
  Each pool empties, so nothing sends you hunting a check that is already gone.
- **Two goals:** max out a number of corporations, or collect a percentage of
  every block type.
- **The game's own progression is preserved** — missions, licence grades and
  enemy scaling are untouched. Vanilla block availability is opened up so an
  early high-grade licence is something you can actually act on.
- **Milestone checks** for blocks collected and enemies destroyed.
- **DeathLink**, optional. In TerraTech the death is your tech coming apart
  where it stands: the blocks scatter and you drive back and rebuild. Nothing
  is deleted — a death costs time, never progress.
- **The mod refuses to run half-patched.** If a game update moves something it
  needs, it says exactly what is missing and changes nothing.

---

## Options

Set these in your YAML, or in the Multiworld Launcher's options screen.

| Option | Values | Default | What it does |
|---|---|---|---|
| `goal` | `licence_master`, `collector` | `licence_master` | How the seed is won |
| `corporations_to_max` | 1–8 | 3 | Corporations to max out, for `licence_master` |
| `collector_percentage` | 10–100 | 50 | Share of block types to collect, for `collector` |
| `block_pool` | `starter`, `standard`, `full` | `standard` | `starter` = GSO + Space Junkers; `standard` = grades 1–3; `full` = all 1,144 |
| `pickup_checks` | on / off | **on** | A check the first time each block type is picked up — the backbone of the pool |
| `attach_checks` | on / off | off | A second check the first time each type is attached. Roughly doubles the length |
| `shop_checks` | 0–500 | 100 | Checks for buying a block type you have never bought before |
| `enemy_checks` | 0–800 | 200 | Checks for destroying hostile techs. Harder techs draw from higher grades |
| `crate_checks` | 0–100 | 30 | Checks for opening crates |
| `quest_checks` | on / off | on | Kill and gather milestones (5, 10, 25, 50 …) |
| `trap_percentage` | 0–40 | 0 | Share of filler replaced by traps |
| `death_link` | on / off | off | Share deaths with the multiworld |

A comfortable first seed is `block_pool: standard` with pickup checks on and
attach checks off. `full` with both on is roughly 2,500 locations.

---

## Known Issues

- **Only GSO has grade locations.** "reaches Grade N" locations exist only for
  corporations whose real grade cap has been read out of a running game, and so
  far that is GSO grades 3–5. The other seven corporations still level up
  normally; they just do not carry checks yet. `corporations_to_max` is clamped
  to what exists, so a seed stays winnable either way.
- **`block_pool` splits on grades inferred from block names**, not from the
  game's own licence table. A handful of blocks may sit one grade off, so
  `standard` can include or miss a block at the edge. (The carrier families do
  read the real table, `ManLicenses.GetBlockTier`.)
- **DeathLink has two switches.** The seed's `death_link` and the launcher's
  own DeathLink toggle both have to be on. The seed wins: a seed generated with
  `death_link: false` will not send or apply deaths even if the launcher's
  toggle is on, and it says so in the log.
- The mod is built against the current Steam build of TerraTech. A game update
  can move what it patches; it will tell you rather than misbehave.

---

## What is in this repository

| Path | What it is |
|---|---|
| `worlds/terratech/` | The Archipelago world: items, locations, logic, options, client |
| `mod/TerraTechArchipelago/` | The game mod (C#, Harmony) |
| `worlds/terratech/docs/` | Setup guide and the game page shown in Archipelago |

Both pieces are attached to every
[release](https://github.com/solida1987/TerraTech-Archipelago/releases),
already built.

### Building it yourself

```bash
dotnet build mod/TerraTechArchipelago -c Release
```

The world is plain Python: zip `worlds/terratech/` as `terratech.apworld` with
the folder inside it.

**No part of TerraTech is redistributed here.** The mod resolves everything it
needs from the game by name at runtime (`mod/TerraTechArchipelago/Reflect.cs`),
so a clean clone builds without a copy of the game's assemblies.

---

## Built With

- [Harmony](https://github.com/pardeike/Harmony) — runtime patching (MIT)
- [BepInEx](https://github.com/BepInEx/BepInEx) — the mod loader, installed
  separately by the player (LGPL-2.1)
- [Archipelago](https://archipelago.gg/) — multiworld randomizer framework

## Credits

- **solida1987** — project lead, world logic, game mod, launcher integration
- **Payload Studios** — TerraTech itself. Not affiliated with this project.
- **BepInEx and Harmony teams** — the tooling that makes modding a game
  without a modding API possible at all
- **Archipelago Community** — multiworld framework and support

## License

The code in this repository is released under the terms in
[LICENSE](LICENSE). See [NOTICE](NOTICE) for what belongs to whom, and
[DISCLAIMER.md](DISCLAIMER.md) for the full disclaimer.

TerraTech is the property of Payload Studios. This project is an unofficial
modification and is not endorsed by or affiliated with them. **A legal copy of
the game is required to play.** Nothing belonging to Payload Studios is
distributed here.

## Archipelago Discord Notice

I have been permanently banned from the official Archipelago Discord server.
Because of this, please do not post or share links to this project on the
official Archipelago Discord, as this project is not permitted there.

For clarity, the ban was not related to malware, viruses, malicious code, or
any security issue with this project.

The moderation issues were related to:

- Copyright/distribution concerns involving game files in earlier versions of
  my projects. Those files were removed, the affected repositories and
  releases were cleaned up, and the distribution process was changed
  accordingly.
- Violations of the Discord server's own content rules, including
  links/content involving games that were restricted or considered 18+ under
  their server rules.

These issues relate to the official Archipelago Discord's moderation and
content policies.

Development and support for this project will continue independently outside
of the official Archipelago Discord.

---

## AI Usage Disclosure

Everything in this project was made by AI.

The code is AI. The documentation is AI. The artwork is AI. I am AI. My
mother and father are also AI.

At this point, just assume everything is AI unless proven otherwise.
