# Hexlands — Online Catan Board Game

A full-stack Settlers of Catan board game, structured as a monorepo:

- **`/client`** — Fable (F# → JavaScript) frontend using Fable.React, bundled with Vite
- **`/server`** — FastAPI (Python) backend that owns all game logic

```
hexlands/
├── client/                      # Fable + F# + Vite frontend
│   ├── package.json             # npm deps and dev/build scripts
│   ├── vite.config.js           # Vite config, proxies /api → :8000
│   ├── index.html
│   ├── .config/
│   │   └── dotnet-tools.json    # pins the Fable compiler as a dotnet tool
│   └── src/
│       ├── Client.fsproj        # F# project: compile order + NuGet packages
│       ├── Types.fs             # Domain types mirroring the API JSON contract
│       ├── HexGrid.fs           # Axial hex coords → SVG pixel geometry
│       ├── Api.fs               # Fetch + Thoth.Json client for the backend
│       ├── Board.fs             # SVG board rendering (tiles, tokens, robber)
│       ├── Players.fs           # Player panel (resources, victory points)
│       ├── Dice.fs              # Dice panel (roll / end-turn controls)
│       ├── App.fs               # React root, state management, entry point
│       └── styles.css
└── server/                      # FastAPI backend
    ├── requirements.txt
    └── app/
        ├── main.py              # FastAPI app + REST and WebSocket endpoints
        ├── connections.py       # WebSocket subscriptions + state broadcasting
        ├── models.py            # Pydantic models for the full game state
        └── game/
            ├── hexgrid.py       # Axial hex-coordinate math
            ├── board.py         # Board generation (terrains, tokens, robber)
            ├── dice.py          # Dice rolling
            ├── players.py       # Player model and resource hands
            └── state.py         # Game state, turn flow, in-memory store
```

## Prerequisites

- **.NET SDK 8.0+** (compiles the F# frontend via Fable)
- **Node.js 18+** and npm
- **Python 3.10+**

## Setup

### 1. Backend (FastAPI)

```bash
cd server
python -m venv .venv
source .venv/bin/activate        # Windows: .venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8000
```

The API now runs at `http://localhost:8000` with interactive docs at `http://localhost:8000/docs`.

### 2. Frontend (Fable + Vite)

In a second terminal:

```bash
cd client
npm install        # also restores the Fable dotnet tool (postinstall)
npm run dev        # compiles F# → JS in watch mode and starts Vite
```

Open `http://localhost:5173`. The Vite dev server proxies all `/api` requests to the backend on port 8000, so no CORS setup is needed in development (the server also enables CORS as a fallback).

To produce a production bundle: `npm run build` (output in `client/dist/`).

## API Overview

All actions apply to the current player and are validated against the game
rules — violations return **409 Conflict** with a human-readable `detail`;
malformed input returns **422**.

| Method | Endpoint                           | Body                                  | Description                                        |
| ------ | ---------------------------------- | ------------------------------------- | -------------------------------------------------- |
| POST   | `/game/new`                        | `{player_names?, auto_setup?}`        | Create a game (auto_setup plays out setup randomly) |
| GET    | `/game/{id}`                       | —                                     | Fetch current game state                           |
| POST   | `/game/{id}/roll-dice`             | —                                     | Roll the dice and distribute production            |
| POST   | `/game/{id}/place-settlement`      | `{"vertex": {"q","r","corner"}}`      | Build a settlement (setup or actions phase)        |
| POST   | `/game/{id}/place-road`            | `{"edge": {"q","r","edge"}}`          | Build a road (setup or actions phase)              |
| POST   | `/game/{id}/place-city`            | `{"vertex": {"q","r","corner"}}`      | Upgrade one of your settlements to a city          |
| POST   | `/game/{id}/buy-dev-card`          | —                                     | Buy the top development card                       |
| POST   | `/game/{id}/trade-bank`            | `{"give": "wood", "receive": "ore"}`  | Trade 4:1 with the bank                            |
| POST   | `/game/{id}/end-turn`              | —                                     | Pass the turn to the next player                   |
| WS     | `/ws/{id}`                         | —                                     | Subscribe to the game's live state feed            |

### WebSocket feed

Connect to `ws://localhost:8000/ws/{game_id}` to receive the full game
state as JSON — the same snake_case shape as the REST responses, with the
development-card deck excluded. A snapshot is sent immediately on connect
(which doubles as reconnect catch-up: there is no replay to miss) and after
every successful action by any player. Failed actions (409s) broadcast
nothing. Unknown game ids are closed with application code `4404`. Clients
never need to send messages on the socket.

Rules enforced: phase order (setup → roll → actions), snake-order setup with
the second settlement collecting starting resources, the distance rule,
road/settlement network connectivity (opponents' buildings block roads),
build costs, piece limits (5 settlements / 4 cities / 15 roads), 4:1 bank
trades, and the 10-VP win check. Vertices are hex corners 0–5 (clockwise
from upper-right), edges are hex sides 0–5 (side *i* joins corners *i* and
*i+1*); any of the shared aliases for the same physical corner/side is
accepted (see `server/app/game/hexgrid.py`).

The JSON contract uses snake_case field names; the F# client decodes them with Thoth.Json's `SnakeCase` strategy (see `client/src/Types.fs` and `server/app/models.py`).

## Gameplay status

This is a playable scaffold, not the full ruleset yet:

- ✅ Full game state modeled in Pydantic (`server/app/models.py`): 2–4 players, resource cards (wood, brick, sheep, wheat, ore), settlements, roads, cities, development cards, turn order, dice rolls, phases, and victory points — stored in memory keyed by game id
- ✅ Standard 19-hex board with shuffled terrains and number tokens, robber on the desert
- ✅ Rule-validated actions for the whole build loop: setup placements (snake order, free, starting resources), settlements, roads, cities, development-card purchases, and 4:1 bank trades
- ✅ Real resource production: settlements collect 1 and cities 2 from adjacent tiles on a matching roll; the robber blocks its tile
- ✅ Turn rotation with phase guards (`setup` → `roll` → `actions` → next player), game log, and a 10-VP win check
- ✅ Live multiplayer feed: `/ws/{game_id}` broadcasts the full state to every subscriber on each action, with snapshot-on-connect reconnect handling
- 🚧 Roadmap: wiring the F# client to the WebSocket feed, board UI for placing pieces from the browser, robber movement + discards on a 7, playing development cards, domestic trades, harbours (3:1 / 2:1), longest road / largest army

## Notes

- Games are stored **in memory** on the server — restarting the backend clears all games.
- Number-token placement is randomized; the official "spiral" placement (which keeps 6s and 8s apart) is a noted TODO in `server/app/game/board.py`.
- The frontend pins React 17 to use Fable.React's stable `ReactDom.render` entry point.
