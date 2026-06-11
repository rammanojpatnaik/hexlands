/// Hex-grid geometry in odd-r offset coordinates: pointy-top hexes
/// arranged in rows, with odd rows shoved right by half a hex.
module Hexlands.HexGrid

open System

/// Odd-r offset coordinate. Row 0 is the middle of the board; columns
/// in odd rows sit half a hex to the right of the same column in even rows.
type OffsetCoord = { Col: int; Row: int }

/// Distance from a hex center to each corner, in SVG units.
let size = 56.0

/// True for rows that are shifted right (odd rows, negatives included).
let private isShifted row = abs (row % 2) = 1

/// Convert the server's axial (q, r) wire coordinates to odd-r offset.
let fromAxial (q: int) (r: int) : OffsetCoord =
    let parity = if isShifted r then 1 else 0
    { Col = q + (r - parity) / 2; Row = r }

/// Center of a hex in SVG pixel space.
let toPixel (coord: OffsetCoord) =
    let shift = if isShifted coord.Row then 0.5 else 0.0
    let x = size * sqrt 3.0 * (float coord.Col + shift)
    let y = size * 1.5 * float coord.Row
    x, y

/// The six corner points of a pointy-top hex centered at (cx, cy).
let corners (cx: float) (cy: float) =
    [ for i in 0 .. 5 ->
        let angle = Math.PI / 180.0 * (60.0 * float i - 30.0)
        cx + size * cos angle, cy + size * sin angle ]

/// Corner list formatted for an SVG polygon's `points` attribute.
let pointsAttr (points: (float * float) list) =
    points
    |> List.map (fun (x, y) -> sprintf "%.1f,%.1f" x y)
    |> String.concat " "

/// The standard Catan board: 19 hexes in rows of 3, 4, 5, 4, 3,
/// centered on the origin. (The server sends the same layout; this is
/// here so the grid component can also render standalone.)
let standardLayout: OffsetCoord list =
    [ for row in -2 .. 2 do
        let count = 5 - abs row
        let first = if isShifted row then -2 else -(count / 2)
        for col in first .. first + count - 1 -> { Col = col; Row = row } ]
