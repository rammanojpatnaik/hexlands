/// Dice / actions panel: roll status plus roll, end-turn, and
/// development-card controls.
module Hexlands.Dice

open Fable.React
open Fable.React.Props
open Hexlands.Types

let view
    (game: GameState)
    (onRoll: unit -> unit)
    (onEndTurn: unit -> unit)
    (onBuyDevCard: unit -> unit)
    =
    let current = game.Players.[game.CurrentPlayer]

    let status =
        match game.Winner with
        | Some winnerId ->
            game.Players
            |> List.tryFind (fun p -> p.Id = winnerId)
            |> Option.map (fun p -> p.Name)
            |> Option.defaultValue "Someone"
            |> sprintf "🏆 %s wins!"
        | None ->
            match game.LastRoll with
            | Some roll -> sprintf "🎲 %d + %d = %d" roll.Die1 roll.Die2 roll.Total
            | None when game.Phase = "setup" -> sprintf "Setup — %s places" current.Name
            | None -> sprintf "%s to roll" current.Name

    div [ ClassName "space-y-3 rounded-xl border border-amber-200 bg-white p-4 shadow-sm" ] [
        div [ ClassName "text-lg font-medium" ] [ str status ]
        div [ ClassName "flex gap-2" ] [
            button [ ClassName "flex-1 rounded-lg bg-blue-600 py-2 font-semibold text-white transition hover:bg-blue-700 disabled:cursor-default disabled:opacity-40"
                     OnClick (fun _ -> onRoll ())
                     Disabled (game.Phase <> "roll") ] [ str "Roll dice" ]
            button [ ClassName "flex-1 rounded-lg border border-blue-600 py-2 font-semibold text-blue-600 transition hover:bg-blue-50 disabled:cursor-default disabled:opacity-40"
                     OnClick (fun _ -> onEndTurn ())
                     Disabled (game.Phase <> "actions") ] [ str "End turn" ]
        ]
        button [ ClassName "w-full rounded-lg border border-stone-300 py-1.5 text-sm transition hover:bg-stone-50 disabled:cursor-default disabled:opacity-40"
                 OnClick (fun _ -> onBuyDevCard ())
                 Disabled (game.Phase <> "actions") ] [ str "Buy development card" ]
    ]
