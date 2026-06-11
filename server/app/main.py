"""FastAPI application exposing the Hexlands game API.

REST endpoints mutate the game; every successful action also broadcasts
the full updated game state to all WebSocket subscribers of that game.
Rule violations surface as 409 Conflict with a human-readable detail;
malformed input (bad coordinates, player counts) as 422.
"""

from fastapi import (
    APIRouter,
    FastAPI,
    HTTPException,
    Request,
    WebSocket,
    WebSocketDisconnect,
)
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from .connections import manager
from .game import actions, state
from .models import (
    CreateGameRequest,
    GameState,
    PlaceCityRequest,
    PlaceRoadRequest,
    PlaceSettlementRequest,
    TradeBankRequest,
)

app = FastAPI(title="Hexlands API", version="0.3.0")

# The Vite dev server proxies /game and /ws to this server, so CORS is
# only a fallback for clients that talk to port 8000 directly.
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5173", "http://127.0.0.1:5173"],
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.exception_handler(actions.RuleViolation)
def rule_violation_handler(_request: Request, exc: actions.RuleViolation) -> JSONResponse:
    return JSONResponse(status_code=409, content={"detail": str(exc)})


def get_game_or_404(game_id: str) -> GameState:
    game = state.store.get(game_id)
    if game is None:
        raise HTTPException(status_code=404, detail=f"No game with id {game_id!r}")
    return game


async def _broadcast(game: GameState) -> GameState:
    """Fan the updated state out to WebSocket subscribers, then return it."""
    await manager.broadcast(game)
    return game


router = APIRouter(prefix="/game", tags=["game"])


@router.post("/new", response_model=GameState)
def new_game(request: CreateGameRequest) -> GameState:
    # No broadcast: nobody can be subscribed before the game id exists.
    try:
        game = state.new_game(request.player_names, request.auto_setup)
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    state.store.add(game)
    return game


@router.get("/{game_id}", response_model=GameState)
def get_game(game_id: str) -> GameState:
    return get_game_or_404(game_id)


@router.post("/{game_id}/roll-dice", response_model=GameState)
async def roll_dice(game_id: str) -> GameState:
    return await _broadcast(actions.roll_dice(get_game_or_404(game_id)))


@router.post("/{game_id}/place-settlement", response_model=GameState)
async def place_settlement(game_id: str, request: PlaceSettlementRequest) -> GameState:
    return await _broadcast(
        actions.place_settlement(get_game_or_404(game_id), request.vertex)
    )


@router.post("/{game_id}/place-road", response_model=GameState)
async def place_road(game_id: str, request: PlaceRoadRequest) -> GameState:
    return await _broadcast(actions.place_road(get_game_or_404(game_id), request.edge))


@router.post("/{game_id}/place-city", response_model=GameState)
async def place_city(game_id: str, request: PlaceCityRequest) -> GameState:
    return await _broadcast(actions.place_city(get_game_or_404(game_id), request.vertex))


@router.post("/{game_id}/buy-dev-card", response_model=GameState)
async def buy_dev_card(game_id: str) -> GameState:
    return await _broadcast(actions.buy_dev_card(get_game_or_404(game_id)))


@router.post("/{game_id}/trade-bank", response_model=GameState)
async def trade_bank(game_id: str, request: TradeBankRequest) -> GameState:
    return await _broadcast(
        actions.trade_bank(get_game_or_404(game_id), request.give, request.receive)
    )


@router.post("/{game_id}/end-turn", response_model=GameState)
async def end_turn(game_id: str) -> GameState:
    return await _broadcast(actions.end_turn(get_game_or_404(game_id)))


app.include_router(router)


@app.websocket("/ws/{game_id}")
async def game_socket(websocket: WebSocket, game_id: str) -> None:
    """Subscribe to a game's state feed.

    The latest snapshot is sent on connect (which also covers reconnects)
    and after every successful action. Incoming messages are ignored;
    the receive loop exists to detect disconnects.
    """
    game = state.store.get(game_id)
    if game is None:
        await websocket.accept()
        await websocket.close(code=4404, reason=f"No game with id {game_id!r}")
        return

    await manager.connect(game_id, websocket)
    try:
        await manager.send_state(websocket, game)
        while True:
            await websocket.receive_text()
    except WebSocketDisconnect:
        pass
    finally:
        manager.disconnect(game_id, websocket)
