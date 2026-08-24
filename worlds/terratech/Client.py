"""The TerraTech client: Archipelago on one side, the game mod on the other.

The mod cannot speak the Archipelago protocol -- it lives inside Unity and has
no business holding a websocket to a room. So this client sits in the middle:
it talks Archipelago the normal way, and it talks to the mod over a plain
localhost TCP socket carrying one JSON object per line.

Newline-delimited JSON, not a length-prefixed binary frame, for one reason:
when this goes wrong at two in the morning, a person can point netcat at the
port and read what is being said.
"""
from __future__ import annotations

import asyncio
import json
import traceback
from typing import Any

import Utils
from CommonClient import (ClientCommandProcessor, CommonContext, get_base_parser,
                          gui_enabled, logger, server_loop)
from NetUtils import ClientStatus

# The mod listens here. Fixed rather than negotiated: the mod has no way to
# tell us a port it picked, and a port file is one more thing to go stale.
MOD_HOST = "127.0.0.1"
MOD_PORT = 24601

ITEMS_HANDLING = 0b111  # own items, others' items, starting inventory


class TerraTechCommandProcessor(ClientCommandProcessor):
    def _cmd_mod(self) -> None:
        """Report whether the game mod is connected."""
        connected = getattr(self.ctx, "mod_writer", None) is not None
        logger.info("Mod: %s (port %d)",
                    "connected" if connected else "not connected", MOD_PORT)

    def _cmd_resync(self) -> None:
        """Re-send every received item to the game.

        The safe repair when a save is restored from a backup: the mod keys
        on item index, so replaying is idempotent -- it can only ever put the
        game back in step, never double-grant.
        """
        self.ctx.resync_requested = True
        logger.info("Resync queued -- every received item will be re-sent.")


class TerraTechContext(CommonContext):
    game = "TerraTech"
    command_processor = TerraTechCommandProcessor
    items_handling = ITEMS_HANDLING

    def __init__(self, server_address: str | None, password: str | None):
        super().__init__(server_address, password)
        self.mod_writer: asyncio.StreamWriter | None = None
        self.slot_data: dict[str, Any] = {}
        self.resync_requested = False
        # Locations the mod has told us about but the server has not yet
        # acknowledged. Kept so a disconnect mid-check cannot lose one.
        self.pending_locations: set[int] = set()
        self.sent_locations: set[int] = set()

    async def server_auth(self, password_requested: bool = False) -> None:
        if password_requested and not self.password:
            await super().server_auth(password_requested)
        await self.get_username()
        await self.send_connect()

    def on_package(self, cmd: str, args: dict) -> None:
        if cmd == "Connected":
            self.slot_data = args.get("slot_data", {})
            asyncio.create_task(self._send_handshake())
        elif cmd == "ReceivedItems":
            asyncio.create_task(self._send_items())
        elif cmd == "RoomUpdate" and "checked_locations" in args:
            self.sent_locations.update(args["checked_locations"])
            self.pending_locations -= self.sent_locations

    # --- talking to the mod ----------------------------------------------

    async def _to_mod(self, payload: dict) -> None:
        if self.mod_writer is None:
            return
        try:
            self.mod_writer.write((json.dumps(payload) + "\n").encode("utf-8"))
            await self.mod_writer.drain()
        except Exception:
            logger.warning("Lost the connection to the game mod.")
            self.mod_writer = None

    async def _send_handshake(self) -> None:
        """Everything the mod needs to configure itself for this seed."""
        await self._to_mod({
            "cmd": "Handshake",
            "slot": self.slot,
            "seed": self.seed_name,
            "slot_data": self.slot_data,
        })

    async def _send_items(self) -> None:
        """Send received items, each tagged with its index.

        The index is the dedup key and it has exactly one form -- an integer
        from the server. Diablo II's redelivery bug came from a key that
        flip-flopped between a number and a slot name; there is nothing here
        for it to flip to.
        """
        for index, item in enumerate(self.items_received):
            await self._to_mod({
                "cmd": "Item",
                "index": index,
                "id": item.item,
                "name": self.item_names.lookup_in_game(item.item),
                "from": self.player_names.get(item.player, "someone"),
            })

    async def send_death(self, death_text: str = "") -> None:
        await super().send_death(death_text)

    def on_deathlink(self, data: dict) -> None:
        super().on_deathlink(data)
        asyncio.create_task(self._to_mod({
            "cmd": "DeathLink",
            "source": data.get("source", "someone"),
            "cause": data.get("cause", ""),
        }))

    # --- hearing from the mod --------------------------------------------

    async def handle_mod_line(self, line: str) -> None:
        try:
            msg = json.loads(line)
        except json.JSONDecodeError:
            logger.debug("Unreadable line from mod: %r", line[:120])
            return

        cmd = msg.get("cmd")

        if cmd == "Hello":
            logger.info("Game mod connected (mod version %s, game %s).",
                        msg.get("mod_version", "?"), msg.get("game_version", "?"))
            if self.slot_data:
                await self._send_handshake()
                await self._send_items()

        elif cmd == "Check":
            # The mod names locations; we translate to ids. An unknown name is
            # reported, never dropped in silence -- that is how a renamed
            # location becomes a bug report instead of a seed that cannot be
            # finished.
            names = msg.get("locations", [])
            ids: list[int] = []
            for name in names:
                loc_id = self.location_names_to_id.get(name)
                if loc_id is None:
                    logger.warning("Mod sent an unknown location: %r", name)
                    continue
                ids.append(loc_id)
            new = [i for i in ids if i not in self.sent_locations]
            if new:
                self.pending_locations.update(new)
                await self.send_msgs([{"cmd": "LocationChecks", "locations": new}])

        elif cmd == "Goal":
            await self.send_msgs([{
                "cmd": "StatusUpdate", "status": ClientStatus.CLIENT_GOAL}])
            logger.info("Goal complete.")

        elif cmd == "Death":
            if "DeathLink" in self.tags:
                await self.send_death(msg.get("cause", "A tech was destroyed."))

        elif cmd == "Log":
            logger.info("[game] %s", msg.get("text", ""))

    @property
    def location_names_to_id(self) -> dict[str, int]:
        from .Locations import ALL_LOCATIONS
        return ALL_LOCATIONS

    def run_gui(self) -> None:
        from kvui import GameManager

        class TerraTechManager(GameManager):
            logging_pairs = [("Client", "Archipelago")]
            base_title = "Archipelago TerraTech Client"

        self.ui = TerraTechManager(self)
        self.ui_task = asyncio.create_task(self.ui.async_run(), name="UI")


async def mod_server(ctx: TerraTechContext) -> None:
    """Listen for the mod. The mod dials us, not the other way round.

    The game starts and stops far more often than the client does, and a
    listener that outlives the game is much less fragile than one that has to
    guess when to reconnect.
    """
    async def on_client(reader: asyncio.StreamReader,
                        writer: asyncio.StreamWriter) -> None:
        ctx.mod_writer = writer
        try:
            while not ctx.exit_event.is_set():
                line = await reader.readline()
                if not line:
                    break
                await ctx.handle_mod_line(line.decode("utf-8", "replace").strip())
        except Exception:
            logger.debug("Mod connection error:\n%s", traceback.format_exc())
        finally:
            logger.info("Game mod disconnected.")
            if ctx.mod_writer is writer:
                ctx.mod_writer = None
            writer.close()

    server = await asyncio.start_server(on_client, MOD_HOST, MOD_PORT)
    logger.info("Waiting for the TerraTech mod on %s:%d ...", MOD_HOST, MOD_PORT)
    async with server:
        await server.serve_forever()


async def resync_watcher(ctx: TerraTechContext) -> None:
    while not ctx.exit_event.is_set():
        if ctx.resync_requested:
            ctx.resync_requested = False
            await ctx._send_items()
        await asyncio.sleep(0.5)


async def main() -> None:
    parser = get_base_parser(description="TerraTech Archipelago client")
    args = parser.parse_args()

    ctx = TerraTechContext(args.connect, args.password)
    ctx.server_task = asyncio.create_task(server_loop(ctx), name="ServerLoop")
    if gui_enabled:
        ctx.run_gui()
    ctx.run_cli()

    listener = asyncio.create_task(mod_server(ctx), name="ModServer")
    watcher = asyncio.create_task(resync_watcher(ctx), name="Resync")

    await ctx.exit_event.wait()
    listener.cancel()
    watcher.cancel()
    await ctx.shutdown()


def launch() -> None:
    Utils.init_logging("TerraTechClient", exception_logger="Client")
    import colorama
    colorama.just_fix_windows_console()
    asyncio.run(main())
    colorama.deinit()
