"""Board generation: terrains, number tokens, and the robber."""

import random

from ..models import HexCoord, Terrain, Tile
from .hexgrid import standard_board_coords

# Standard Catan terrain distribution (19 tiles).
STANDARD_TERRAINS = (
    [Terrain.FOREST] * 4
    + [Terrain.PASTURE] * 4
    + [Terrain.FIELDS] * 4
    + [Terrain.HILLS] * 3
    + [Terrain.MOUNTAINS] * 3
    + [Terrain.DESERT]
)

# Standard number tokens (no 7; the desert gets none).
STANDARD_TOKENS = [2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12]


def generate_board(rng: random.Random | None = None) -> list[Tile]:
    """Generate a randomized standard board.

    Terrains are shuffled across the 19 hexes and number tokens are shuffled
    across the non-desert tiles. The robber starts on the desert.

    TODO: implement the official spiral token placement, which guarantees
    that 6s and 8s are never adjacent.
    """
    rng = rng or random.Random()

    terrains = STANDARD_TERRAINS.copy()
    rng.shuffle(terrains)
    tokens = STANDARD_TOKENS.copy()
    rng.shuffle(tokens)

    tiles = []
    token_iter = iter(tokens)
    for (q, r), terrain in zip(standard_board_coords(), terrains):
        token = None if terrain == Terrain.DESERT else next(token_iter)
        tiles.append(Tile(q=q, r=r, terrain=terrain, token=token))
    return tiles


def desert_coord(tiles: list[Tile]) -> HexCoord:
    """Coordinate of the desert tile (initial robber position)."""
    for tile in tiles:
        if tile.terrain == Terrain.DESERT:
            return HexCoord(q=tile.q, r=tile.r)
    raise ValueError("board has no desert tile")
