/// REST client for the FastAPI backend, built on Fable.SimpleHttp.
/// Every endpoint returns the full game state; rule violations (409)
/// raise ApiError with the server's human-readable detail message.
module Hexlands.Api

open Fable.SimpleHttp
open Thoth.Json
open Hexlands.Types

exception ApiError of string

type private CreateGamePayload = { PlayerName: string; NumPlayers: int }
type private JoinGamePayload = { PlayerName: string }
type private VertexPayload = { Vertex: VertexCoord }
type private EdgePayload = { Edge: EdgeCoord }
type private TradePayload = { Give: string; Receive: string }

let private decodeGame (json: string) : GameState =
    match Decode.Auto.fromString<GameState> (json, caseStrategy = SnakeCase) with
    | Ok game -> game
    | Error message -> raise (ApiError (sprintf "Could not decode game state: %s" message))

/// Pull the "detail" field out of a FastAPI error body.
let private detailOf (body: string) =
    match Decode.fromString (Decode.field "detail" Decode.string) body with
    | Ok detail -> detail
    | Error _ -> body

let private handle (response: HttpResponse) : GameState =
    if response.statusCode >= 200 && response.statusCode < 300 then
        decodeGame response.responseText
    else
        raise (ApiError (detailOf response.responseText))

let private get (url: string) : Async<GameState> =
    async {
        let! response = Http.request url |> Http.method GET |> Http.send
        return handle response
    }

let private post (url: string) (body: string option) : Async<GameState> =
    async {
        let request = Http.request url |> Http.method POST

        let request =
            match body with
            | Some json ->
                request
                |> Http.header (Headers.contentType "application/json")
                |> Http.content (BodyContent.Text json)
            | None -> request

        let! response = Http.send request
        return handle response
    }

// inline so Fable can resolve the concrete payload type for Encode.Auto
let inline private encode payload =
    Encode.Auto.toString (space = 0, value = payload, caseStrategy = SnakeCase)

let createGame (playerName: string) (numPlayers: int) : Async<GameState> =
    post "/game/new" (Some (encode { PlayerName = playerName; NumPlayers = numPlayers }))

let joinGame (gameId: string) (playerName: string) : Async<GameState> =
    post (sprintf "/game/%s/join" gameId) (Some (encode { PlayerName = playerName }))

let getGame (gameId: string) : Async<GameState> =
    get (sprintf "/game/%s" gameId)

let rollDice (gameId: string) : Async<GameState> =
    post (sprintf "/game/%s/roll-dice" gameId) None

let placeSettlement (gameId: string) (vertex: VertexCoord) : Async<GameState> =
    post (sprintf "/game/%s/place-settlement" gameId) (Some (encode { Vertex = vertex }))

let placeRoad (gameId: string) (edge: EdgeCoord) : Async<GameState> =
    post (sprintf "/game/%s/place-road" gameId) (Some (encode { Edge = edge }))

let placeCity (gameId: string) (vertex: VertexCoord) : Async<GameState> =
    post (sprintf "/game/%s/place-city" gameId) (Some (encode { Vertex = vertex }))

let buyDevCard (gameId: string) : Async<GameState> =
    post (sprintf "/game/%s/buy-dev-card" gameId) None

let tradeBank (gameId: string) (give: string) (receive: string) : Async<GameState> =
    post (sprintf "/game/%s/trade-bank" gameId) (Some (encode { Give = give; Receive = receive }))

let endTurn (gameId: string) : Async<GameState> =
    post (sprintf "/game/%s/end-turn" gameId) None
