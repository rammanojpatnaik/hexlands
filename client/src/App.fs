/// Application root: owns the game state, wires API calls to the UI,
/// and mounts the React tree.
module Hexlands.App

open Browser.Dom
open Fable.Core
open Fable.React
open Fable.React.Props
open Hexlands.Types

let private defaultPlayers = [ "Red"; "Blue"; "Orange" ]

let private app =
    FunctionComponent.Of(
        (fun () ->
            let game = Hooks.useState<GameState option> None
            let error = Hooks.useState<string option> None

            let load (request: unit -> JS.Promise<GameState>) =
                request ()
                |> Promise.map (fun updated ->
                    game.update (Some updated)
                    error.update None)
                |> Promise.catch (fun exn -> error.update (Some exn.Message))
                |> ignore

            Hooks.useEffect ((fun () -> load (fun () -> Api.createGame defaultPlayers)), [||])

            div [ ClassName "app" ] [
                h1 [] [ str "Hexlands — Catan" ]
                (match error.current with
                 | Some message -> div [ ClassName "error" ] [ str message ]
                 | None -> nothing)
                (match game.current with
                 | None -> div [ ClassName "loading" ] [ str "Setting up the board..." ]
                 | Some g ->
                     fragment [] [
                         Dice.view
                             g
                             (fun () -> load (fun () -> Api.rollDice g.Id))
                             (fun () -> load (fun () -> Api.endTurn g.Id))
                         div [ ClassName "layout" ] [
                             Board.view g
                             Players.view g
                         ]
                         div [ ClassName "log" ] (
                             g.Log
                             |> List.rev
                             |> List.mapi (fun index line ->
                                 div [ Key (string index); ClassName "log-line" ] [ str line ])
                         )
                     ])
            ]),
        "App"
    )

ReactDom.render (app (), document.getElementById "root")
