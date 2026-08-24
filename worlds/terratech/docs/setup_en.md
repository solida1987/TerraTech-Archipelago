# TerraTech Archipelago — Setup Guide

## What this is

Every block in TerraTech is locked until the multiworld sends you the right to
use it. You can still find blocks, buy them and carry them in your inventory —
you just cannot bolt them on until your licence for that block arrives.

The game's own progression is left alone. Missions, licence grades and the
enemy ramp all work exactly as they always did. What changes is only *who
holds the key to the blocks*.

## What you need

- **TerraTech** on Steam or Epic (this was built against version 1.4.x)
- **Archipelago 0.5.0 or newer** — <https://github.com/ArchipelagoMW/Archipelago/releases>
- **`terratech.apworld`** and **`TerraTechArchipelago.zip`** from
  [our releases page](https://github.com/solida1987/TerraTech-Archipelago/releases)

You do **not** need a modded copy of the game, a patched executable, or any
files from us that belong to Payload Studios. Everything the mod needs from
TerraTech it finds in the game you already own.

## Installing

### 1. The apworld

Put `terratech.apworld` in Archipelago's `custom_worlds` folder:

```
C:\ProgramData\Archipelago\custom_worlds\
```

Or simply drag the file onto the Archipelago Launcher window and let it place
the file for you.

### 2. The game mod

Extract `TerraTechArchipelago.zip` into TerraTech's mod folder:

```
<your TerraTech folder>\QMods\TerraTechArchipelago\
```

If you already use **0ModManager** or **TTMM**, install it the way you install
any other mod — the mod answers to every loader convention in use.

To find your TerraTech folder: right-click the game in Steam →
**Manage** → **Browse local files**.

### 3. Check it loaded

Start TerraTech. In the log you should see:

```
[Archipelago] TerraTech Archipelago 0.1.0 starting.
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
| `shop_checks` | Archipelago items placed in vendor stock, priced by grade |
| `enemy_checks` | Archipelago blocks mounted on enemy techs — destroy them for the check |
| `crate_checks` | Archipelago crates that fall from the sky |
| `goal` | `licence_master`, `collector` or `ap_hunt` |

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
