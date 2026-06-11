/// Home page: create a new game (returns a shareable id) or join one.
module Hexlands.Home

open Fable.React
open Fable.React.Props
open Hexlands.GameState

let private card (title: string) (children: ReactElement list) =
    div [ ClassName "space-y-4 rounded-xl border border-amber-200 bg-white p-6 shadow-sm" ] (
        h2 [ ClassName "text-lg font-semibold" ] [ str title ] :: children
    )

let private textInput (placeholderText: string) (value: string) (onChange: string -> unit) =
    input [ ClassName "w-full rounded-lg border border-stone-300 px-3 py-2"
            Placeholder placeholderText
            Value value
            OnChange (fun ev -> onChange ev.Value) ]

let view (model: Model) (dispatch: Msg -> unit) =
    div [ ClassName "mx-auto max-w-md space-y-6" ] [
        card "Create a game" [
            textInput "Your name" model.PlayerName (PlayerNameChanged >> dispatch)
            div [] [
                label [ ClassName "mb-1 block text-xs uppercase tracking-wide text-stone-500" ] [
                    str "Players"
                ]
                select [ ClassName "w-full rounded-lg border border-stone-300 bg-white px-2 py-2"
                         Value (string model.NumPlayers)
                         OnChange (fun ev -> dispatch (NumPlayersChanged (int ev.Value))) ] [
                    option [ Value "2" ] [ str "2 players" ]
                    option [ Value "3" ] [ str "3 players" ]
                    option [ Value "4" ] [ str "4 players" ]
                ]
            ]
            button [ ClassName "w-full rounded-lg bg-blue-600 py-2 font-semibold text-white transition hover:bg-blue-700"
                     OnClick (fun _ -> dispatch CreateGame) ] [ str "Create game" ]
            p [ ClassName "text-xs text-stone-500" ] [
                str "You'll get a shareable game id; the game starts once everyone has joined."
            ]
        ]
        card "Join a game" [
            textInput "Game id (e.g. 7ad61cff)" model.JoinId (JoinIdChanged >> dispatch)
            button [ ClassName "w-full rounded-lg border border-blue-600 py-2 font-semibold text-blue-600 transition hover:bg-blue-50"
                     OnClick (fun _ -> dispatch OpenGame) ] [ str "Open lobby" ]
        ]
    ]
