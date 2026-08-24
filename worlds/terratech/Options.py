"""Player options for TerraTech Archipelago.

The defaults describe a seed a person can actually finish in an evening or
two. Everything that makes the pool enormous is opt-in, because a first seed
with five thousand locations is not a harder seed, it is an abandoned one.
"""
from dataclasses import dataclass

from Options import (Choice, DeathLink, DefaultOnToggle, PerGameCommonOptions,
                     Range, StartInventoryPool, Toggle)


class GoalType(Choice):
    """How the seed is won.

    licence_master  Max out the licence of a number of corporations.
    collector       Collect a percentage of every block type in the game.
    ap_hunt         Destroy the Archipelago cores mounted on named boss techs.
    """
    display_name = "Goal"
    option_licence_master = 0
    option_collector = 1
    option_ap_hunt = 2
    default = 0


class CorporationsToMax(Range):
    """How many corporations must reach max licence grade for the
    licence_master goal."""
    display_name = "Corporations to max"
    range_start = 1
    range_end = 8
    default = 3


class CollectorPercentage(Range):
    """Percentage of block types to collect for the collector goal."""
    display_name = "Collector percentage"
    range_start = 10
    range_end = 100
    default = 50


class APCoresRequired(Range):
    """How many Archipelago cores to destroy for the ap_hunt goal."""
    display_name = "Archipelago cores required"
    range_start = 3
    range_end = 25
    default = 8


class BlockPoolSize(Choice):
    """How much of the block catalogue is shuffled.

    starter    GSO and Space Junkers only -- a short seed to learn the ropes.
    standard   Every grade 1-3 block. The intended first playthrough.
    full       All 1144 blocks, including grade 4-5 and Experimental.
    """
    display_name = "Block pool"
    option_starter = 0
    option_standard = 1
    option_full = 2
    default = 1


class PickupChecks(DefaultOnToggle):
    """Send a check the first time each block type is picked up.

    This is the backbone of the location pool. Turning it off leaves only the
    shops, enemies, crates and quests, which makes for a much shorter seed.
    """
    display_name = "Pickup checks"


class AttachChecks(Toggle):
    """Send a second check the first time each block type is attached.

    Roughly doubles the location count. Off by default: it asks the player to
    visit every block twice.
    """
    display_name = "Attach checks"


class ShopChecks(Range):
    """How many Archipelago items are placed in vendor stock, spread across
    the licence grades. Prices climb steeply with grade."""
    display_name = "Shop checks"
    range_start = 0
    range_end = 500
    default = 100


class EnemyChecks(Range):
    """How many Archipelago blocks are mounted on enemy techs. Higher grades
    ride on harder techs."""
    display_name = "Enemy checks"
    range_start = 0
    range_end = 800
    default = 200


class CrateChecks(Range):
    """How many Archipelago crates fall from the sky as locations."""
    display_name = "Crate checks"
    range_start = 0
    range_end = 100
    default = 30


class QuestChecks(DefaultOnToggle):
    """Kill and gather milestones (5, 10, 25, 50 ... ) as locations."""
    display_name = "Quest checks"


class MissionChecks(DefaultOnToggle):
    """The game's own campaign missions as locations.

    Each mission fires once. Repeating a mission still pays the game's own
    reward but never sends the check again.
    """
    display_name = "Mission checks"


class TrapPercentage(Range):
    """Share of filler replaced by traps. Traps are deliberately mild --
    they cost time and money, never a save."""
    display_name = "Trap percentage"
    range_start = 0
    range_end = 40
    default = 0


@dataclass
class TerraTechOptions(PerGameCommonOptions):
    goal: GoalType
    corporations_to_max: CorporationsToMax
    collector_percentage: CollectorPercentage
    ap_cores_required: APCoresRequired
    block_pool: BlockPoolSize
    pickup_checks: PickupChecks
    attach_checks: AttachChecks
    shop_checks: ShopChecks
    enemy_checks: EnemyChecks
    crate_checks: CrateChecks
    quest_checks: QuestChecks
    mission_checks: MissionChecks
    trap_percentage: TrapPercentage
    death_link: DeathLink
    start_inventory_from_pool: StartInventoryPool
