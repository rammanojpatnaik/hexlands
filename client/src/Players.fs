/// Sidebar player cards: color swatch, resource hand, victory points.
module Hexlands.Players

open Fable.React
open Fable.React.Props
open Hexlands.Types

let private resourceChip (name: string) (count: int) =
    span [ Key name
           ClassName "rounded-md bg-stone-100 px-2 py-0.5 text-xs text-stone-700" ] [
        str (sprintf "%s %d" name count)
    ]

let private playerCard (isCurrent: bool) (player: Player) =
    let border =
        if isCurrent then "border-blue-500 ring-2 ring-blue-200"
        else "border-amber-200"

    div [ Key player.Name
          ClassName (sprintf "rounded-xl border bg-white p-3 shadow-sm %s" border) ] [
        div [ ClassName "mb-2 flex items-center gap-2" ] [
            span [ ClassName "h-3.5 w-3.5 rounded border border-black/20"
                   Style [ BackgroundColor (playerColor player.Color) ] ] []
            span [ ClassName "font-semibold" ] [ str player.Name ]
            (if isCurrent then
                 span [ ClassName "ml-auto rounded-full bg-blue-600 px-2 py-0.5 text-[10px] uppercase tracking-wider text-white" ] [
                     str "turn"
                 ]
             else
                 nothing)
        ]
        div [ ClassName "flex flex-wrap gap-1.5" ] (
            resourceOrder
            |> List.map (fun resource ->
                let count =
                    player.Resources
                    |> Map.tryFind resource
                    |> Option.defaultValue 0

                resourceChip resource count)
        )
        div [ ClassName "mt-2 text-sm font-semibold text-amber-800" ] [
            str (sprintf "%d victory points" player.VictoryPoints)
        ]
    ]

let view (game: GameState) =
    section [ ClassName "space-y-3" ] (
        game.Players
        |> List.map (fun player -> playerCard (player.Id = game.CurrentPlayer) player)
    )
