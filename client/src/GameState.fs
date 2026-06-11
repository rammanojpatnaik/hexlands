/// Elmish state management and routing: model, messages, update, URL
/// handling, and the WebSocket subscription that keeps the open game
/// fresh in realtime. Views dispatch messages; all API traffic and
/// navigation flows through update.
module Hexlands.GameState

open System
open Browser
open Browser.Types
open Elmish
open Elmish.Navigation
open Elmish.UrlParser
open Thoth.Json
open Hexlands.Types

type Page =
    | HomePage
    | GamePage of gameId: string

type Model =
    { Page: Page
      // home / lobby form fields
      PlayerName: string
      JoinId: string
      NumPlayers: int
      // the open game
      Game: GameState option
      Error: string option
      Connected: bool }

type Msg =
    | PlayerNameChanged of string
    | JoinIdChanged of string
    | NumPlayersChanged of int
    | CreateGame
    | OpenGame
    | JoinLobby
    | GameCreated of GameState
    | GameReceived of GameState
    | GameUpdated of GameState
    | ApiFailed of exn
    | SocketChanged of bool
    | RollDice
    | EndTurn
    | BuyDevCard
    | TradeBank of give: string * receive: string

// --- routing ----------------------------------------------------------

/// Hash routes: "#/" -> home, "#/game/{id}" -> game page.
let route: Parser<Page -> Page, Page> =
    oneOf [ map HomePage top
            map GamePage (s "game" </> str) ]

let private gameUrl (gameId: string) = sprintf "#/game/%s" gameId

// --- commands -----------------------------------------------------------

let private call (request: Async<GameState>) (ok: GameState -> Msg) : Cmd<Msg> =
    Cmd.OfAsync.either (fun () -> request) () ok ApiFailed

let urlUpdate (page: Page option) (model: Model) : Model * Cmd<Msg> =
    match page with
    | Some HomePage ->
        { model with Page = HomePage; Game = None; Error = None; Connected = false }, Cmd.none
    | Some (GamePage gameId) ->
        let next = { model with Page = GamePage gameId; Error = None }

        match model.Game with
        | Some game when game.Id = gameId -> next, Cmd.none
        | _ ->
            { next with Game = None; Connected = false },
            call (Api.getGame gameId) GameReceived
    | None -> model, Navigation.modifyUrl "#/"

let init (page: Page option) : Model * Cmd<Msg> =
    let model =
        { Page = HomePage
          PlayerName = ""
          JoinId = ""
          NumPlayers = 3
          Game = None
          Error = None
          Connected = false }

    urlUpdate page model

// --- update ---------------------------------------------------------------

let private describe (ex: exn) =
    match ex with
    | Api.ApiError detail -> detail
    | _ -> ex.Message

let private withGame (model: Model) (action: string -> Cmd<Msg>) : Model * Cmd<Msg> =
    match model.Game with
    | Some game -> model, action game.Id
    | None -> model, Cmd.none

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | PlayerNameChanged value -> { model with PlayerName = value }, Cmd.none
    | JoinIdChanged value -> { model with JoinId = value }, Cmd.none
    | NumPlayersChanged value -> { model with NumPlayers = value }, Cmd.none

    | CreateGame ->
        let name = model.PlayerName.Trim()

        if name = "" then
            { model with Error = Some "Enter your name first." }, Cmd.none
        else
            { model with Error = None }, call (Api.createGame name model.NumPlayers) GameCreated

    | OpenGame ->
        let gameId = model.JoinId.Trim()

        if gameId = "" then
            { model with Error = Some "Enter a game id." }, Cmd.none
        else
            { model with Error = None }, Navigation.newUrl (gameUrl gameId)

    | JoinLobby ->
        match model.Page, model.PlayerName.Trim() with
        | GamePage _, "" -> { model with Error = Some "Enter your name first." }, Cmd.none
        | GamePage gameId, name -> { model with Error = None }, call (Api.joinGame gameId name) GameReceived
        | HomePage, _ -> model, Cmd.none

    | GameCreated game ->
        { model with Game = Some game; Error = None }, Navigation.newUrl (gameUrl game.Id)

    | GameReceived game -> { model with Game = Some game; Error = None }, Cmd.none

    | GameUpdated game ->
        // realtime push: only apply if we are still looking at that game
        match model.Page with
        | GamePage gameId when gameId = game.Id -> { model with Game = Some game }, Cmd.none
        | _ -> model, Cmd.none

    | ApiFailed ex -> { model with Error = Some (describe ex) }, Cmd.none
    | SocketChanged connected -> { model with Connected = connected }, Cmd.none

    | RollDice -> withGame model (fun gameId -> call (Api.rollDice gameId) GameReceived)
    | EndTurn -> withGame model (fun gameId -> call (Api.endTurn gameId) GameReceived)
    | BuyDevCard -> withGame model (fun gameId -> call (Api.buyDevCard gameId) GameReceived)
    | TradeBank (give, receive) ->
        withGame model (fun gameId -> call (Api.tradeBank gameId give receive) GameReceived)

// --- WebSocket subscription -------------------------------------------------

let private decodeGame (json: string) =
    Decode.Auto.fromString<GameState> (json, caseStrategy = SnakeCase)

let private gameFeed (gameId: string) (dispatch: Dispatch<Msg>) : IDisposable =
    let mutable disposed = false
    let mutable socket: WebSocket option = None

    let rec start () =
        if not disposed then
            let protocol =
                if Dom.window.location.protocol = "https:" then "wss:" else "ws:"

            let ws =
                WebSocket.Create(sprintf "%s//%s/ws/%s" protocol Dom.window.location.host gameId)

            ws.onopen <- fun _ -> dispatch (SocketChanged true)

            ws.onmessage <-
                fun event ->
                    match decodeGame (string event.data) with
                    | Ok game -> dispatch (GameUpdated game)
                    | Error message ->
                        dispatch (ApiFailed (Exception (sprintf "Bad state from server: %s" message)))

            ws.onclose <-
                fun _ ->
                    dispatch (SocketChanged false)
                    // The server resends the full state on connect, so a
                    // delayed retry misses nothing.
                    Dom.window.setTimeout ((fun () -> start ()), 1000) |> ignore

            socket <- Some ws

    start ()

    { new IDisposable with
        member _.Dispose() =
            disposed <- true

            match socket with
            | Some ws ->
                ws.onclose <- ignore
                ws.close ()
            | None -> () }

/// One live feed per open game page; Elmish starts and stops it as the
/// game id in the URL changes.
let subscriptions (model: Model) : Sub<Msg> =
    match model.Page with
    | GamePage gameId -> [ [ "game-feed"; gameId ], gameFeed gameId ]
    | HomePage -> []
