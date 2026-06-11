/// Bank trade panel: give 4 of one resource, receive 1 of another.
module Hexlands.Trade

open Fable.React
open Fable.React.Props
open Hexlands.Types

let private resourceSelect (caption: string) (value: string) (onChange: string -> unit) =
    div [ ClassName "flex-1" ] [
        label [ ClassName "mb-1 block text-xs uppercase tracking-wide text-stone-500" ] [
            str caption
        ]
        select [ ClassName "w-full rounded-lg border border-stone-300 bg-white px-2 py-1.5"
                 Value value
                 OnChange (fun ev -> onChange ev.Value) ] (
            resourceOrder
            |> List.map (fun resource -> option [ Key resource; Value resource ] [ str resource ])
        )
    ]

let view =
    FunctionComponent.Of(
        (fun (props: {| Game: GameState |}) ->
            let give = Hooks.useState "wood"
            let receive = Hooks.useState "ore"
            let sameResource = give.current = receive.current
            let active = props.Game.Phase = "actions"

            div [ ClassName "space-y-3 rounded-xl border border-amber-200 bg-white p-4 shadow-sm" ] [
                h2 [ ClassName "font-semibold" ] [ str "Bank trade · 4 : 1" ]
                div [ ClassName "flex gap-3" ] [
                    resourceSelect "Give 4" give.current (fun value -> give.update value)
                    resourceSelect "Receive 1" receive.current (fun value -> receive.update value)
                ]
                button [ ClassName "w-full rounded-lg bg-amber-700 py-2 font-semibold text-white transition hover:bg-amber-800 disabled:cursor-default disabled:opacity-40"
                         OnClick (fun _ -> GameState.tradeBank give.current receive.current)
                         Disabled (not active || sameResource) ] [ str "Trade" ]
                (if sameResource then
                     p [ ClassName "text-xs text-stone-500" ] [ str "Pick two different resources." ]
                 else
                     nothing)
            ]),
        "TradePanel"
    )
