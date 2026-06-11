/// Pointy-top hex geometry: axial (q, r) coordinates to SVG pixel space.
module Hexlands.HexGrid

open System

/// Distance from a hex center to each corner, in SVG units.
let size = 56.0

/// Center of the hex at axial coordinate (q, r).
let toPixel (q: int) (r: int) =
    let x = size * (sqrt 3.0 * float q + sqrt 3.0 / 2.0 * float r)
    let y = size * 1.5 * float r
    x, y

/// The six corner points of a hex centered at (cx, cy).
let corners (cx: float) (cy: float) =
    [ for i in 0 .. 5 ->
        let angle = Math.PI / 180.0 * (60.0 * float i - 30.0)
        cx + size * cos angle, cy + size * sin angle ]

/// Corner list formatted for an SVG polygon's `points` attribute.
let pointsAttr (points: (float * float) list) =
    points
    |> List.map (fun (x, y) -> sprintf "%.1f,%.1f" x y)
    |> String.concat " "
