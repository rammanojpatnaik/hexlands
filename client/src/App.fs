/// Application root: assembles the full game UI from the GameState store.
/// Layout: header with turn indicator, hex board in the center, sidebar
/// with dice/actions, bank trade, and player cards; game log below.
module Hexlands.App

open Browser.Dom
open Fable.React
open Fable.React.Props
open Hexlands.Types

let private defaultPlayers = [ "Red"; "Blue"; "Orange" ]

let private banner (classes: string) (message: string) =
    div [ ClassName classes ] [ str message ]

let private turnIndicator (game: GameState) =
    let current = game.Players.[game.CurrentPlayer]

    div [ ClassName "flex items-center gap-2 rounded-full border border-amber-200 bg-white px-4 py-1.5 shadow-sm" ] [
        span [ ClassName "h-3 w-3 rounded-full"
               Style [ BackgroundColor (playerColor current.Color) ] ] []
        span [ ClassName "font-semibold" ] [ str current.Name ]
        span [ ClassName "text-xs uppercase tracking-wide text-stone-500" ] [ str game.Phase ]
    ]

let private app =
    FunctionComponent.Of(
        (fun () ->
            let model = GameState.useGameState ()

            Hooks.useEffect ((fun () -> GameState.startGame defaultPlayers), [||])

            div [ ClassName "mx-auto max-w-6xl px-6 py-6" ] [
                header [ ClassName "mb-4 flex items-center justify-between gap-4" ] [
                    h1 [ ClassName "text-2xl font-bold tracking-tight" ] [ str "Hexlands — Catan" ]
                    (match model.Game with
                     | Some game -> turnIndicator game
                     | None -> nothing)
                ]
                (match model.Error with
                 | Some message ->
                     banner "mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-2 text-red-800" message
                 | None -> nothing)
                (if not model.Connected && model.Game.IsSome then
                     banner
                         "mb-4 rounded-lg border border-amber-300 bg-amber-100 px-4 py-2 text-sm text-amber-900"
                         "Live connection lost — reconnecting…"
                 else
                     nothing)
                (match model.Game with
                 | None ->
                     div [ ClassName "py-16 text-center italic text-stone-500" ] [
                         str "Setting up the board..."
                     ]
                 | Some game ->
                     fragment [] [
                         div [ ClassName "flex flex-col items-start gap-6 lg:flex-row" ] [
                             main [ ClassName "min-w-0 flex-1" ] [ Board.view game ]
                             aside [ ClassName "w-full shrink-0 space-y-4 lg:w-80" ] [
                                 Dice.view game GameState.rollDice GameState.endTurn GameState.buyDevCard
                                 Trade.view {| Game = game |}
                                 Players.view game
                             ]
                         ]
                         div [ ClassName "mt-6 max-h-48 space-y-1 overflow-y-auto rounded-xl border border-amber-200 bg-white p-4 text-sm shadow-sm" ] (
                             game.Log
                             |> List.rev
                             |> List.mapi (fun index line ->
                                 div [ Key (string index)
                                       ClassName "border-b border-dashed border-amber-100 pb-1 last:border-0" ] [
                                     str line
                                 ])
                         )
                     ])
            ]),
        "App"
    )

ReactDom.render (app (), document.getElementById "root")
