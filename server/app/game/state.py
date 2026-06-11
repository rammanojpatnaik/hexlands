"""Game lifecycle and the in-memory game store."""

import random
import uuid

from ..models import DEV_CARD_DECK, GameState
from .actions import auto_setup
from .board import desert_coord, generate_board
from .players import create_players


def new_game(player_names: list[str], auto_place: bool = True) -> GameState:
    if not 2 <= len(player_names) <= 4:
        raise ValueError("Catan needs 2 to 4 players")

    tiles = generate_board()
    players = create_players(player_names)
    deck = DEV_CARD_DECK.copy()
    random.shuffle(deck)

    game = GameState(
        id=uuid.uuid4().hex[:8],
        tiles=tiles,
        players=players,
        turn_order=[player.id for player in players],
        robber=desert_coord(tiles),
        dev_card_deck=deck,
    )
    game.log.append(
        f"Game created with {len(players)} players. "
        f"Setup phase: {game.current.name} places first."
    )
    if auto_place:
        auto_setup(game)
    return game


class GameStore:
    """In-memory store keyed by game id; games vanish on server restart."""

    def __init__(self) -> None:
        self._games: dict[str, GameState] = {}

    def add(self, game: GameState) -> None:
        self._games[game.id] = game

    def get(self, game_id: str) -> GameState | None:
        return self._games.get(game_id)


store = GameStore()
