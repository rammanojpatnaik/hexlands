/// HTTP client for the FastAPI backend. All endpoints return the full
/// game state, decoded from snake_case JSON.
module Hexlands.Api

open Fable.Core
open Fable.Core.JsInterop
open Fetch
open Thoth.Json
open Hexlands.Types

type private CreateGameRequest = { PlayerNames: string list }

let private decodeGame (json: string) : GameState =
    match Decode.Auto.fromString<GameState> (json, caseStrategy = SnakeCase) with
    | Ok game -> game
    | Error message -> failwithf "Could not decode game state: %s" message

let private requestGame (url: string) (props: RequestProperties list) : JS.Promise<GameState> =
    promise {
        let! response = fetch url props
        let! body = response.text ()
        return decodeGame body
    }

let createGame (playerNames: string list) : JS.Promise<GameState> =
    let body =
        Encode.Auto.toString (0, { PlayerNames = playerNames }, caseStrategy = SnakeCase)

    requestGame
        "/game/new"
        [ Method HttpMethod.POST
          requestHeaders [ ContentType "application/json" ]
          Body !^body ]

let getGame (gameId: string) : JS.Promise<GameState> =
    requestGame (sprintf "/game/%s" gameId) []

let rollDice (gameId: string) : JS.Promise<GameState> =
    requestGame (sprintf "/game/%s/roll-dice" gameId) [ Method HttpMethod.POST ]

let endTurn (gameId: string) : JS.Promise<GameState> =
    requestGame (sprintf "/game/%s/end-turn" gameId) [ Method HttpMethod.POST ]
