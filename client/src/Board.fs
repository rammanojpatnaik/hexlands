/// Hexagonal grid component: renders hexes as SVG with terrain color,
/// number token, and robber indicator. Layout math is odd-r offset
/// (see HexGrid.fs); the adapter at the bottom converts the server's
/// axial wire coordinates into the component's model.
module Hexlands.Board

open Fable.React
open Fable.React.Props
open Hexlands.Types
open Hexlands.HexGrid

/// One renderable cell of the grid.
type Hex =
    { Coord: OffsetCoord
      Terrain: Terrain
      Token: int option
      HasRobber: bool }

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

let private hexView (hex: Hex) =
    let cx, cy = toPixel hex.Coord
    let points = corners cx cy |> pointsAttr

    g [ Key (sprintf "%d_%d" hex.Coord.Col hex.Coord.Row) ] [
        polygon [ SVGAttr.Points points
                  SVGAttr.Fill (Terrain.color hex.Terrain)
                  SVGAttr.Stroke "#6d5a3a"
                  SVGAttr.StrokeWidth 2.0 ] []
        (match hex.Token with
         | Some token -> numberToken cx cy token
         | None -> nothing)
        (if hex.HasRobber then robberMarker cx cy else nothing)
    ]

/// The grid component itself: renders any list of hexes as an SVG.
/// Sized for the standard 19-hex board (5 hexes wide, rows 3-4-5-4-3).
let grid (hexes: Hex list) =
    let width = size * sqrt 3.0 * 5.0 + 40.0
    let height = size * 8.0 + 40.0

    let viewBox =
        sprintf "%.1f %.1f %.1f %.1f" (-width / 2.0) (-height / 2.0) width height

    svg [ HTMLAttr.Custom("viewBox", viewBox)
          ClassName "mx-auto h-auto w-full max-w-2xl drop-shadow-sm" ] (hexes |> List.map hexView)

/// Adapter: server game state (axial coords, terrain names) -> grid model.
let view (game: GameState) =
    game.Tiles
    |> List.map (fun tile ->
        { Coord = fromAxial tile.Q tile.R
          Terrain = Terrain.parse tile.Terrain
          Token = tile.Token
          HasRobber = game.Robber.Q = tile.Q && game.Robber.R = tile.R })
    |> grid
