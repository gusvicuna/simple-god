using System.Collections.Generic;
using UnityEngine;
using SimpleGod.Grid;
using SimpleGod.AI.Pathfinding;

namespace SimpleGod.DevTools
{
    /// <summary>
    /// Herramienta de visualización para el spike de A*. No es código de producción.
    ///
    /// Conecta GridManager + Pathfinder y dibuja en el Scene view:
    ///   - Tiles con obstáculos (cubos negros)
    ///   - Zona húmeda de alto costo (cubos azules semitransparentes)
    ///   - Camino encontrado (línea verde + esferas)
    ///   - Puntos de inicio (azul) y destino (rojo)
    ///
    /// Cómo usar:
    ///   1. Agrega este componente al mismo GameObject que GridManager.
    ///   2. Configura Start y Goal desde el Inspector.
    ///   3. Pulsa Play. El camino se dibuja en el Scene view mientras el juego corre.
    ///   4. Con el juego corriendo: clic derecho en el componente → "Recalcular camino"
    ///      para probar distintos puntos sin salir de Play mode.
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    public class PathfindingDebugger : MonoBehaviour
    {
        [Header("Puntos de prueba")]
        [SerializeField] private Vector2Int _start = new(10, 6);
        [SerializeField] private Vector2Int _goal = new(17, 17);

        [Header("Visualización")]
        [SerializeField] private Color _pathColor = Color.green;
        [SerializeField] private Color _startColor = Color.blue;
        [SerializeField] private Color _goalColor = Color.red;
        [SerializeField] private Color _obstacleColor = Color.black;
        [SerializeField] private Color _waterColor = new Color(0.1f, 0.4f, 0.9f, 0.7f);

        private GridManager _gridManager;
        private Pathfinder _pathfinder;
        private List<Vector2Int> _currentPath;

        // ─── Ciclo de vida ────────────────────────────────────────────────────────

        private void Awake()
        {
            _gridManager = GetComponent<GridManager>();
        }

        private void Start()
        {
            // Pathfinder recibe referencia directa al array — sin copias
            _pathfinder = new Pathfinder(
                _gridManager.GetTilesReadOnly(),
                _gridManager.Width,
                _gridManager.Height
            );
            RecalculatePath();
        }

        // ─── Recalculo ────────────────────────────────────────────────────────────

        /// <summary>
        /// Llama a A* con los puntos actuales y loguea el resultado.
        /// Disponible como ContextMenu para invocar desde el Inspector en Play mode.
        /// </summary>
        [ContextMenu("Recalcular camino")]
        public void RecalculatePath()
        {
            if (_pathfinder == null) return;

            _currentPath = _pathfinder.FindPath(_start, _goal);

            if (_currentPath == null)
                Debug.LogWarning($"[Pathfinding] Sin camino de {_start} a {_goal}. " +
                                  "¿El destino está bloqueado o es inalcanzable?");
            else
                Debug.Log($"[Pathfinding] Camino encontrado: {_currentPath.Count} pasos " +
                          $"de {_start} a {_goal}.");
        }

        // ─── Gizmos ───────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            // Solo dibujar en Play mode; el GridManager no está inicializado antes
            if (!Application.isPlaying) return;
            if (_gridManager == null) return;

            DrawGrid();
            DrawPath();
            DrawStartAndGoal();
        }

        private void DrawGrid()
        {
            for (int y = 0; y < _gridManager.Height; y++)
                for (int x = 0; x < _gridManager.Width; x++)
                {
                    var tile = _gridManager.GetTile(x, y);
                    var worldPos = _gridManager.TileToWorld(x, y);

                    if (tile.HasObstacle)
                    {
                        Gizmos.color = _obstacleColor;
                        Gizmos.DrawCube(worldPos, Vector3.one * 0.85f);
                    }
                    else if (tile.Biome == BiomeType.Water)
                    {
                        Gizmos.color = _waterColor;
                        Gizmos.DrawCube(worldPos, new Vector3(0.85f, 0.05f, 0.85f));
                    }
                }
        }

        private void DrawPath()
        {
            if (_currentPath == null || _currentPath.Count == 0) return;

            const float yOffset = 0.3f;     // elevar ligeramente sobre el suelo

            Gizmos.color = _pathColor;

            // Línea desde start hasta el primer paso
            var prev = _gridManager.TileToWorld(_start) + Vector3.up * yOffset;
            foreach (var pos in _currentPath)
            {
                var world = _gridManager.TileToWorld(pos) + Vector3.up * yOffset;
                Gizmos.DrawLine(prev, world);
                Gizmos.DrawSphere(world, 0.12f);
                prev = world;
            }
        }

        private void DrawStartAndGoal()
        {
            const float yOffset = 0.5f;

            if (_gridManager.IsInBounds(_start))
            {
                Gizmos.color = _startColor;
                Gizmos.DrawSphere(_gridManager.TileToWorld(_start) + Vector3.up * yOffset, 0.28f);
            }

            if (_gridManager.IsInBounds(_goal))
            {
                Gizmos.color = _goalColor;
                Gizmos.DrawSphere(_gridManager.TileToWorld(_goal) + Vector3.up * yOffset, 0.28f);
            }
        }
    }
}
