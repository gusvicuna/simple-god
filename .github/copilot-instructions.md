# Copilot Instructions — Simple God

God game / systemic simulation in Unity 6 + C#. Portfolio project targeting AI Engineer roles (Mobius Digital Games, 2026). Villagers navigate a tile-based world using custom Behavior Trees, Utility AI, and A\* pathfinding. The player influences the world through divine powers (Rain, Lightning).

---

## MVP Scope — do not exceed without flagging it

| Element       | Detail                                                              |
| ------------- | ------------------------------------------------------------------- |
| Grid          | 20×20 tiles                                                         |
| Agents        | 5 Villagers (Rabbits and Wolves are Phase 4 — do not implement yet) |
| Needs         | Hunger · Fear · Faith                                               |
| Divine Powers | Rain · Lightning                                                    |
| Perspective   | Isometric 3D over a discrete 2D grid                                |

If a suggestion would add systems, agent types, or mechanics outside this scope, say so before generating code.

---

## Architecture — 4 Layers

```
Layer 0  Player Input  →  clicks on grid → PowerSystem
Layer 1  GameLoop      →  tick coordinator, controls system execution order
Layer 2  World Systems →  DayNightCycle · GridManager · FloraSystem · AIManager
Layer 3  Agent System  →  AgentBrain → UtilityAI → BehaviorTree → Pathfinder
Layer 4  Data (DOD)    →  TileData[] · AgentState[] · PathNode heap
```

- `GameLoop` orchestrates tick order. Systems do not call each other directly.
- `GridManager` is the single source of truth for world state. Nothing stores a copy of tile data — always read from the grid.
- `AIManager` owns scheduling and time-slicing for all agents. It calls `AgentBrain.OnStateTick()`, not agents calling themselves.

---

## Architectural Decisions (ADR-001 to ADR-010) — closed, do not reopen

### ADR-001 — Grid Memory Representation

- Grid is a **flat `TileData[]` array** with index `y * width + x`.
- `TileData` is a **struct** (not a class). Never make it a class.
- Dynamic states use `TileFlags` bitmask (`byte`). Permanent biome states use `BiomeType` enum.
- To modify a tile: call `GridManager.SetTile()` or `SetFlag()`. Never modify a local copy and expect it to persist — C# copies structs by value.

```csharp
// CORRECT
gridManager.SetFlag(pos, TileFlags.Obstacle, true);

// WRONG — modifies a local copy, original array is unchanged
var tile = gridManager.GetTile(pos);
tile.Flags |= TileFlags.Obstacle;
```

### ADR-002 — Agent–Grid Communication

Two patterns — use the right one:

- **Push (event):** `GridManager.OnTileChanged` static event. Systems that react to tile changes subscribe. (`FloraSystem`, `WeatherSystem`)
- **Pull (spatial query):** Systems query tiles when they need to decide. (`AIManager`, `Pathfinder`, `Debug UI`)

Never poll the grid every frame from an agent. Query when making a decision.

### ADR-003 — Agent Decision Architecture

Three responsibilities, three classes:

- `AIManager` — scheduling: controls when and in what order agents evaluate
- `UtilityAI` — scoring: picks the best action given context and personality
- `AgentBrain` — per-agent state: holds personality parameters and local state copy

### ADR-004 — Behavior Trees

- BTs are implemented **from scratch in C#**. Do not use Unity Behavior Package or any third-party BT asset.
- Variant: **BT with Memory + Selective Interruption**. Nodes remember their state across frames (`_runningIndex`). High-priority interrupts (Fear > 0.8 or Hunger > 0.95) can abort the running subtree via `BT.Abort()`.
- BT is the **execution layer** — it translates the UtilityAI decision into multi-frame steps. It does not decide _what_ to do, only _how_ to do it.

### ADR-005 — Engine

Unity 6 (LTS). C# only. No external scripting languages.

### ADR-006 — Perspective and Coordinate Space

- Isometric 3D over a discrete 2D logical grid.
- `Altitude` is a field in `TileData` (terrain height per tile), not a dimension of the grid system.
- World position: `new Vector3(x, tile.Altitude, y)` — X and Z map to grid columns/rows, Y maps to altitude.

### ADR-007 — Grid Coordinate System

- Square grid, **8-directional movement** (cardinal + diagonal).
- Cardinal cost: `1.0f`. Diagonal cost: `√2 ≈ 1.4142f`.
- Neighbor direction order is fixed across the codebase for reproducibility:
    ```
    (1,0) (-1,0) (0,1) (0,-1) (1,1) (1,-1) (-1,1) (-1,-1)
    ```

### ADR-008 — Pathfinding

- A\* implemented from scratch. **Do not use Unity NavMesh** — it is incompatible with the discrete tile grid.
- Heuristic: **Octile Distance** — admissible for 8-directional square grids.
- Priority queue: custom `MinHeap<T>` (PriorityQueue<T,P> not available on all Unity/Mono targets).
- `Pathfinder` is a **pure C# class**, no `MonoBehaviour`. It receives `TileData[]` by reference.
- Walkability is checked at query time against the live grid array — the pathfinder does not cache walkability.
- Phase 4: migrate pathfinding to Unity Job System + `NativeArray<TileData>` without changing the algorithm.

### ADR-009 — Game Loop

- Fixed-tick game loop managed by `GameLoop`. Not Unity's per-frame `Update()` for simulation logic.
- Tick rate is configurable. `AIManager` uses time-slicing across ticks to stay within frame budget.

### ADR-010 — Agent State Persistence

- Agent state lives in `AgentState[]` (flat array of structs), owned by `AIManager`.
- Each agent has an `AgentState` struct with Hunger, Fear, Faith, Position, CurrentAction.
- `AgentBrain` receives a copy of `AgentState`, updates it locally, and the manager writes it back.

---

## Code Conventions

### Data vs Logic split

- **Data:** C# `struct`. Fields only. Convenience properties that mask flags are allowed.
- **Logic:** C# `class`. Reads data structs, executes behavior, never owns persistent state.

```csharp
// Data — struct
public struct AgentState
{
    public float Hunger;
    public float Fear;
    public float Faith;
    public Vector2Int Position;
}

// Logic — class
public class UtilityAI
{
    public ActionType Evaluate(AgentState state, GridContext context) { ... }
}
```

### Namespaces

Follow the existing structure:

```
SimpleGod.Grid          → GridManager, TileData, BiomeType, TileFlags
SimpleGod.AI.Pathfinding → Pathfinder, MinHeap
SimpleGod.AI            → AIManager, UtilityAI, AgentBrain, AgentState
SimpleGod.AI.BehaviorTree → BT nodes and runner
SimpleGod.Agents        → VillagerAgent and future agent types
SimpleGod.Powers        → PowerSystem, Rain, Lightning
SimpleGod.Ecosystem     → FloraSystem, WeatherSystem, DayNightCycle
SimpleGod.DevTools      → Debug visualizers, not production code
SimpleGod.UI            → HUD, god panel
SimpleGod.Core          → GameLoop, shared interfaces
```

### Comments

- Comment the **intention** behind non-trivial decisions, not what the code obviously does.
- Reference the relevant ADR when a choice is constrained by it: `// ADR-008`.
- Mark Phase 4 migration points explicitly:
    ```csharp
    // Phase 4: migrate to NativeArray<TileData> + IJob for Burst compatibility (ADR-008)
    ```
- DevTools code gets a header comment clarifying it is not production code.

### No GC allocations in hot paths

- Prefer `struct` over `class` for per-frame data.
- Use `yield return` or pre-allocated buffers instead of `new List<>()` inside tight loops.
- `GetNeighborPositions` in `Pathfinder` uses `yield return` for this reason.

### MonoBehaviour usage

- `MonoBehaviour` only for scene-facing entry points: `GridManager`, `AIManager`, `GameLoop`, `PathfindingDebugger`.
- Core logic classes (`Pathfinder`, `UtilityAI`, `AgentBrain`, BT nodes) are plain C# — no `MonoBehaviour`.

---

## File Structure

```
Assets/_Project/Scripts/
├── AI/
│   ├── Pathfinding/        MinHeap.cs · Pathfinder.cs
│   └── BehaviorTree/       (nodes, runner — Phase 2)
├── Agents/                 VillagerAgent.cs (Phase 2)
├── Core/                   GameLoop.cs · interfaces
├── DevTools/               PathfindingDebugger.cs · debug visualizers
├── Ecosystem/              FloraSystem · WeatherSystem · DayNightCycle
├── Grid/                   GridManager.cs · TileData.cs
├── Powers/                 PowerSystem · Rain · Lightning
└── UI/                     HUD panels
```

New files always go into the matching namespace folder. No catch-all folders.

---

## Common Patterns

### Reading a tile

```csharp
TileData tile = gridManager.GetTile(pos);
if (tile.HasObstacle) { ... }
```

### Modifying a tile

```csharp
// Modify a single flag
gridManager.SetFlag(pos, TileFlags.Obstacle, true);

// Replace the full tile
var tile = gridManager.GetTile(pos);
tile.Humidity = 1.0f;
gridManager.SetTile(pos, tile);
```

### Pathfinding

```csharp
var pathfinder = new Pathfinder(gridManager.GetTilesReadOnly(), gridManager.Width, gridManager.Height);
List<Vector2Int> path = pathfinder.FindPath(start, goal);
// null  → no path exists
// empty → already at goal
```

### Subscribing to tile changes (Push pattern)

```csharp
private void OnEnable()  => GridManager.OnTileChanged += HandleTileChanged;
private void OnDisable() => GridManager.OnTileChanged -= HandleTileChanged;

private void HandleTileChanged(Vector2Int pos, TileData tile) { ... }
```

### Adding a new BT node (once BT system is scaffolded)

- Inherit from `BTNode` base class.
- Return `BTResult.Running`, `BTResult.Success`, or `BTResult.Failure`.
- Store resume state in a field — never assume the node starts fresh each tick.

---

## What NOT to generate

- NavMesh, `NavMeshAgent`, or any Unity navigation API.
- Unity Behavior Package or any third-party BT/GOAP/FSM assets.
- `class TileData` — it must remain a struct.
- Agent types beyond Villager (Rabbit, Wolf) for MVP.
- Mechanics beyond the three needs (Hunger, Fear, Faith) and two powers (Rain, Lightning) for MVP.
- `Update()`-based agent decision loops — all agent evaluation goes through `AIManager`.
