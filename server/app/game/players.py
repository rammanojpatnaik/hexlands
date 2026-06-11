"""Player creation and victory-point accounting."""

from ..models import DevCardType, GamePhase, GameState, Player, PlayerColor

WINNING_POINTS = 10


def create_players(names: list[str]) -> list[Player]:
    colors = list(PlayerColor)
    return [
        Player(id=index, name=name, color=colors[index])
        for index, name in enumerate(names)
    ]


def recompute_victory_points(game: GameState) -> None:
    """Settlements are worth 1 VP, cities 2, victory-point cards 1 each.

    TODO: longest road (2 VP) and largest army (2 VP) bonuses.
    """
    for player in game.players:
        settlements = sum(1 for s in game.settlements if s.player == player.id)
        cities = sum(1 for c in game.cities if c.player == player.id)
        vp_cards = player.dev_cards.get(DevCardType.VICTORY_POINT, 0)
        player.victory_points = settlements + 2 * cities + vp_cards


def check_winner(game: GameState) -> None:
    if game.winner is not None:
        return
    for player in game.players:
        if player.victory_points >= WINNING_POINTS:
            game.winner = player.id
            game.phase = GamePhase.FINISHED
            game.log.append(
                f"{player.name} wins with {player.victory_points} victory points!"
            )
            return
