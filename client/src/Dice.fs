/// Dice panel: last roll display plus roll / end-turn controls.
module Hexlands.Dice

open Fable.React
open Fable.React.Props
open Hexlands.Types

let view (game: GameState) (onRoll: unit -> unit) (onEndTurn: unit -> unit) =
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
            | None when game.Phase = "setup" -> sprintf "Setup phase — %s places" current.Name
            | None -> sprintf "%s's turn — roll the dice" current.Name

    div [ ClassName "dice-panel" ] [
        div [ ClassName "dice-result" ] [ str status ]
        button [ ClassName "btn"
                 OnClick (fun _ -> onRoll ())
                 Disabled (game.Phase <> "roll") ] [ str "Roll dice" ]
        button [ ClassName "btn secondary"
                 OnClick (fun _ -> onEndTurn ())
                 Disabled (game.Phase <> "actions") ] [ str "End turn" ]
    ]
