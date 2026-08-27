# TerraTech Archipelago — Setup Guide

## What this is

Every block in TerraTech is locked until the multiworld sends you the right to
use it. You can still find blocks, buy them and carry them in your inventory —
you just cannot bolt them on until your licence for that block arrives.

The game's own progression is left alone. Missions, licence grades and the
enemy ramp all work exactly as they always did. What changes is only *who
holds the key to the blocks*.

## What you need

- **TerraTech** on Steam or Epic (built and tested against the current Steam build)
- **Archipelago 0.5.0 or newer** — <https://github.com/ArchipelagoMW/Archipelago/releases>
- **`terratech.apworld`** and **`TerraTechArchipelago.zip`** from
  [our releases page](https://github.com/solida1987/TerraTech-Archipelago/releases)

You do **not** need a modded copy of the game, a patched executable, or any
files from us that belong to Payload Studios. Everything the mod needs from
TerraTech it finds in the game you already own.

## The short way: Multiworld Launcher

If you use [Multiworld Launcher](https://github.com/solida1987/Multiworld-Launcher),
all of this is one button: install the TerraTech plugin from the Plugin
Library, point it at your TerraTech folder if it does not find it by itself,
and press Play. The launcher installs the world and the mod, connects to the
multiworld for you, and talks to the game directly — no separate client
window, nothing to type.

The rest of this guide is the manual road, for playing without the launcher.

## Installing

### 1. The apworld

Put `terratech.apworld` in Archipelago's `custom_worlds` folder:

```
C:\ProgramData\Archipelago\custom_worlds\
```

Or simply drag the file onto the Archipelago Launcher window and let it place
the file for you.

### 2. BepInEx, the mod loader

TerraTech loads no outside code by itself, so the mod needs a loader. Get
**BepInEx 5.x, 64-bit** from <https://github.com/BepInEx/BepInEx/releases> and
extract it into your TerraTech folder — the one holding `TerraTechWin64.exe`.
You should end up with `winhttp.dll` and a `BepInEx` folder beside the game.

Start the game once so BepInEx creates its folders, then close it.

If you already run BepInEx for other TerraTech mods, skip this step.

### 3. The game mod

Extract `TerraTechArchipelago.zip` into your TerraTech folder. The zip is laid
out so the files land where the loader looks:

```
<your TerraTech folder>\BepInEx\plugins\TerraTechArchipelago\
```

To find your TerraTech folder: right-click the game in Steam →
**Manage** → **Browse local files**.

### 4. Check it loaded

Start TerraTech and open `BepInEx\LogOutput.log` in the game folder.
You should see:

```
[Archipelago] TerraTech Archipelago 0.1.2 starting, in TerraTech <version>.
[Archipelago] Patches applied. Waiting for the Archipelago client on port 24601.
```

If instead you see **REFUSING TO PATCH**, the mod is telling you that this
build of TerraTech has moved something it needs. It will list exactly what is
missing and change nothing — your game still runs normally. Send us that list.

## Playing

1. Generate or join a multiworld with a TerraTech slot.
2. Open the **Archipelago Launcher** and start **TerraTech Client**.
3. Connect it to the room with your slot name.
4. Start TerraTech and begin a **new single-player campaign**.

The mod dials the client on `127.0.0.1:24601` by itself. Start them in either
order — whichever comes second finds the other.

### What you will see

- The blocks on your starting vehicle work normally.
- Every other block glows **red** and refuses to attach. Red is the game's own
  "this block is not right" colour, so it should read as familiar rather than
  broken.
- When a licence arrives, that block type flashes **green** and works from
  then on — everywhere in the world, including ones you already carry.
- Vendors sell across every corporation from the start, including grades you
  have not earned. That is deliberate: an item you cannot obtain is not an
  item. Prices still climb steeply with grade, so an early high-grade block is
  an investment rather than a gift.

### Saving

Save and load as you always would. The mod keeps its own bookkeeping in a file
beside your save, never inside it — a damaged mod file costs you nothing but
some re-sent checks, and your save is never touched.

Playing offline works. Checks are queued and sent the next time the client
connects.

## Options worth knowing

| Option | What it does |
|---|---|
| `block_pool` | `starter` (GSO + Space Junkers), `standard` (grades 1–3), `full` (all 1144) |
| `pickup_checks` | A check the first time you pick up each block type. On by default — this is most of the seed |
| `attach_checks` | A second check the first time you attach each type. Roughly doubles the length |
| `shop_checks` | Checks for buying a block type you have never bought before |
| `enemy_checks` | Checks for destroying hostile techs. Harder techs draw from higher grades |
| `crate_checks` | Checks for opening crates |
| `quest_checks` | Kill and gather milestones (5, 10, 25, 50 …) |
| `trap_percentage` | Share of filler replaced by traps. Mild by design — they cost time and money, never a save |
| `death_link` | Share deaths. In TerraTech a death is your tech coming apart where it stands; the blocks scatter and you rebuild |
| `goal` | `licence_master` or `collector` |

A first seed is comfortable at `block_pool: standard` with pickup checks on and
attach checks off. `full` with both on is roughly 2,500 locations.

## When something is wrong

**The client says it is waiting for the mod.** The mod only dials once you are
in a game — get to the main menu and start or load a campaign.

**A block stays red after its licence arrived.** Type `/resync` in the client.
Every received item is re-sent; the mod applies each one once, so this can only
put things back in step.

**A licence arrived for a block that does not exist.** The log names it. This
means the seed was generated against a different version of TerraTech than the
one you are running — tell us both versions.

**Nothing at all happens.** Check the client window for `Game mod connected`.
Without that line the two halves have not found each other, and nothing else
will work.

## Reporting problems

Please include the mod's start-up lines from the log and the client window's
first ten lines. Between them they say which versions met each other, which is
the first thing anybody needs to know.
