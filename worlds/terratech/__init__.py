"""TerraTech Archipelago.

Every block in TerraTech is locked until the multiworld sends you the right
to use it. You can still find blocks, earn them and carry them -- you just
cannot bolt them on. The game's own progression is left alone: missions,
licence grades and the enemy ramp all run as they always did.
"""
from __future__ import annotations

from BaseClasses import Item, ItemClassification as IC, Region, Tutorial
from worlds.AutoWorld import WebWorld, World

from .Data import BLOCKS, CORPORATIONS, STARTER_CORPS, blocks_for_pool
from .Items import (ALL_ITEMS, BLOCK_ITEMS, FILLER_ITEMS, FILLER_WEIGHTS,
                    GRADE_ITEMS, TRAP_ITEMS, TerraTechItem, classify)
from .Locations import (ALL_LOCATIONS, TerraTechLocation, attach_locations,
                        crate_locations, enemy_locations, grade_locations,
                        mission_locations, pickup_locations, quest_locations,
                        shop_locations)
from .Options import TerraTechOptions
from .Rules import set_rules

# Registering the client here is what puts "TerraTech Client" in the
# Archipelago launcher. Import is deferred inside the function so a headless
# generator never pays for the client's dependencies.
from worlds.LauncherComponents import Component, Type, components, launch_subprocess


def _launch_client() -> None:
    from .Client import launch
    launch_subprocess(launch, name="TerraTechClient")


components.append(Component(
    "TerraTech Client", func=_launch_client, component_type=Type.CLIENT,
    description="Connects Archipelago to the TerraTech Archipelago mod",
))


class TerraTechWeb(WebWorld):
    theme = "dirt"
    tutorials = [Tutorial(
        "Multiworld Setup Guide",
        "How to set up TerraTech for an Archipelago multiworld.",
        "English", "setup_en.md", "setup/en",
        ["solida1987"],
    )]
    game_info_languages = ["en"]


class TerraTechWorld(World):
    """TerraTech as a multiworld: 1144 blocks, all locked until somebody
    sends you the right to use them."""

    game = "TerraTech"
    web = TerraTechWeb()
    options_dataclass = TerraTechOptions
    options: TerraTechOptions

    item_name_to_id = ALL_ITEMS
    location_name_to_id = ALL_LOCATIONS

    required_client_version = (0, 5, 0)

    def __init__(self, multiworld, player: int):
        super().__init__(multiworld, player)
        self.pool_blocks = []
        self.role_items: dict = {}
        self.used_locations: list[str] = []
        # The blocks on the starting vehicle are never locked. Which ones
        # those are is read from the live game by the mod on first spawn --
        # the world only needs to know that they exist, so mobility logic
        # does not demand an item the player already has.
        self.starter_blocks_free = True

    # --- generation -------------------------------------------------------

    def generate_early(self) -> None:
        from .Rules import build_role_items
        self.pool_blocks = blocks_for_pool(self.options.block_pool.current_key)
        self._fit_pool_to_locations()
        # Computed once. Walking every block on every rule evaluation is the
        # kind of cost that only shows up on a big pool.
        self.role_items = build_role_items(self.pool_blocks)

    def _fit_pool_to_locations(self) -> None:
        """Shrink the block pool until its licences can fit in the world.

        ⚠ Found by a stress test, not by reading the code. A player who turns
        pickup and attach checks off but leaves the pool on "starter" asks for
        230 block licences and offers 20 places to put them -- generation then
        fails with two hundred homeless items and a wall of red text that says
        nothing about which option caused it.

        Blocks are dropped from the highest grade down, so what survives is the
        early-game catalogue a short seed actually reaches.
        """
        budget = self._location_budget()
        # ⚠ Reserve the grade items EXPLICITLY, not as a percentage. A vague
        # "leave a fifth spare" left create_items trimming grade items to fit,
        # and it happily trimmed the Grade 5 the goal required -- an unwinnable
        # seed that generated without complaint until the fill gave up.
        grades_needed = len({b.corp for b in self.pool_blocks}) * 5
        room = max(1, budget - grades_needed - 2)   # 2 spare for filler
        if len(self.pool_blocks) <= room:
            return

        # ⚠ Trimming by grade alone once removed every weapon from a small
        # pool, and "Destroy 5 enemies" became unreachable. The logic depends
        # on three roles existing; the pool must guarantee them before it
        # optimises for anything else.
        from .Rules import ROLE_WORDS

        keep: list = []
        kept_ids: set = set()
        for words in ROLE_WORDS.values():
            found = 0
            for b in sorted(self.pool_blocks, key=lambda x: (x.tier, x.name)):
                if b.id in kept_ids:
                    continue
                if any(w in b.name for w in words):
                    keep.append(b)
                    kept_ids.add(b.id)
                    found += 1
                    if found >= 3:
                        break

        rest = [b for b in sorted(self.pool_blocks, key=lambda x: (x.tier, x.name))
                if b.id not in kept_ids]
        self.pool_blocks = sorted(keep + rest[:max(0, room - len(keep))],
                                  key=lambda b: (b.tier, b.name))

    def _location_budget(self) -> int:
        """How many locations this player's options will produce."""
        from .Data import QUEST_MILESTONES, CAMPAIGN_MISSIONS
        n = 0
        if self.options.pickup_checks:
            n += len(self.pool_blocks)
        if self.options.attach_checks:
            n += len(self.pool_blocks)
        n += len({b.corp for b in self.pool_blocks}) * 5      # grade locations
        n += self.options.shop_checks.value
        n += self.options.enemy_checks.value
        n += self.options.crate_checks.value
        if self.options.quest_checks:
            n += len(QUEST_MILESTONES) * 2
        if self.options.mission_checks:
            n += len(CAMPAIGN_MISSIONS)
        return n

    def create_regions(self) -> None:
        menu = Region("Menu", self.player, self.multiworld)
        self.multiworld.regions.append(menu)

        world = Region("Terra", self.player, self.multiworld)
        self.multiworld.regions.append(world)
        menu.connect(world)

        names: dict[str, int] = {}
        pool_ids = {b.id for b in self.pool_blocks}

        if self.options.pickup_checks:
            names.update({n: i for n, i in pickup_locations().items()
                          if self._block_of(n, "Pick up ").id in pool_ids})
        if self.options.attach_checks:
            names.update({n: i for n, i in attach_locations().items()
                          if self._block_of(n, "Attach ").id in pool_ids})

        # Only corporations the pool actually contains. Adding a grade
        # location for a corporation with no blocks creates a location whose
        # prerequisite item is never made -- generation reports it as
        # unreachable, and rightly so.
        pool_corps = {b.corp for b in self.pool_blocks}
        names.update({n: i for n, i in grade_locations().items()
                      if n.split(" reaches Grade ")[0] in pool_corps})

        if self.options.shop_checks.value:
            names.update(shop_locations(self.options.shop_checks.value))
        if self.options.enemy_checks.value:
            names.update(enemy_locations(self.options.enemy_checks.value))
        if self.options.crate_checks.value:
            names.update(crate_locations(self.options.crate_checks.value))
        if self.options.quest_checks:
            names.update(quest_locations())
        if self.options.mission_checks:
            names.update(mission_locations())

        for name, code in names.items():
            world.locations.append(TerraTechLocation(self.player, name, code, world))
        self.used_locations = list(names)

    def _block_of(self, location_name: str, prefix: str):
        from .Data import BLOCK_BY_NAME
        return BLOCK_BY_NAME[location_name[len(prefix):]]

    def create_item(self, name: str) -> Item:
        return TerraTechItem(name, classify(name), ALL_ITEMS[name], self.player)

    def create_items(self) -> None:
        pool: list[Item] = []

        # One licence per block in the pool. These are the progression.
        for block in self.pool_blocks:
            pool.append(self.create_item(f"{block.name} Licence"))

        # Corporation grades, for exactly the corporations create_regions
        # made locations for. The two must agree: an item without its
        # location is waste, a location without its item is unreachable.
        pool_corps = {b.corp for b in self.pool_blocks}
        for corp in CORPORATIONS:
            if corp not in pool_corps:
                continue
            for tier in (1, 2, 3, 4, 5):
                pool.append(self.create_item(f"{corp} Grade {tier}"))

        # The grade locations for corporations outside the pool still exist
        # as ids, but were never added to the region, so nothing to fill.
        needed = len(self.used_locations)
        if len(pool) > needed:
            # Should not happen -- generate_early sizes the pool to fit. If it
            # does, trim LICENCES, never grades: a missing grade item can make
            # the goal unreachable, while a missing licence only removes one
            # block from the shuffle.
            licences = [i for i in pool if i.name.endswith(" Licence")]
            while len(pool) > needed and licences:
                pool.remove(licences.pop())

        self._fill_with_filler(pool, needed)
        self.multiworld.itempool += pool

    def _fill_with_filler(self, pool: list[Item], needed: int) -> None:
        """Top the pool up so it exactly matches the location count."""
        trap_pct = self.options.trap_percentage.value
        names = list(FILLER_WEIGHTS)
        weights = [FILLER_WEIGHTS[n] for n in names]
        traps = list(TRAP_ITEMS)

        while len(pool) < needed:
            if traps and trap_pct and self.random.randrange(100) < trap_pct:
                pool.append(self.create_item(self.random.choice(traps)))
            else:
                pool.append(self.create_item(
                    self.random.choices(names, weights=weights, k=1)[0]))

    def get_filler_item_name(self) -> str:
        names = list(FILLER_WEIGHTS)
        weights = [FILLER_WEIGHTS[n] for n in names]
        return self.random.choices(names, weights=weights, k=1)[0]

    def set_rules(self) -> None:
        set_rules(self)

    # --- what the client needs to know ------------------------------------

    def fill_slot_data(self) -> dict:
        """Handed to the client on connect.

        The block table travels with the slot so the mod never has to guess
        which id a licence refers to, and so a game update that renames a
        block shows up as a mismatch the client can report rather than a
        silent no-op.
        """
        return {
            "goal": self.options.goal.current_key,
            "corporations_to_max": self.options.corporations_to_max.value,
            "collector_percentage": self.options.collector_percentage.value,
            "ap_cores_required": self.options.ap_cores_required.value,
            "block_pool": self.options.block_pool.current_key,
            "pickup_checks": bool(self.options.pickup_checks),
            "attach_checks": bool(self.options.attach_checks),
            "shop_checks": self.options.shop_checks.value,
            "enemy_checks": self.options.enemy_checks.value,
            "crate_checks": self.options.crate_checks.value,
            "quest_checks": bool(self.options.quest_checks),
            "mission_checks": bool(self.options.mission_checks),
            "death_link": bool(self.options.death_link),
            # name -> game block id, for every block this seed shuffles
            "blocks": {b.name: b.id for b in self.pool_blocks},
        }
