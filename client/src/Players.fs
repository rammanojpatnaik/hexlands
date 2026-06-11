/// Player panel: names, color swatches, resource hands, victory points.
module Hexlands.Players

open Fable.React
open Fable.React.Props
open Hexlands.Types

let private playerCard (isCurrent: bool) (player: Player) =
    let count resource =
        player.Resources
        |> Map.tryFind resource
        |> Option.defaultValue 0

    div [ Key player.Name
          ClassName (if isCurrent then "player-card current" else "player-card") ] [
        div [ ClassName "player-name" ] [
            span [ ClassName "player-swatch"
                   Style [ BackgroundColor (playerColor player.Color) ] ] []
            str player.Name
            (if isCurrent then
                 span [ ClassName "turn-badge" ] [ str "turn" ]
             else
                 nothing)
        ]
        div [ ClassName "player-resources" ] (
            resourceOrder
            |> List.map (fun resource ->
                span [ Key resource; ClassName "resource" ] [
                    str (sprintf "%s %d" resource (count resource))
                ])
        )
        div [ ClassName "player-vp" ] [
            str (sprintf "%d VP" player.VictoryPoints)
        ]
    ]

let view (game: GameState) =
    div [ ClassName "players" ] (
        game.Players
        |> List.mapi (fun index player -> playerCard (index = game.CurrentPlayer) player)
    )
