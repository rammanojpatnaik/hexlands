"""Player actions, validated against the game rules.

Every action operates on the current player (requests carry no player id,
so acting out of turn is impossible by construction), raises RuleViolation
when a rule is broken, and returns the mutated GameState.
"""

import random

from ..models import (
    City,
    EdgeCoord,
    GamePhase,
    GameState,
    Player,
    Resource,
    Road,
    Settlement,
    Tile,
    VertexCoord,
)
from . import dice, hexgrid
from .players import check_winner, recompute_victory_points


class RuleViolation(Exception):
    """The requested action breaks a game rule."""


ROAD_COST = {Resource.WOOD: 1, Resource.BRICK: 1}
SETTLEMENT_COST = {
    Resource.WOOD: 1,
    Resource.BRICK: 1,
    Resource.SHEEP: 1,
    Resource.WHEAT: 1,
}
CITY_COST = {Resource.WHEAT: 2, Resource.ORE: 3}
DEV_CARD_COST = {Resource.SHEEP: 1, Resource.WHEAT: 1, Resource.ORE: 1}

MAX_SETTLEMENTS = 5
MAX_CITIES = 4
MAX_ROADS = 15

BANK_TRADE_RATE = 4  # TODO: 3:1 and 2:1 harbour rates


# --- helpers ---------------------------------------------------------------


def _require_phase(game: GameState, phase: GamePhase, action: str) -> None:
    if game.phase != phase:
        raise RuleViolation(f"Cannot {action} during the {game.phase.value!r} phase")


def _vertex_key(vertex: VertexCoord) -> hexgrid.Vertex:
    return hexgrid.normalize_vertex(vertex.q, vertex.r, vertex.corner)


def _edge_key(edge: EdgeCoord) -> hexgrid.Edge:
    return hexgrid.normalize_edge(edge.q, edge.r, edge.edge)


def _to_vertex(key: hexgrid.Vertex) -> VertexCoord:
    q, r, corner = key
    return VertexCoord(q=q, r=r, corner=corner)


def _to_edge(key: hexgrid.Edge) -> EdgeCoord:
    q, r, side = key
    return EdgeCoord(q=q, r=r, edge=side)


def _board(game: GameState) -> set[tuple[int, int]]:
    return {(tile.q, tile.r) for tile in game.tiles}


def _tiles_by_coord(game: GameState) -> dict[tuple[int, int], Tile]:
    return {(tile.q, tile.r): tile for tile in game.tiles}


def _buildings(game: GameState) -> dict[hexgrid.Vertex, tuple[str, int]]:
    """Occupied vertices -> ("settlement" | "city", owning player id)."""
    spots: dict[hexgrid.Vertex, tuple[str, int]] = {}
    for settlement in game.settlements:
        spots[_vertex_key(settlement.vertex)] = ("settlement", settlement.player)
    for city in game.cities:
        spots[_vertex_key(city.vertex)] = ("city", city.player)
    return spots


def _roads_by_edge(game: GameState) -> dict[hexgrid.Edge, int]:
    return {_edge_key(road.edge): road.player for road in game.roads}


def _pay(player: Player, cost: dict[Resource, int], what: str) -> None:
    missing = [
        f"{needed - player.resources.get(res, 0)} {res.value}"
        for res, needed in cost.items()
        if player.resources.get(res, 0) < needed
    ]
    if missing:
        raise RuleViolation(f"Not enough resources for a {what}: missing {', '.join(missing)}")
    for res, needed in cost.items():
        player.resources[res] -= needed


def _validate_settlement_site(game: GameState, key: hexgrid.Vertex) -> None:
    if not any(coord in _board(game) for coord in hexgrid.vertex_hexes(key)):
        raise RuleViolation("That vertex is not on the board")
    buildings = _buildings(game)
    if key in buildings:
        raise RuleViolation("That vertex is already occupied")
    for neighbour in hexgrid.vertex_neighbors(key):
        if neighbour in buildings:
            raise RuleViolation("Too close to another settlement or city (distance rule)")


def _validate_road_site(game: GameState, key: hexgrid.Edge) -> None:
    if not any(coord in _board(game) for coord in hexgrid.edge_hexes(key)):
        raise RuleViolation("That edge is not on the board")
    if key in _roads_by_edge(game):
        raise RuleViolation("That edge already has a road")


def _connects_to_network(game: GameState, key: hexgrid.Edge, player_id: int) -> bool:
    buildings = _buildings(game)
    roads = _roads_by_edge(game)
    for endpoint in hexgrid.edge_vertices(key):
        occupant = buildings.get(endpoint)
        if occupant is not None and occupant[1] == player_id:
            return True
        # A road may continue through an unoccupied vertex; an opponent's
        # building on the vertex blocks the connection.
        if occupant is None and any(
            roads.get(edge) == player_id
            for edge in hexgrid.vertex_edges(endpoint)
            if edge != key
        ):
            return True
    return False


# --- dice ------------------------------------------------------------------


def roll_dice(game: GameState) -> GameState:
    if game.phase != GamePhase.ROLL:
        raise RuleViolation(f"Cannot roll the dice during the {game.phase.value!r} phase")

    result = dice.roll()
    game.last_roll = result
    game.phase = GamePhase.ACTIONS
    roller = game.current

    if result.total == 7:
        game.log.append(
            f"{roller.name} rolled a 7! The robber stirs (robber movement and discards TODO)."
        )
        return game

    # Production: each settlement on a matching tile yields 1 resource,
    # each city 2. The robber blocks its tile. Bank supply is unlimited.
    buildings = _buildings(game)
    gained: dict[int, list[str]] = {}
    for tile in game.tiles:
        if tile.token != result.total or tile.resource is None:
            continue
        if (tile.q, tile.r) == (game.robber.q, game.robber.r):
            continue
        for vertex in hexgrid.hex_vertices(tile.q, tile.r):
            occupant = buildings.get(vertex)
            if occupant is None:
                continue
            kind, owner = occupant
            amount = 2 if kind == "city" else 1
            game.player_by_id(owner).add_resource(tile.resource, amount)
            gained.setdefault(owner, []).extend([tile.resource.value] * amount)

    if gained:
        production = "; ".join(
            f"{game.player_by_id(owner).name}: {', '.join(resources)}"
            for owner, resources in sorted(gained.items())
        )
        game.log.append(f"{roller.name} rolled {result.total}. Production — {production}.")
    else:
        game.log.append(f"{roller.name} rolled {result.total}. No production.")
    return game


# --- setup phase -------------------------------------------------------------


def _setup_settlement(game: GameState, vertex: VertexCoord) -> GameState:
    player = game.current
    if game.pending_setup_vertex is not None:
        raise RuleViolation("Place the road for your new settlement first")

    key = _vertex_key(vertex)
    _validate_settlement_site(game, key)
    game.settlements.append(Settlement(player=player.id, vertex=_to_vertex(key)))
    game.pending_setup_vertex = _to_vertex(key)

    # The second-round settlement collects one resource from each
    # adjacent tile (official setup rule).
    second_round = game.setup_index >= len(game.players)
    if second_round:
        tiles = _tiles_by_coord(game)
        for coord in hexgrid.vertex_hexes(key):
            tile = tiles.get(coord)
            if tile is not None and tile.resource is not None:
                player.add_resource(tile.resource)

    suffix = " and collected its starting resources" if second_round else ""
    game.log.append(f"{player.name} placed a settlement{suffix}.")
    recompute_victory_points(game)
    return game


def _setup_road(game: GameState, edge: EdgeCoord) -> GameState:
    player = game.current
    if game.pending_setup_vertex is None:
        raise RuleViolation("Place your settlement first")

    key = _edge_key(edge)
    _validate_road_site(game, key)
    anchor = _vertex_key(game.pending_setup_vertex)
    if anchor not in hexgrid.edge_vertices(key):
        raise RuleViolation("The setup road must touch the settlement you just placed")

    game.roads.append(Road(player=player.id, edge=_to_edge(key)))
    game.pending_setup_vertex = None
    game.setup_index += 1
    game.log.append(f"{player.name} placed a road.")

    if game.setup_index >= 2 * len(game.players):
        game.phase = GamePhase.ROLL
        game.current_turn = 0
        game.log.append(f"Setup complete. {game.current.name} starts — roll the dice!")
    return game


def auto_setup(game: GameState, rng: random.Random | None = None) -> GameState:
    """Play out the setup phase with random rule-valid placements."""
    rng = rng or random.Random()
    board_vertices = sorted(
        {vertex for tile in game.tiles for vertex in hexgrid.hex_vertices(tile.q, tile.r)}
    )
    while game.phase == GamePhase.SETUP:
        if game.pending_setup_vertex is None:
            candidates = board_vertices.copy()
        else:
            candidates = hexgrid.vertex_edges(_vertex_key(game.pending_setup_vertex))
        rng.shuffle(candidates)
        for key in candidates:
            try:
                if game.pending_setup_vertex is None:
                    place_settlement(game, _to_vertex(key))
                else:
                    place_road(game, _to_edge(key))
                break
            except RuleViolation:
                continue
        else:
            raise RuntimeError("auto-setup found no legal placement")
    return game


# --- build actions -----------------------------------------------------------


def place_settlement(game: GameState, vertex: VertexCoord) -> GameState:
    if game.phase == GamePhase.SETUP:
        return _setup_settlement(game, vertex)
    _require_phase(game, GamePhase.ACTIONS, "build a settlement")

    player = game.current
    if sum(1 for s in game.settlements if s.player == player.id) >= MAX_SETTLEMENTS:
        raise RuleViolation(f"All {MAX_SETTLEMENTS} of your settlements are on the board")

    key = _vertex_key(vertex)
    _validate_settlement_site(game, key)
    roads = _roads_by_edge(game)
    if not any(roads.get(edge) == player.id for edge in hexgrid.vertex_edges(key)):
        raise RuleViolation("A settlement must connect to one of your roads")

    _pay(player, SETTLEMENT_COST, "settlement")
    game.settlements.append(Settlement(player=player.id, vertex=_to_vertex(key)))
    game.log.append(f"{player.name} built a settlement.")
    recompute_victory_points(game)
    check_winner(game)
    return game


def place_road(game: GameState, edge: EdgeCoord) -> GameState:
    if game.phase == GamePhase.SETUP:
        return _setup_road(game, edge)
    _require_phase(game, GamePhase.ACTIONS, "build a road")

    player = game.current
    if sum(1 for road in game.roads if road.player == player.id) >= MAX_ROADS:
        raise RuleViolation(f"All {MAX_ROADS} of your roads are on the board")

    key = _edge_key(edge)
    _validate_road_site(game, key)
    if not _connects_to_network(game, key, player.id):
        raise RuleViolation("A road must connect to your existing roads or buildings")

    _pay(player, ROAD_COST, "road")
    game.roads.append(Road(player=player.id, edge=_to_edge(key)))
    game.log.append(f"{player.name} built a road.")
    return game


def place_city(game: GameState, vertex: VertexCoord) -> GameState:
    _require_phase(game, GamePhase.ACTIONS, "build a city")

    player = game.current
    if sum(1 for city in game.cities if city.player == player.id) >= MAX_CITIES:
        raise RuleViolation(f"All {MAX_CITIES} of your cities are on the board")

    key = _vertex_key(vertex)
    settlement = next(
        (
            s
            for s in game.settlements
            if s.player == player.id and _vertex_key(s.vertex) == key
        ),
        None,
    )
    if settlement is None:
        raise RuleViolation("A city must upgrade one of your own settlements")

    _pay(player, CITY_COST, "city")
    game.settlements.remove(settlement)
    game.cities.append(City(player=player.id, vertex=_to_vertex(key)))
    game.log.append(f"{player.name} upgraded a settlement to a city.")
    recompute_victory_points(game)
    check_winner(game)
    return game


# --- cards and trading --------------------------------------------------------


def buy_dev_card(game: GameState) -> GameState:
    _require_phase(game, GamePhase.ACTIONS, "buy a development card")

    player = game.current
    if not game.dev_card_deck:
        raise RuleViolation("The development-card deck is empty")

    _pay(player, DEV_CARD_COST, "development card")
    card = game.dev_card_deck.pop()
    player.dev_cards[card] = player.dev_cards.get(card, 0) + 1
    game.log.append(f"{player.name} bought a development card.")  # type stays hidden
    recompute_victory_points(game)
    check_winner(game)
    return game


def trade_bank(game: GameState, give: Resource, receive: Resource) -> GameState:
    _require_phase(game, GamePhase.ACTIONS, "trade with the bank")

    player = game.current
    if give == receive:
        raise RuleViolation("Pick two different resources to trade")
    held = player.resources.get(give, 0)
    if held < BANK_TRADE_RATE:
        raise RuleViolation(
            f"A bank trade costs {BANK_TRADE_RATE} {give.value} (you have {held})"
        )

    player.resources[give] -= BANK_TRADE_RATE
    player.add_resource(receive)
    game.log.append(f"{player.name} traded {BANK_TRADE_RATE} {give.value} for 1 {receive.value}.")
    return game


# --- turn flow -----------------------------------------------------------------


def end_turn(game: GameState) -> GameState:
    _require_phase(game, GamePhase.ACTIONS, "end the turn")

    recompute_victory_points(game)
    check_winner(game)
    if game.phase == GamePhase.FINISHED:
        return game

    previous = game.current.name
    game.current_turn = (game.current_turn + 1) % len(game.turn_order)
    if game.current_turn == 0:
        game.turn_number += 1
    game.last_roll = None
    game.phase = GamePhase.ROLL
    game.log.append(f"{previous} ended their turn. {game.current.name} is up.")
    return game
