"""Axial hex-coordinate math and board topology for the Catan board.

Tiles are addressed with axial coordinates (q, r) on a pointy-top hex grid;
the standard board is a hexagon of radius 2 around the origin (19 tiles).

Corners of a hex are numbered 0-5 clockwise starting at the upper-right
(matching the client's rendering, where corner i sits at angle 60*i - 30
degrees with y pointing down), so corner 2 is the bottom (S) and corner 5
the top (N) of the hex. Edge i of a hex is the side joining corners i
and i+1.

Every physical vertex is shared by up to three hexes, and is canonically
the N (corner 5) or S (corner 2) vertex of exactly one hex. Every physical
edge is shared by up to two hexes, and is canonically the E (0), SE (1),
or SW (2) side of exactly one hex. All topology functions below take and
return canonical (q, r, corner|edge) tuples; use normalize_vertex /
normalize_edge to canonicalize user input first.
"""

STANDARD_BOARD_RADIUS = 2

# Pointy-top axial direction vectors, counterclockwise from east.
DIRECTIONS = [(1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1)]

Vertex = tuple[int, int, int]  # (q, r, corner) with corner in {2, 5}
Edge = tuple[int, int, int]    # (q, r, edge) with edge in {0, 1, 2}


def standard_board_coords() -> list[tuple[int, int]]:
    """All axial (q, r) coordinates of the standard 19-tile board."""
    radius = STANDARD_BOARD_RADIUS
    coords = []
    for r in range(-radius, radius + 1):
        q_min = max(-radius, -radius - r)
        q_max = min(radius, radius - r)
        for q in range(q_min, q_max + 1):
            coords.append((q, r))
    return coords


def neighbors(q: int, r: int) -> list[tuple[int, int]]:
    """The six axial coordinates adjacent to (q, r)."""
    return [(q + dq, r + dr) for dq, dr in DIRECTIONS]


def distance(a: tuple[int, int], b: tuple[int, int]) -> int:
    """Hex distance between two axial coordinates."""
    aq, ar = a
    bq, br = b
    return (abs(aq - bq) + abs(ar - br) + abs((aq + ar) - (bq + br))) // 2


# corner -> (dq, dr, canonical corner)
_CANONICAL_VERTEX = {
    0: (1, -1, 2),  # NE corner = S corner of the NE neighbour
    1: (0, 1, 5),   # SE corner = N corner of the SE neighbour
    2: (0, 0, 2),   # S corner (already canonical)
    3: (-1, 1, 5),  # SW corner = N corner of the SW neighbour
    4: (0, -1, 2),  # NW corner = S corner of the NW neighbour
    5: (0, 0, 5),   # N corner (already canonical)
}

# edge -> (dq, dr, canonical edge)
_CANONICAL_EDGE = {
    0: (0, 0, 0),   # E side (already canonical)
    1: (0, 0, 1),   # SE side (already canonical)
    2: (0, 0, 2),   # SW side (already canonical)
    3: (-1, 0, 0),  # W side = E side of the W neighbour
    4: (0, -1, 1),  # NW side = SE side of the NW neighbour
    5: (1, -1, 2),  # NE side = SW side of the NE neighbour
}


def normalize_vertex(q: int, r: int, corner: int) -> Vertex:
    dq, dr, canonical = _CANONICAL_VERTEX[corner]
    return (q + dq, r + dr, canonical)


def normalize_edge(q: int, r: int, edge: int) -> Edge:
    dq, dr, canonical = _CANONICAL_EDGE[edge]
    return (q + dq, r + dr, canonical)


def vertex_hexes(vertex: Vertex) -> list[tuple[int, int]]:
    """The three hex coordinates sharing a vertex (some may be off-board)."""
    q, r, corner = vertex
    if corner == 5:  # N
        return [(q, r), (q + 1, r - 1), (q, r - 1)]
    return [(q, r), (q, r + 1), (q - 1, r + 1)]  # S


def vertex_neighbors(vertex: Vertex) -> list[Vertex]:
    """The three vertices one edge away (used for the distance rule)."""
    q, r, corner = vertex
    if corner == 5:  # N
        return [(q, r - 1, 2), (q + 1, r - 1, 2), (q + 1, r - 2, 2)]
    return [(q, r + 1, 5), (q - 1, r + 1, 5), (q - 1, r + 2, 5)]  # S


def edge_vertices(edge: Edge) -> tuple[Vertex, Vertex]:
    """The two endpoint vertices of an edge."""
    q, r, side = edge
    if side == 0:  # E
        return (q + 1, r - 1, 2), (q, r + 1, 5)
    if side == 1:  # SE
        return (q, r + 1, 5), (q, r, 2)
    return (q, r, 2), (q - 1, r + 1, 5)  # SW


def edge_hexes(edge: Edge) -> list[tuple[int, int]]:
    """The two hex coordinates sharing an edge (one may be off-board)."""
    q, r, side = edge
    if side == 0:  # E
        return [(q, r), (q + 1, r)]
    if side == 1:  # SE
        return [(q, r), (q, r + 1)]
    return [(q, r), (q - 1, r + 1)]  # SW


def hex_vertices(q: int, r: int) -> list[Vertex]:
    """The six canonical vertices of hex (q, r)."""
    return [normalize_vertex(q, r, corner) for corner in range(6)]


def hex_edges(q: int, r: int) -> list[Edge]:
    """The six canonical edges of hex (q, r)."""
    return [normalize_edge(q, r, edge) for edge in range(6)]


def vertex_edges(vertex: Vertex) -> list[Edge]:
    """The three edges incident to a vertex."""
    incident = set()
    for hq, hr in vertex_hexes(vertex):
        for edge in hex_edges(hq, hr):
            if vertex in edge_vertices(edge):
                incident.add(edge)
    return sorted(incident)
