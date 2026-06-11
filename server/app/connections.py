"""WebSocket connection tracking and game-state broadcasting."""

from fastapi import WebSocket

from .models import GameState


class ConnectionManager:
    """Tracks open sockets per game and fans out state snapshots.

    Every message is the full game state (same JSON as the REST API), so
    reconnects need no replay bookkeeping: a (re)connecting socket is
    simply sent the latest snapshot and is immediately up to date.
    """

    def __init__(self) -> None:
        self._connections: dict[str, list[WebSocket]] = {}

    async def connect(self, game_id: str, websocket: WebSocket) -> None:
        await websocket.accept()
        self._connections.setdefault(game_id, []).append(websocket)

    def disconnect(self, game_id: str, websocket: WebSocket) -> None:
        sockets = self._connections.get(game_id)
        if sockets is None:
            return
        if websocket in sockets:
            sockets.remove(websocket)
        if not sockets:
            del self._connections[game_id]

    def connection_count(self, game_id: str) -> int:
        return len(self._connections.get(game_id, []))

    async def send_state(self, websocket: WebSocket, game: GameState) -> None:
        # model_dump_json respects field exclusions (the dev-card deck
        # stays hidden), matching the REST responses exactly.
        await websocket.send_text(game.model_dump_json())

    async def broadcast(self, game: GameState) -> None:
        """Send the full game state to every socket watching the game."""
        dead: list[WebSocket] = []
        for websocket in list(self._connections.get(game.id, [])):
            try:
                await self.send_state(websocket, game)
            except Exception:
                dead.append(websocket)
        for websocket in dead:
            self.disconnect(game.id, websocket)


manager = ConnectionManager()
