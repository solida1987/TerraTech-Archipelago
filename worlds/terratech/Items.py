"""Items: what the multiworld can send a TerraTech player.

Four kinds, and the split matters. Block rights are the progression — they are
the only thing that opens a lock. Everything else is filler that makes the run
smoother without gating it, so a seed stays solvable no matter where the
filler lands.
"""
from __future__ import annotations

from BaseClasses import Item, ItemClassification as IC

from .Data import BLOCKS, CORPORATIONS, TIERS

# Every id in this world is BASE_ID + offset. Keeping one base for items and
# locations alike means an id can never mean two things.
BASE_ID = 0x5454_0000  # "TT"


class TerraTechItem(Item):
    game = "TerraTech"


# --- Progression ---------------------------------------------------------
# One per block type: the right to attach it. The block itself is already in
# the world; this is permission, not delivery.
BLOCK_ITEMS: dict[str, int] = {
    f"{b.name} Licence": BASE_ID + i
    for i, b in enumerate(BLOCKS)
}

# Corporation grades. These are useful progression on their own: the grade
# gates the shop, enemy and crate carriers that belong to it.
GRADE_ITEMS: dict[str, int] = {
    f"{corp} Grade {tier}": BASE_ID + 0x8000 + (ci * 8) + tier
    for ci, corp in enumerate(CORPORATIONS)
    for tier in TIERS
}

# --- Filler --------------------------------------------------------------
FILLER_ITEMS: dict[str, int] = {
    "Block Bucks (small)":  BASE_ID + 0x9000,
    "Block Bucks (medium)": BASE_ID + 0x9001,
    "Block Bucks (large)":  BASE_ID + 0x9002,
    "Block Pack":           BASE_ID + 0x9003,
    "Supply Crate":         BASE_ID + 0x9004,
}

# --- Traps ---------------------------------------------------------------
# Deliberately mild. A trap that destroys a tech would cost a player an hour
# of building, which is a different kind of setback than a randomiser should
# hand out uninvited.
TRAP_ITEMS: dict[str, int] = {
    "Scrapper Trap":  BASE_ID + 0x9100,  # detaches a few random blocks
    "Bill Trap":      BASE_ID + 0x9101,  # takes a slice of the player's money
}

ALL_ITEMS: dict[str, int] = {
    **BLOCK_ITEMS, **GRADE_ITEMS, **FILLER_ITEMS, **TRAP_ITEMS,
}

FILLER_WEIGHTS = {
    "Block Bucks (small)": 30,
    "Block Bucks (medium)": 20,
    "Block Bucks (large)": 8,
    "Block Pack": 25,
    "Supply Crate": 17,
}


def classify(name: str) -> IC:
    if name in BLOCK_ITEMS or name in GRADE_ITEMS:
        return IC.progression
    if name in TRAP_ITEMS:
        return IC.trap
    return IC.filler


def item_id(name: str) -> int:
    return ALL_ITEMS[name]
