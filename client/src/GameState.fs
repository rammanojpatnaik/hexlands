/// Client-side game store. Holds the latest game state, keeps it fresh
/// through the server's WebSocket feed (with automatic reconnect), and
/// exposes the player actions to React components. Components subscribe
/// via the useGameState hook and never talk to the API directly.
module Hexlands.GameState

open System
open Browser
open Browser.Types
open Fable.React
open Thoth.Json
open Hexlands.Types

type Model =
    { Game: GameState option
      Error: string option
      Connected: bool }

// --- store -------------------------------------------------------------

let mutable private model =
    { Game = None; Error = None; Connected = false }

let private listeners = ResizeArray<Model -> unit>()

let private setModel (next: Model) =
    model <- next
    for listener in List.ofSeq listeners do
        listener next

/// Current snapshot of the store (components should prefer useGameState).
let current () = model

/// Subscribe to store changes; returns an unsubscribe function.
let subscribe (listener: Model -> unit) : unit -> unit =
    listeners.Add listener
    fun () -> listeners.Remove listener |> ignore

// --- WebSocket feed -----------------------------------------------------

let mutable private socket: WebSocket option = None

let private decodeGame (json: string) =
    Decode.Auto.fromString<GameState> (json, caseStrategy = SnakeCase)

let rec private connect (gameId: string) =
    let protocol =
        if Dom.window.location.protocol = "https:" then "wss:" else "ws:"

    let ws =
        WebSocket.Create(sprintf "%s//%s/ws/%s" protocol Dom.window.location.host gameId)

    ws.onopen <- fun _ -> setModel { model with Connected = true }

    ws.onmessage <-
        fun event ->
            match decodeGame (string event.data) with
            | Ok game -> setModel { model with Game = Some game; Error = None }
            | Error message ->
                setModel { model with Error = Some (sprintf "Bad state from server: %s" message) }

    ws.onclose <-
        fun _ ->
            setModel { model with Connected = false }
            // Retry after a short delay. The server resends the full
            // latest state on connect, so a reconnect misses nothing.
            Dom.window.setTimeout(
                (fun () ->
                    match model.Game with
                    | Some game when game.Id = gameId -> connect gameId
                    | _ -> ()),
                1000
            )
            |> ignore

    socket <- Some ws

let private disconnect () =
    match socket with
    | Some ws ->
        ws.onclose <- ignore // suppress the auto-reconnect
        ws.close ()
        socket <- None
    | None -> ()

// --- actions exposed to components ---------------------------------------

let private run (action: Async<GameState>) =
    async {
        try
            let! game = action
            setModel { model with Game = Some game; Error = None }
        with
        | Api.ApiError detail -> setModel { model with Error = Some detail }
        | ex -> setModel { model with Error = Some ex.Message }
    }
    |> Async.StartImmediate

let private withGame (action: string -> Async<GameState>) =
    match model.Game with
    | Some game -> run (action game.Id)
    | None -> ()

/// Create a game and open its realtime feed.
let startGame (playerNames: string list) =
    async {
        try
            let! game = Api.createGame playerNames
            setModel { model with Game = Some game; Error = None }
            disconnect ()
            connect game.Id
        with
        | Api.ApiError detail -> setModel { model with Error = Some detail }
        | ex -> setModel { model with Error = Some ex.Message }
    }
    |> Async.StartImmediate

let rollDice () = withGame Api.rollDice
let endTurn () = withGame Api.endTurn
let buyDevCard () = withGame Api.buyDevCard

let placeSettlement (vertex: VertexCoord) =
    withGame (fun gameId -> Api.placeSettlement gameId vertex)

let placeRoad (edge: EdgeCoord) =
    withGame (fun gameId -> Api.placeRoad gameId edge)

let placeCity (vertex: VertexCoord) =
    withGame (fun gameId -> Api.placeCity gameId vertex)

let tradeBank (give: string) (receive: string) =
    withGame (fun gameId -> Api.tradeBank gameId give receive)

// --- React adapter --------------------------------------------------------

/// Hook: subscribe the calling component to the store. The component
/// re-renders on every store change (REST responses and WebSocket pushes).
let useGameState () : Model =
    let state = Hooks.useState model

    Hooks.useEffectDisposable (
        (fun () ->
            let unsubscribe = subscribe state.update
            // catch up on anything that changed between render and mount
            state.update model

            { new IDisposable with
                member _.Dispose() = unsubscribe () }),
        [||]
    )

    state.current
