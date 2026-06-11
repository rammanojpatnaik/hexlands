"""Game lifecycle (lobby, joining, starting) and the in-memory game store."""

import random
import uuid

from ..models import DEV_CARD_DECK, GamePhase, GameState
from .actions import RuleViolation, auto_setup
from .board import desert_coord, generate_board
from .players import new_player


def new_game(player_name: str, num_players: int = 3, auto_place: bool = True) -> GameState:
    """Open a lobby with the host seated; others join by game id."""
    tiles = generate_board()
    host = new_player(0, player_name)
    deck = DEV_CARD_DECK.copy()
    random.shuffle(deck)

    game = GameState(
        id=uuid.uuid4().hex[:8],
        phase=GamePhase.LOBBY,
        tiles=tiles,
        players=[host],
        num_players=num_players,
        auto_setup=auto_place,
        turn_order=[host.id],
        robber=desert_coord(tiles),
        dev_card_deck=deck,
    )
    game.log.append(
        f"{host.name} opened a lobby for {num_players} players. "
        f"Share the game id to invite the rest!"
    )
    return game


def join_game(game: GameState, player_name: str) -> GameState:
    """Seat a player in the lobby; the game starts when the last seat fills."""
    if game.phase != GamePhase.LOBBY:
        raise RuleViolation("This game has already started")
    if any(p.name.lower() == player_name.lower() for p in game.players):
        raise RuleViolation(f"The name {player_name!r} is already taken in this game")

    player = new_player(len(game.players), player_name)
    game.players.append(player)
    game.turn_order.append(player.id)
    game.log.append(f"{player.name} joined ({len(game.players)}/{game.num_players}).")

    if len(game.players) == game.num_players:
        _start(game)
    return game


def _start(game: GameState) -> None:
    game.log.append("All players are in — the game begins!")
    game.phase = GamePhase.SETUP
    if game.auto_setup:
        auto_setup(game)  # plays the setup phase and leaves the game at ROLL
    else:
        game.log.append(f"Setup phase: {game.current.name} places first.")


class GameStore:
    """In-memory store keyed by game id; games vanish on server restart."""

    def __init__(self) -> None:
        self._games: dict[str, GameState] = {}

    def add(self, game: GameState) -> None:
        self._games[game.id] = game

    def get(self, game_id: str) -> GameState | None:
        return self._games.get(game_id)


store = GameStore()
