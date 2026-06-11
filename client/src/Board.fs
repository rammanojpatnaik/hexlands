/// SVG rendering of the hex board: terrain tiles, number tokens, robber.
module Hexlands.Board

open Fable.React
open Fable.React.Props
open Hexlands.Types
open Hexlands.HexGrid

let private numberToken (cx: float) (cy: float) (token: int) =
    // 6 and 8 are the most likely rolls; Catan prints them in red.
    let highlight = token = 6 || token = 8

    g [] [
        circle [ SVGAttr.Cx cx
                 SVGAttr.Cy cy
                 SVGAttr.R 16.0
                 SVGAttr.Fill "#f7f1dd"
                 SVGAttr.Stroke "#6d5a3a"
                 SVGAttr.StrokeWidth 1.5 ] []
        text [ SVGAttr.X cx
               SVGAttr.Y (cy + 5.0)
               SVGAttr.TextAnchor "middle"
               SVGAttr.FontSize 15.0
               SVGAttr.Fill (if highlight then "#c0392b" else "#3a3a3a") ] [
            str (string token)
        ]
    ]

let private robberMarker (cx: float) (cy: float) =
    g [] [
        circle [ SVGAttr.Cx cx
                 SVGAttr.Cy (cy - 24.0)
                 SVGAttr.R 10.0
                 SVGAttr.Fill "#2f2f2f"
                 SVGAttr.Stroke "#111111"
                 SVGAttr.StrokeWidth 1.0 ] []
        text [ SVGAttr.X cx
               SVGAttr.Y (cy - 20.5)
               SVGAttr.TextAnchor "middle"
               SVGAttr.FontSize 9.0
               SVGAttr.Fill "#ffffff" ] [ str "R" ]
    ]

let private tileView (robber: HexCoord) (tile: Tile) =
    let cx, cy = toPixel tile.Q tile.R
    let points = corners cx cy |> pointsAttr

    g [ Key (sprintf "%d_%d" tile.Q tile.R) ] [
        polygon [ SVGAttr.Points points
                  SVGAttr.Fill (terrainColor tile.Terrain)
                  SVGAttr.Stroke "#6d5a3a"
                  SVGAttr.StrokeWidth 2.0 ] []
        (match tile.Token with
         | Some token -> numberToken cx cy token
         | None -> nothing)
        (if robber = { Q = tile.Q; R = tile.R } then robberMarker cx cy else nothing)
    ]

let view (game: GameState) =
    // The board is 5 hexes wide and ~8 half-rows tall; pad with a margin.
    let width = size * sqrt 3.0 * 5.0 + 40.0
    let height = size * 8.0 + 40.0

    let viewBox =
        sprintf "%.1f %.1f %.1f %.1f" (-width / 2.0) (-height / 2.0) width height

    svg [ HTMLAttr.Custom("viewBox", viewBox)
          ClassName "board" ] (game.Tiles |> List.map (tileView game.Robber))
