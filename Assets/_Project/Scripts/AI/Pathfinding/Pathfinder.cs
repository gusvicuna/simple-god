using System.Collections.Generic;
using UnityEngine;
using SimpleGod.Grid;

namespace SimpleGod.AI.Pathfinding
{
    /// <summary>
    /// Implementación de A* sobre TileData[]. Clase pura — sin dependencias de MonoBehaviour.
    ///
    /// Diseño:
    ///   - Opera directamente sobre el array del GridManager (sin conversiones ni copias)
    ///   - Heurística: Octile Distance — admisible para grids cuadrados con diagonales (ADR-008)
    ///   - Costos diferenciados: cardinal 1.0, diagonal √2, terreno húmedo ×2 (ADR-007, ADR-008)
    ///   - En Fase 4, la lógica migra a un IJob con NativeArray&lt;TileData&gt; sin cambiar el algoritmo
    /// </summary>
    public class Pathfinder
    {
        // ─── Costos de movimiento (ADR-007) ───────────────────────────────────────

        private const float CardinalCost = 1.0f;
        private const float DiagonalCost = 1.4142f;    // √2

        // ─── Referencia al grid ───────────────────────────────────────────────────

        private readonly TileData[] _grid;
        private readonly int _width;
        private readonly int _height;

        public Pathfinder(TileData[] grid, int width, int height)
        {
            _grid = grid;
            _width = width;
            _height = height;
        }

        // ─── Nodo de búsqueda (interno) ───────────────────────────────────────────

        /// <summary>
        /// Datos por nodo durante la búsqueda. Struct para evitar heap allocations en hot-path.
        /// IComparable&lt;PathNode&gt; permite usarlo directamente en el MinHeap&lt;T&gt;.
        /// </summary>
        private struct PathNode : System.IComparable<PathNode>
        {
            public Vector2Int Position;
            public float G;           // costo acumulado real desde el origen
            public float F;           // G + H (criterio de ordenación del heap)
            public Vector2Int Parent;      // para reconstruir el camino al final

            public int CompareTo(PathNode other) => F.CompareTo(other.F);
        }

        // ─── API pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Encuentra el camino óptimo de start a goal usando A*.
        ///
        /// Retorna la secuencia de posiciones desde start (exclusivo) hasta goal (inclusivo),
        /// lista para que un agente la siga tile a tile. Retorna null si no existe camino.
        /// </summary>
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            if (!IsInBounds(start) || !IsInBounds(goal)) return null;
            if (!IsWalkable(goal)) return null;
            if (start == goal) return new List<Vector2Int>();

            var openHeap = new MinHeap<PathNode>();
            var closedSet = new HashSet<Vector2Int>();
            // Mejor G conocido por posición: descarta re-inserciones con costo mayor
            var bestG = new Dictionary<Vector2Int, float>();
            // Para reconstruir el camino al terminar
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();

            openHeap.Add(new PathNode
            {
                Position = start,
                G = 0f,
                F = Heuristic(start, goal),
                Parent = start,
            });
            bestG[start] = 0f;

            while (!openHeap.IsEmpty)
            {
                var current = openHeap.ExtractMin();

                // El heap puede tener entradas duplicadas con G más alto — ignorarlas
                if (closedSet.Contains(current.Position)) continue;
                closedSet.Add(current.Position);

                if (current.Position == goal)
                    return ReconstructPath(cameFrom, start, goal);

                foreach (var neighborPos in GetNeighborPositions(current.Position))
                {
                    if (closedSet.Contains(neighborPos)) continue;
                    if (!IsWalkable(neighborPos)) continue;

                    float g = current.G + MoveCost(current.Position, neighborPos);

                    // Solo procesar si encontramos un camino más corto al vecino
                    if (bestG.TryGetValue(neighborPos, out float knownG) && g >= knownG)
                        continue;

                    bestG[neighborPos] = g;
                    cameFrom[neighborPos] = current.Position;

                    openHeap.Add(new PathNode
                    {
                        Position = neighborPos,
                        G = g,
                        F = g + Heuristic(neighborPos, goal),
                        Parent = current.Position,
                    });
                }
            }

            return null;    // Sin camino disponible
        }

        // ─── Heurística ───────────────────────────────────────────────────────────

        /// <summary>
        /// Octile Distance: heurística admisible para grids cuadrados con movimiento
        /// cardinal y diagonal. Nunca sobreestima → A* garantiza el camino óptimo (ADR-008).
        ///
        /// Para (0,0)→(3,2): dx=3, dy=2 → 3.828 (igual al costo real: 2 diagonales + 1 cardinal)
        /// </summary>
        private static float Heuristic(Vector2Int a, Vector2Int b)
        {
            float dx = Mathf.Abs(a.x - b.x);
            float dy = Mathf.Abs(a.y - b.y);
            // Pasos diagonales = Min(dx, dy); pasos cardinales restantes = |dx - dy|
            return CardinalCost * (dx + dy) + (DiagonalCost - 2f * CardinalCost) * Mathf.Min(dx, dy);
        }

        // ─── Costo de movimiento ──────────────────────────────────────────────────

        private float MoveCost(Vector2Int from, Vector2Int to)
        {
            bool isDiagonal = (to.x - from.x) != 0 && (to.y - from.y) != 0;
            return isDiagonal ? DiagonalCost : CardinalCost;

            // Post-MVP: aquí se añadirán multiplicadores por tipo de terreno
            // (barro, pendiente, zona de miedo via Influence Maps).
        }

        // ─── Walkability ──────────────────────────────────────────────────────────

        /// <summary>
        /// Consulta el estado actual del grid en tiempo real.
        /// Reacciona automáticamente a cambios del mundo sin que el Pathfinder necesite
        /// ser notificado — la fuente de verdad es el array (ADR-008).
        ///
        /// MVP: bloqueado por Obstacle o BiomeType.Water.
        /// Post-MVP: este método se vuelve un predicado por tipo de agente —
        /// un aldeano no puede cruzar agua, pero un agente acuático sí podría.
        /// </summary>
        private bool IsWalkable(Vector2Int pos)
        {
            var tile = _grid[pos.y * _width + pos.x];
            return !tile.HasObstacle
                && tile.Biome != BiomeType.Water;
        }

        // ─── Vecinos ──────────────────────────────────────────────────────────────

        private static readonly Vector2Int[] NeighborDirs =
        {
            new( 1,  0), new(-1,  0), new( 0,  1), new( 0, -1),
            new( 1,  1), new( 1, -1), new(-1,  1), new(-1, -1),
        };

        // Uso de yield return para evitar la allocación del List en cada llamada
        private IEnumerable<Vector2Int> GetNeighborPositions(Vector2Int pos)
        {
            foreach (var dir in NeighborDirs)
            {
                var n = pos + dir;
                if (IsInBounds(n)) yield return n;
            }
        }

        // ─── Utilidades ───────────────────────────────────────────────────────────

        private bool IsInBounds(Vector2Int pos) =>
            pos.x >= 0 && pos.x < _width && pos.y >= 0 && pos.y < _height;

        /// <summary>Reconstruye el camino hacia atrás desde goal usando el mapa cameFrom.</summary>
        private static List<Vector2Int> ReconstructPath(
            Dictionary<Vector2Int, Vector2Int> cameFrom,
            Vector2Int start,
            Vector2Int goal)
        {
            var path = new List<Vector2Int>();
            var current = goal;

            while (current != start)
            {
                path.Add(current);
                current = cameFrom[current];
            }

            path.Reverse();     // cameFrom construye el camino al revés
            return path;
        }
    }
}
