"""Logic: what a player must already have before a location is reachable.

Two rules carry almost everything.

  Mobility  A tech that cannot move cannot reach anything. The starting
            vehicle satisfies this, so it only matters if someone turns the
            starter blocks off.
  Economy   Buying from a shop needs income, and income at grade 5 prices
            needs more than a drill and hope.

⚠ Deliberately NOT a rule: "you must hold the licence to buy the block". The
whole point of the design is that vanilla availability is opened up so an
early high-grade item can actually be obtained. Only the CARRIERS -- shops,
enemies, crates at a given grade -- sit behind their grade.
"""
from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import CollectionState

from .Data import CORPORATIONS, TIERS

if TYPE_CHECKING:
    from . import TerraTechWorld

# Categories a block name has to contain for us to count it as that role.
# Read off the game's own naming: every wheel is called a wheel.
_MOBILITY = ("Wheel", "Hover", "Track", "Anti Grav", "Booster", "Propeller")
_WEAPON = ("Cannon", "Laser", "Gun", "Mortar", "Missile", "Rocket", "Drill",
           "Flame", "Plasma", "Shotgun", "Sniper", "Rail")
_INCOME = ("Drill", "Refinery", "Silo", "Conveyor", "Collector", "Harvester",
           "Furnace", "Fabricator")

# The three roles the logic depends on, in one place so the pool trimmer and
# the rules can never disagree about what "a weapon" means.
ROLE_WORDS = {"mobility": _MOBILITY, "weapon": _WEAPON, "income": _INCOME}


def _has_any(state: CollectionState, player: int, world: "TerraTechWorld",
             which: str) -> bool:
    """Does the player hold a licence for any block of this role?

    ⚠ The role sets are computed ONCE in generate_early. The first version of
    this walked every block in the pool on every rule evaluation -- with 230
    blocks and a few hundred locations that is millions of string comparisons
    per fill attempt, and generation simply never finished. Precomputing turns
    it into one set lookup.
    """
    names = world.role_items.get(which)
    return bool(names) and state.has_any(names, player)


def has_mobility(state: CollectionState, player: int, world: "TerraTechWorld") -> bool:
    # The starting vehicle is always usable, so mobility is only ever in
    # question when the player has asked for it to be.
    if world.starter_blocks_free:
        return True
    return _has_any(state, player, world, "mobility")


def has_weapon(state: CollectionState, player: int, world: "TerraTechWorld") -> bool:
    return _has_any(state, player, world, "weapon")


def has_income(state: CollectionState, player: int, world: "TerraTechWorld") -> bool:
    # Selling scrap from destroyed enemies is income too, so a weapon counts.
    return (_has_any(state, player, world, "income")
            or _has_any(state, player, world, "weapon"))


def build_role_items(pool_blocks) -> dict:
    """The licence names that count as mobility, weapon and income."""
    out = {}
    for role, words in ROLE_WORDS.items():
        out[role] = frozenset(
            f"{b.name} Licence" for b in pool_blocks
            if any(w in b.name for w in words))
    return out


def has_grade(state: CollectionState, player: int, tier: int) -> bool:
    """Any corporation at this grade or higher.

    Any, not all: the carriers at grade 3 are spread across corporations, and
    demanding every corporation would make the early game a wall.

    ⚠ "or higher" is the rule, and this used to test the EXACT grade -- the
    docstring and the code disagreed, and the code was the one that was wrong.
    A seed only carries grade items for the corporations it tracks: the
    default one carries GSO Grade 3, 4 and 5 and no Grade 2 at all, so every
    grade-2 carrier was unreachable and generation said so out loud
    ("Missing: Shop G2 #1 ... Crate G2 #2"). Holding Grade 5 plainly means a
    player can shop at grade 2.
    """
    if tier <= 1:
        return True
    return any(state.has(f"{corp} Grade {t}", player)
               for corp in CORPORATIONS
               for t in range(tier, max(TIERS) + 1))


def set_rules(world: "TerraTechWorld") -> None:
    from worlds.generic.Rules import set_rule

    player = world.player
    mw = world.multiworld

    for name in world.used_locations:
        loc = mw.get_location(name, player)

        if name.startswith("Pick up "):
            # Picking a block up needs no permission at all -- that is the
            # design. Only getting to it needs a tech that moves.
            set_rule(loc, lambda st, w=world: has_mobility(st, w.player, w))

        elif name.startswith("Attach "):
            block_name = name[len("Attach "):]
            set_rule(loc, lambda st, w=world, b=block_name:
                     st.has(f"{b} Licence", w.player) and has_mobility(st, w.player, w))

        elif " reaches Grade " in name:
            tier = int(name.rpartition(" ")[2])
            # ⚠ This used to require the PREVIOUS grade as an item, which was
            # simply wrong about the game: licence grades are earned with XP
            # by playing, not received from another world. The mistake also
            # built a five-deep chain that a small seed could not fill --
            # found by the stress test, not by reading the rule.
            #
            # The grade ITEM still gates our own carriers. The grade LOCATION
            # is the player's own progress, and needs only the means to make
            # it: something that moves, and from grade 2 up, something that
            # fights.
            set_rule(loc, lambda st, w=world, t=tier:
                     has_mobility(st, w.player, w)
                     and (t <= 1 or has_weapon(st, w.player, w)))

        elif name.startswith("Shop G"):
            tier = int(name[6])
            set_rule(loc, lambda st, w=world, t=tier:
                     has_mobility(st, w.player, w)
                     and has_income(st, w.player, w)
                     and has_grade(st, w.player, t))

        elif name.startswith("Enemy G"):
            tier = int(name[7])
            set_rule(loc, lambda st, w=world, t=tier:
                     has_mobility(st, w.player, w)
                     and has_weapon(st, w.player, w)
                     and has_grade(st, w.player, t))

        elif name.startswith("Crate G"):
            tier = int(name[7])
            set_rule(loc, lambda st, w=world, t=tier:
                     has_mobility(st, w.player, w) and has_grade(st, w.player, t))

        elif name.startswith("Destroy "):
            set_rule(loc, lambda st, w=world:
                     has_mobility(st, w.player, w) and has_weapon(st, w.player, w))

        elif name.startswith("Collect "):
            set_rule(loc, lambda st, w=world: has_mobility(st, w.player, w))

    mw.completion_condition[player] = lambda st, w=world: world_complete(st, w)


def world_complete(state: CollectionState, world: "TerraTechWorld") -> bool:
    goal = world.options.goal.current_key
    player = world.player

    if goal == "licence_master":
        # What the player can REACH, not what they happen to hold.
        #
        # ⚠ This used to be state.has(f"{corp} Grade 5"), and those are
        # ordinary items in the pool. With corporations_to_max = 1 the goal
        # was satisfied by whichever block happened to hold a Grade 5 — in
        # testing, the very first pickup. The seed announced victory and the
        # server released everything before the player had left the spawn.
        from .Data import GRADE_LOCATION_RANGE
        tops = [f"{corp} reaches Grade {hi}"
                for corp, (_, hi) in GRADE_LOCATION_RANGE.items()]
        tops = [t for t in tops if t in world.used_locations]
        # Clamp to what exists: the option may ask for three corporations
        # while only one has a measured ladder yet. Demanding the others
        # would make every seed unwinnable until they are measured.
        needed = min(world.options.corporations_to_max.value, max(1, len(tops)))
        maxed = sum(1 for t in tops if state.can_reach_location(t, player))
        return maxed >= needed

    if goal == "collector":
        pct = world.options.collector_percentage.value
        need = max(1, len(world.pool_blocks) * pct // 100)
        have = sum(1 for b in world.pool_blocks
                   if state.has(f"{b.name} Licence", player))
        return have >= need

    # ⚠ Not a fallback. GoalType has exactly the two goals above, and the
    # third (ap_hunt) was removed because nothing in the world could ever
    # satisfy it. A silent "return something plausible" here is how a new
    # goal would quietly inherit another goal's completion rule.
    raise ValueError(f"TerraTech: no completion rule for goal {goal!r}")
