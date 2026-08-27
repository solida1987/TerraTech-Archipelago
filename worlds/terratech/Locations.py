"""Locations: everything in TerraTech that can hold somebody else's item.

Six families, and every one of them is exhaustible. When a family's pool for
a grade is empty the mod stops placing its carriers, so a player never meets
an Archipelago crate that has nothing left to give. That rule lives in the
mod; this module only decides which locations exist and what they are called.
"""
from __future__ import annotations

from BaseClasses import Location

from .Data import (BLOCKS, CORPORATIONS, QUEST_MILESTONES,
                   TIERS, split_by_tier)
from .Items import BASE_ID


class TerraTechLocation(Location):
    game = "TerraTech"


# Offsets are grouped so a location id says which family it belongs to at a
# glance -- useful when a log line is all you have to debug from.
_PICKUP = 0x10000
_ATTACH = 0x20000
_GRADE = 0x30000
_SHOP = 0x40000
_ENEMY = 0x50000
_CRATE = 0x60000
_QUEST = 0x70000
# ⚠ 0x80000 was the campaign missions. The offset is retired rather than
# reused: an id that once meant "Complete Mission 007" must never come back
# meaning something else in somebody's half-finished seed.


def pickup_locations() -> dict[str, int]:
    return {f"Pick up {b.name}": BASE_ID + _PICKUP + i
            for i, b in enumerate(BLOCKS)}


def attach_locations() -> dict[str, int]:
    return {f"Attach {b.name}": BASE_ID + _ATTACH + i
            for i, b in enumerate(BLOCKS)}


def grade_locations() -> dict[str, int]:
    # Ids stay keyed on the corporation's index in CORPORATIONS, so a
    # corporation added to the measured table later keeps stable ids.
    from .Data import GRADE_LOCATION_RANGE
    return {f"{corp} reaches Grade {tier}": BASE_ID + _GRADE + (ci * 8) + tier
            for ci, corp in enumerate(CORPORATIONS)
            if corp in GRADE_LOCATION_RANGE
            for tier in range(GRADE_LOCATION_RANGE[corp][0],
                              GRADE_LOCATION_RANGE[corp][1] + 1)}


def _numbered(prefix: str, offset: int, counts: dict[int, int]) -> dict[str, int]:
    """Carrier locations, numbered inside each grade.

    The name carries the grade because the player sees it in the spoiler and
    in the client log, and "which grade was that?" is the first question a
    person asks when a location will not come.
    """
    out: dict[str, int] = {}
    n = 0
    for tier in TIERS:
        for k in range(counts.get(tier, 0)):
            out[f"{prefix} G{tier} #{k + 1}"] = BASE_ID + offset + n
            n += 1
    return out


def shop_locations(total: int) -> dict[str, int]:
    return _numbered("Shop", _SHOP, split_by_tier(total))


def enemy_locations(total: int) -> dict[str, int]:
    return _numbered("Enemy", _ENEMY, split_by_tier(total))


def crate_locations(total: int) -> dict[str, int]:
    return _numbered("Crate", _CRATE, split_by_tier(total))


def quest_locations() -> dict[str, int]:
    out: dict[str, int] = {}
    for i, milestone in enumerate(QUEST_MILESTONES):
        out[f"Destroy {milestone} enemies"] = BASE_ID + _QUEST + i
        out[f"Collect {milestone} blocks"] = BASE_ID + _QUEST + 0x100 + i
    return out


def all_locations() -> dict[str, int]:
    """Every location this world can ever define.

    Archipelago needs a stable, complete name-to-id map that does not depend
    on one player's options -- two players in the same multiworld may ask for
    different counts, and the ids have to agree. So the maximums are used
    here, and the per-player selection happens in __init__.
    """
    out: dict[str, int] = {}
    out.update(pickup_locations())
    out.update(attach_locations())
    out.update(grade_locations())
    out.update(shop_locations(500))    # option maximum
    out.update(enemy_locations(800))   # option maximum
    out.update(crate_locations(100))   # option maximum
    out.update(quest_locations())
    return out


ALL_LOCATIONS = all_locations()
