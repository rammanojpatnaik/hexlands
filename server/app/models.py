"""Pydantic models for the full Catan game state.

These models are both the in-memory game state and the API wire format
(snake_case JSON; the F# client decodes it with Thoth.Json's SnakeCase
strategy). Hidden information — the development-card deck — is excluded
from serialization.
"""

from enum import Enum

from pydantic import (
    BaseModel,
    ConfigDict,
    Field,
    computed_field,
    field_validator,
    model_validator,
)


class Resource(str, Enum):
    WOOD = "wood"
    BRICK = "brick"
    SHEEP = "sheep"
    WHEAT = "wheat"
    ORE = "ore"


class Terrain(str, Enum):
    FOREST = "forest"        # produces wood
    HILLS = "hills"          # produces brick
    PASTURE = "pasture"      # produces sheep
    FIELDS = "fields"        # produces wheat
    MOUNTAINS = "mountains"  # produces ore
    DESERT = "desert"        # produces nothing


TERRAIN_RESOURCE: dict[Terrain, "Resource | None"] = {
    Terrain.FOREST: Resource.WOOD,
    Terrain.HILLS: Resource.BRICK,
    Terrain.PASTURE: Resource.SHEEP,
    Terrain.FIELDS: Resource.WHEAT,
    Terrain.MOUNTAINS: Resource.ORE,
    Terrain.DESERT: None,
}


class DevCardType(str, Enum):
    KNIGHT = "knight"
    VICTORY_POINT = "victory_point"
    ROAD_BUILDING = "road_building"
    YEAR_OF_PLENTY = "year_of_plenty"
    MONOPOLY = "monopoly"


# Standard 25-card development deck.
DEV_CARD_DECK = (
    [DevCardType.KNIGHT] * 14
    + [DevCardType.VICTORY_POINT] * 5
    + [DevCardType.ROAD_BUILDING] * 2
    + [DevCardType.YEAR_OF_PLENTY] * 2
    + [DevCardType.MONOPOLY] * 2
)


class PlayerColor(str, Enum):
    RED = "red"
    BLUE = "blue"
    ORANGE = "orange"
    WHITE = "white"


class GamePhase(str, Enum):
    LOBBY = "lobby"      # waiting for players to join
    SETUP = "setup"      # players place their two starting settlements + roads
    ROLL = "roll"        # waiting for the current player to roll
    ACTIONS = "actions"  # build / trade / play cards, then end the turn
    FINISHED = "finished"


def empty_hand() -> dict[Resource, int]:
    return {resource: 0 for resource in Resource}


def empty_dev_hand() -> dict[DevCardType, int]:
    return {card: 0 for card in DevCardType}


class HexCoord(BaseModel):
    """Axial coordinate of a hex tile."""

    model_config = ConfigDict(frozen=True)

    q: int
    r: int


class VertexCoord(BaseModel):
    """A corner of hex (q, r), numbered 0-5 clockwise from the top.

    Settlements and cities sit on vertices. Each physical vertex is shared
    by up to three hexes; canonical normalization is a TODO in hexgrid.py.
    """

    model_config = ConfigDict(frozen=True)

    q: int
    r: int
    corner: int = Field(ge=0, le=5)


class EdgeCoord(BaseModel):
    """A side of hex (q, r), numbered 0-5 clockwise from the top-right.

    Roads sit on edges. Each physical edge is shared by two hexes;
    canonical normalization is a TODO in hexgrid.py.
    """

    model_config = ConfigDict(frozen=True)

    q: int
    r: int
    edge: int = Field(ge=0, le=5)


class Tile(BaseModel):
    q: int
    r: int
    terrain: Terrain
    token: int | None = None  # the dice number that makes this tile produce

    @field_validator("token")
    @classmethod
    def _valid_token(cls, value: int | None) -> int | None:
        if value is not None and (not 2 <= value <= 12 or value == 7):
            raise ValueError("number tokens are 2-6 and 8-12")
        return value

    @property
    def resource(self) -> Resource | None:
        return TERRAIN_RESOURCE[self.terrain]


class DiceRoll(BaseModel):
    die1: int = Field(ge=1, le=6)
    die2: int = Field(ge=1, le=6)

    @computed_field
    @property
    def total(self) -> int:
        return self.die1 + self.die2


class Settlement(BaseModel):
    player: int  # owning player id
    vertex: VertexCoord


class City(BaseModel):
    player: int
    vertex: VertexCoord


class Road(BaseModel):
    player: int
    edge: EdgeCoord


class Player(BaseModel):
    id: int
    name: str
    color: PlayerColor
    resources: dict[Resource, int] = Field(default_factory=empty_hand)
    dev_cards: dict[DevCardType, int] = Field(default_factory=empty_dev_hand)
    played_knights: int = 0
    victory_points: int = 0

    @field_validator("resources", "dev_cards")
    @classmethod
    def _non_negative(cls, counts: dict) -> dict:
        for key, count in counts.items():
            if count < 0:
                raise ValueError(f"negative count for {key}")
        return counts

    @property
    def total_resources(self) -> int:
        return sum(self.resources.values())

    def add_resource(self, resource: Resource, amount: int = 1) -> None:
        self.resources[resource] = self.resources.get(resource, 0) + amount


class GameState(BaseModel):
    id: str
    phase: GamePhase = GamePhase.LOBBY
    tiles: list[Tile]
    # The lobby opens with just the host; num_players seats in total.
    players: list[Player] = Field(min_length=1, max_length=4)
    num_players: int = Field(default=3, ge=2, le=4)
    auto_setup: bool = True  # play out the setup phase automatically when full
    turn_order: list[int]  # player ids in play order
    current_turn: int = 0  # index into turn_order
    turn_number: int = 1
    # Setup-phase bookkeeping: placements go in snake order (1, 2, 3, 3, 2, 1)
    # and each settlement must be followed by its road.
    setup_index: int = 0  # which of the 2n setup placements we're on
    pending_setup_vertex: VertexCoord | None = None  # settlement awaiting its road
    robber: HexCoord
    settlements: list[Settlement] = Field(default_factory=list)
    cities: list[City] = Field(default_factory=list)
    roads: list[Road] = Field(default_factory=list)
    # Hidden information: kept in memory, never serialized to API responses.
    dev_card_deck: list[DevCardType] = Field(default_factory=list, exclude=True)
    last_roll: DiceRoll | None = None
    winner: int | None = None  # winning player id
    log: list[str] = Field(default_factory=list)

    @model_validator(mode="after")
    def _consistent_turns(self) -> "GameState":
        if sorted(self.turn_order) != sorted(p.id for p in self.players):
            raise ValueError("turn_order must contain each player id exactly once")
        if not 0 <= self.current_turn < len(self.turn_order):
            raise ValueError("current_turn is out of range")
        return self

    @computed_field
    @property
    def current_player(self) -> int:
        """Id of the player whose turn it is."""
        if self.phase == GamePhase.SETUP:
            count = len(self.turn_order)
            slot = min(self.setup_index, 2 * count - 1)
            position = slot if slot < count else 2 * count - 1 - slot
            return self.turn_order[position]
        return self.turn_order[self.current_turn]

    def player_by_id(self, player_id: int) -> Player:
        for player in self.players:
            if player.id == player_id:
                return player
        raise KeyError(f"no player with id {player_id}")

    @property
    def current(self) -> Player:
        return self.player_by_id(self.current_player)


class CreateGameRequest(BaseModel):
    player_name: str = Field(default="Host", max_length=30)
    num_players: int = Field(default=3, ge=2, le=4)
    # When true, the server plays out the setup phase with random
    # rule-valid placements once everyone has joined.
    auto_setup: bool = True

    @field_validator("player_name")
    @classmethod
    def _not_blank(cls, value: str) -> str:
        value = value.strip()
        if not value:
            raise ValueError("player_name must not be blank")
        return value


class JoinGameRequest(BaseModel):
    player_name: str = Field(max_length=30)

    @field_validator("player_name")
    @classmethod
    def _not_blank(cls, value: str) -> str:
        value = value.strip()
        if not value:
            raise ValueError("player_name must not be blank")
        return value


class PlaceSettlementRequest(BaseModel):
    vertex: VertexCoord


class PlaceRoadRequest(BaseModel):
    edge: EdgeCoord


class PlaceCityRequest(BaseModel):
    vertex: VertexCoord


class TradeBankRequest(BaseModel):
    give: Resource
    receive: Resource
