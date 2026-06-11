/// Shared domain types mirroring the backend's JSON contract
/// (snake_case fields on the wire, decoded via Thoth's SnakeCase strategy).
module Hexlands.Types

type HexCoord = { Q: int; R: int }

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
      CurrentPlayer: int
      Robber: HexCoord
      LastRoll: DiceRoll option
      Winner: int option
      Log: string list }

/// Display order of resources in the UI.
let resourceOrder = [ "wood"; "brick"; "sheep"; "wheat"; "ore" ]

let terrainColor terrain =
    match terrain with
    | "hills" -> "#c8602d"
    | "forest" -> "#2f7d32"
    | "pasture" -> "#8bc34a"
    | "fields" -> "#e7c12f"
    | "mountains" -> "#90a4ae"
    | "desert" -> "#e6d9a8"
    | _ -> "#cccccc"

let playerColor color =
    match color with
    | "red" -> "#d32f2f"
    | "blue" -> "#1976d2"
    | "orange" -> "#ef6c00"
    | "white" -> "#9e9e9e"
    | _ -> "#616161"
