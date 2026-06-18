# ADR-008: Implementación de Pathfinding

**Estado:** Aceptado  
**Fecha:** 2026-06  
**Proyecto:** Simple God

---

## Decisión

Implementar **A* personalizado desde cero** sobre el grid de tiles, con heurística Octile Distance y costos diferenciados para movimiento cardinal y diagonal. No usar NavMesh de Unity.

---

## Contexto

Los agentes (aldeanos, lobos, conejos) necesitan navegar por el Grid para ejecutar sus acciones — ir a buscar comida, huir de depredadores, llegar al altar. El pathfinding debe ser coherente con la estructura de datos del Grid (ADR-001), el sistema de coordenadas cuadradas (ADR-007) y preparado para escalar con Job System en Fase 4.

---

## Alternativas Consideradas

### Opción A — NavMesh de Unity
Sistema de navegación nativo de Unity. Construye una malla de navegación sobre la geometría del mundo.

- ✅ Sin implementación manual, listo para usar
- ✅ Maneja terreno irregular y obstáculos 3D nativamente
- ❌ Filosofía opuesta al Grid discreto — NavMesh navega superficie libre, no tiles
- ❌ Cada cambio dinámico del Grid (tile inundada, árbol talado) requiere recalcular la malla — costoso
- ❌ Pérdida de control sobre qué tiles son navegables según variables del Grid
- ❌ Incompatible con Job System para procesamiento paralelo
- ❌ No demuestra comprensión del algoritmo en una entrevista técnica

### Opción B — A* personalizado sobre el Grid *(elegida)*
Implementación propia del algoritmo A* operando directamente sobre el array de `TileData`.

- ✅ Control total sobre walkability basada en variables del Grid (humedad, fuego, obstáculos)
- ✅ Compatible con la estructura de structs de ADR-001 — sin overhead de conversión
- ✅ Preparado para Job System en Fase 4 sin refactorización
- ✅ Demuestra comprensión profunda del algoritmo
- ✅ Permite extensiones propias: costos por tipo de terreno, zonas de miedo, influence maps
- ⚠️ Mayor tiempo de implementación inicial

---

## Fundamentos del Algoritmo

A* evalúa qué tile explorar a continuación usando:

```
f(n) = g(n) + h(n)

g(n) → costo real acumulado desde el inicio hasta este tile
h(n) → estimación heurística del costo desde este tile hasta el destino
f(n) → costo total estimado — A* siempre expande el tile con menor f(n)
```

El proceso de búsqueda:

```
1. Agregar tile inicial a Open List
2. Mientras Open List no esté vacía:
   a. Tomar el tile con menor f(n)
   b. Si es el destino → reconstruir camino y terminar
   c. Moverlo a Closed List
   d. Por cada vecino válido:
      - Calcular g(n) = g(padre) + costo de movimiento
      - Calcular h(n) = heurística al destino
      - Si no está en Open List → agregar
      - Si ya está con g(n) mayor → actualizar

Open List  → tiles candidatos a explorar (priority queue por f)
Closed List → tiles ya procesados (HashSet para O(1) de búsqueda)
```

---

## Heurística: Octile Distance

Para un grid cuadrado con movimiento cardinal y diagonal, la heurística correcta es Octile Distance. Es **admisible** — nunca sobreestima el costo real, garantizando que A* encuentre siempre el camino óptimo.

```csharp
const float CARDINAL_COST = 1.0f;
const float DIAGONAL_COST = 1.414f; // √2

float Heuristic(Vector2Int a, Vector2Int b)
{
    float dx = Mathf.Abs(a.x - b.x);
    float dy = Mathf.Abs(a.y - b.y);

    // Pasos diagonales posibles = Min(dx, dy)
    // Pasos cardinales restantes = (dx + dy) - 2 * Min(dx, dy)
    // Simplificado:
    return CARDINAL_COST * (dx + dy) + (DIAGONAL_COST - 2 * CARDINAL_COST) * Mathf.Min(dx, dy);
}
```

### Ejemplo: de (0,0) a (3,2)

```
dx = 3, dy = 2

= 1.0 * (3 + 2) + (1.414 - 2.0) * Min(3,2)
= 5.0           + (-0.586)       * 2
= 5.0 - 1.172
= 3.828
```

El camino óptimo real es 2 diagonales + 1 cardinal = `1.414 + 1.414 + 1.0 = 3.828`.
La heurística estimó exactamente el costo real — guía A* directamente hacia el destino sin explorar tiles innecesarios.

### Por qué no Manhattan Distance

```csharp
// Manhattan ignora que las diagonales existen
float Manhattan(Vector2Int a, Vector2Int b) =>
    Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // = 5.0 para el ejemplo anterior
```

Manhattan sobreestima (`5.0` vs `3.828` real). A* descartaría rutas diagonales óptimas y buscaría caminos solo cardinales innecesariamente largos.

---

## Costos de Movimiento

```csharp
float MoveCost(Vector2Int from, Vector2Int to)
{
    bool isDiagonal = (to.x - from.x) != 0 && (to.y - from.y) != 0;
    float baseCost = isDiagonal ? DIAGONAL_COST : CARDINAL_COST;

    // El terreno puede encarecer el movimiento
    TileData tile = _grid[to.y * _width + to.x];
    float terrainMultiplier = GetTerrainCost(tile);

    return baseCost * terrainMultiplier;
}

float GetTerrainCost(TileData tile)
{
    // Ejemplo: agua parcial enlentece, barro también
    if (tile.Humidity > 0.7f) return 2.0f;
    return 1.0f;
}
```

Esto permite que el terreno influya en las rutas — los agentes evitan naturalmente zonas costosas sin lógica adicional.

---

## Estructura de Implementación

```csharp
public class Pathfinder
{
    // Datos por nodo durante la búsqueda
    private struct PathNode
    {
        public Vector2Int Position;
        public float G, H, F;
        public Vector2Int Parent;
    }

    private TileData[] _grid;
    private int _width, _height;

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int destination)
    {
        // Priority queue ordenada por F (min-heap)
        var openList  = new MinHeap<PathNode>();
        var closedSet = new HashSet<Vector2Int>();
        var nodeMap   = new Dictionary<Vector2Int, PathNode>();

        // Inicializar con el tile de inicio
        var startNode = new PathNode {
            Position = start,
            G = 0,
            H = Heuristic(start, destination)
        };
        startNode.F = startNode.G + startNode.H;
        openList.Add(startNode);

        while (openList.Count > 0)
        {
            var current = openList.ExtractMin();

            if (current.Position == destination)
                return ReconstructPath(nodeMap, current);

            closedSet.Add(current.Position);

            foreach (var neighbor in GetNeighbors(current.Position))
            {
                if (closedSet.Contains(neighbor)) continue;
                if (!IsWalkable(neighbor))         continue;

                float g = current.G + MoveCost(current.Position, neighbor);
                float h = Heuristic(neighbor, destination);

                var neighborNode = new PathNode {
                    Position = neighbor,
                    G = g, H = h, F = g + h,
                    Parent = current.Position
                };

                if (!nodeMap.ContainsKey(neighbor) || g < nodeMap[neighbor].G)
                {
                    nodeMap[neighbor] = neighborNode;
                    openList.Add(neighborNode);
                }
            }
        }

        return null; // Sin camino disponible
    }

    private bool IsWalkable(Vector2Int pos)
    {
        TileData tile = _grid[pos.y * _width + pos.x];
        return tile.Humidity < 0.9f   // no anegada
            && !tile.HasObstacle
            && !tile.IsOnFire;
    }
}
```

---

## Extensiones Planificadas

La implementación base se puede extender sin cambiar la arquitectura central:

| Extensión | Fase | Descripción |
|---|---|---|
| Costos por terreno | Fase 2 | Tiles con agua o barro cuestan más |
| Influence Maps | Fase 3 | Zonas de miedo encarecen el movimiento para aldeanos |
| Pathfinding paralelo | Fase 4 | Mover cálculos a Job System con `NativeArray<TileData>` |
| Caché de rutas | Fase 4 | Reutilizar rutas entre agentes con destinos similares |

---

## Consecuencias

- El Pathfinder opera directamente sobre `TileData[]` — sin conversión ni overhead adicional
- `IsWalkable` consulta variables del Grid en tiempo real — reacciona automáticamente a cambios del mundo
- La Open List requiere una priority queue eficiente — se implementa como min-heap binario (C# no tiene una nativa adecuada en las versiones de Unity actuales)
- Los agentes reciben una `List<Vector2Int>` como camino y la siguen tile por tile
- En Fase 4, `NativeArray<TileData>` reemplaza el array managed para compatibilidad con Jobs sin cambiar la lógica del algoritmo

---

## Relevancia para el Portafolio

Implementar A* desde cero sobre un grid de datos propios — en lugar de usar NavMesh — demuestra comprensión de **pathfinding y navegación** a nivel algorítmico. La integración con el sistema de variables del Grid (walkability dinámica, costos por terreno) muestra que el pathfinding forma parte de un sistema cohesivo, no es una feature aislada.
