/// Application root: the Elmish program. Routes between the home page
/// (create / join) and the game page (lobby while waiting, full game UI
/// once started), with the WebSocket subscription keeping everything live.
module Hexlands.App

open Elmish
open Elmish.Navigation
open Elmish.UrlParser
open Elmish.React
open Fable.React
open Fable.React.Props
open Hexlands.Types
open Hexlands.GameState

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

let private lobbyView (gameId: string) (game: GameState) (model: Model) (dispatch: Msg -> unit) =
    div [ ClassName "mx-auto max-w-md" ] [
        div [ ClassName "space-y-4 rounded-xl border border-amber-200 bg-white p-6 shadow-sm" ] [
            h2 [ ClassName "text-lg font-semibold" ] [ str "Waiting for players…" ]
            p [ ClassName "text-sm text-stone-600" ] [ str "Share this game id with your friends:" ]
            div [ ClassName "select-all rounded-lg bg-stone-100 px-4 py-2 text-center font-mono text-lg" ] [
                str gameId
            ]
            p [ ClassName "text-sm" ] [
                str (sprintf "%d of %d players joined" game.Players.Length game.NumPlayers)
            ]
            div [ ClassName "flex flex-wrap gap-2" ] (
                game.Players
                |> List.map (fun player ->
                    span [ Key player.Name
                           ClassName "flex items-center gap-1.5 rounded-full bg-stone-100 px-3 py-1 text-sm" ] [
                        span [ ClassName "h-2.5 w-2.5 rounded-full"
                               Style [ BackgroundColor (playerColor player.Color) ] ] []
                        str player.Name
                    ])
            )
            div [ ClassName "space-y-2 border-t border-amber-100 pt-4" ] [
                p [ ClassName "text-sm text-stone-600" ] [ str "Joining from an invite? Enter your name:" ]
                input [ ClassName "w-full rounded-lg border border-stone-300 px-3 py-2"
                        Placeholder "Your name"
                        Value model.PlayerName
                        OnChange (fun ev -> dispatch (PlayerNameChanged ev.Value)) ]
                button [ ClassName "w-full rounded-lg bg-blue-600 py-2 font-semibold text-white transition hover:bg-blue-700"
                         OnClick (fun _ -> dispatch JoinLobby) ] [ str "Join game" ]
            ]
        ]
    ]

let private gameView (game: GameState) (dispatch: Msg -> unit) =
    fragment [] [
        div [ ClassName "flex flex-col items-start gap-6 lg:flex-row" ] [
            main [ ClassName "min-w-0 flex-1" ] [ Board.view game ]
            aside [ ClassName "w-full shrink-0 space-y-4 lg:w-80" ] [
                Dice.view
                    game
                    (fun () -> dispatch RollDice)
                    (fun () -> dispatch EndTurn)
                    (fun () -> dispatch BuyDevCard)
                Trade.view {| Game = game
                              OnTrade = fun (give, receive) -> dispatch (TradeBank (give, receive)) |}
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
    ]

let private view (model: Model) (dispatch: Msg -> unit) =
    div [ ClassName "mx-auto max-w-6xl px-6 py-6" ] [
        header [ ClassName "mb-4 flex items-center justify-between gap-4" ] [
            h1 [ ClassName "text-2xl font-bold tracking-tight" ] [
                a [ Href "#/" ] [ str "Hexlands — Catan" ]
            ]
            (match model.Page, model.Game with
             | GamePage _, Some game when game.Phase <> "lobby" -> turnIndicator game
             | _ -> nothing)
        ]
        (match model.Error with
         | Some message ->
             banner "mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-2 text-red-800" message
         | None -> nothing)
        (match model.Page, model.Game with
         | GamePage _, Some _ when not model.Connected ->
             banner
                 "mb-4 rounded-lg border border-amber-300 bg-amber-100 px-4 py-2 text-sm text-amber-900"
                 "Connecting to the live feed…"
         | _ -> nothing)
        (match model.Page with
         | HomePage -> Home.view model dispatch
         | GamePage gameId ->
             match model.Game with
             | None ->
                 div [ ClassName "py-16 text-center italic text-stone-500" ] [
                     str "Loading game..."
                 ]
             | Some game when game.Phase = "lobby" -> lobbyView gameId game model dispatch
             | Some game -> gameView game dispatch)
    ]

Program.mkProgram init update view
|> Program.withSubscription subscriptions
|> Program.toNavigable (parseHash route) urlUpdate
|> Program.withReactSynchronous "root"
|> Program.run
