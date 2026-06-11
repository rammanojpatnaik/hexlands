/// Shared domain types mirroring the backend's JSON contract
/// (snake_case fields on the wire, decoded via Thoth's SnakeCase strategy).
module Hexlands.Types

/// The six Catan terrain types.
type Terrain =
    | Forest    // produces wood
    | Hills     // produces brick
    | Pasture   // produces sheep
    | Fields    // produces wheat
    | Mountains // produces ore
    | Desert    // produces nothing; the robber starts here

module Terrain =
    /// Parse the wire name used by the backend's Terrain enum.
    let parse (name: string) : Terrain =
        match name with
        | "forest" -> Forest
        | "hills" -> Hills
        | "pasture" -> Pasture
        | "fields" -> Fields
        | "mountains" -> Mountains
        | "desert" -> Desert
        | other -> failwithf "Unknown terrain %s" other

    let color (terrain: Terrain) =
        match terrain with
        | Forest -> "#2f7d32"
        | Hills -> "#c8602d"
        | Pasture -> "#8bc34a"
        | Fields -> "#e7c12f"
        | Mountains -> "#90a4ae"
        | Desert -> "#e6d9a8"

type HexCoord = { Q: int; R: int }

/// A hex corner (0-5, clockwise from upper-right); settlements/cities sit here.
type VertexCoord = { Q: int; R: int; Corner: int }

/// A hex side (0-5, side i joins corners i and i+1); roads sit here.
type EdgeCoord = { Q: int; R: int; Edge: int }

type Tile =
    { Q: int
      R: int
      Terrain: string
      Token: int option }

type DiceRoll =
    { Die1: int
      Die2: int
      Total: int }

type Player =
    { Id: int
      Name: string
      Color: string
      Resources: Map<string, int>
      VictoryPoints: int }

type GameState =
    { Id: string
      Phase: string
      Tiles: Tile list
      Players: Player list
      NumPlayers: int
      CurrentPlayer: int
      Robber: HexCoord
      LastRoll: DiceRoll option
      Winner: int option
      Log: string list }

/// Display order of resources in the UI.
let resourceOrder = [ "wood"; "brick"; "sheep"; "wheat"; "ore" ]

let playerColor color =
    match color with
    | "red" -> "#d32f2f"
    | "blue" -> "#1976d2"
    | "orange" -> "#ef6c00"
    | "white" -> "#9e9e9e"
    | _ -> "#616161"
