using System.Collections.Generic;
using UnityEngine;

namespace SimpleGod.Grid
{
    /// <summary>
    /// Fuente de verdad del mundo. Posee el array plano de TileData y expone acceso
    /// controlado a tiles, vecinos y conversiones coordenada↔mundo (ADR-001, ADR-007).
    ///
    /// Separación de responsabilidades:
    ///   GridManager  → propietario de los datos y su ciclo de vida
    ///   Pathfinder   → consume los datos para navegar (recibe referencia al array)
    ///   Powers/Rules → modifican tiles via SetTile / SetFlag
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("Dimensiones del Grid")]
        [SerializeField] private int _width = 20;
        [SerializeField] private int _height = 20;

        // Array plano de structs: contiguo en memoria, compatible con Job System (ADR-001)
        private TileData[] _tiles;

        public int Width => _width;
        public int Height => _height;

        // Direcciones de vecinos — orden fijo para reproducibilidad entre plataformas (ADR-007)
        private static readonly Vector2Int[] CardinalDirs =
        {
            new( 1,  0), new(-1,  0),
            new( 0,  1), new( 0, -1),
        };

        private static readonly Vector2Int[] AllDirs =
        {
            new( 1,  0), new(-1,  0), new( 0,  1), new( 0, -1),   // cardinales
            new( 1,  1), new( 1, -1), new(-1,  1), new(-1, -1),   // diagonales
        };

        // ─── Ciclo de vida ────────────────────────────────────────────────────────

        private void Awake()
        {
            _tiles = new TileData[_width * _height];
            GenerateSyntheticGrid();
        }

        // ─── Acceso a tiles ───────────────────────────────────────────────────────

        public bool IsInBounds(int x, int y) =>
            x >= 0 && x < _width && y >= 0 && y < _height;

        public bool IsInBounds(Vector2Int pos) => IsInBounds(pos.x, pos.y);

        /// <summary>Lee el TileData en (x, y). Verifica bounds antes de llamar.</summary>
        public TileData GetTile(int x, int y) => _tiles[y * _width + x];
        public TileData GetTile(Vector2Int pos) => GetTile(pos.x, pos.y);

        /// <summary>
        /// Escribe un tile completo. Los structs se copian por valor en C# —
        /// modificar el resultado de GetTile() no afecta el array; siempre usar SetTile (ADR-001).
        /// </summary>
        public void SetTile(int x, int y, TileData data) => _tiles[y * _width + x] = data;
        public void SetTile(Vector2Int pos, TileData data) => SetTile(pos.x, pos.y, data);

        /// <summary>Activa o desactiva un flag sin copiar el struct completo.</summary>
        public void SetFlag(int x, int y, TileFlags flag, bool active)
        {
            int idx = y * _width + x;
            if (active) _tiles[idx].Flags |= flag;
            else _tiles[idx].Flags &= ~flag;
        }
        public void SetFlag(Vector2Int pos, TileFlags flag, bool active) =>
            SetFlag(pos.x, pos.y, flag, active);

        // ─── Vecinos ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Retorna los vecinos válidos (dentro de bounds) de pos.
        /// En Fase 4 esto migrará a NativeArray + Jobs para evitar la allocación del List.
        /// </summary>
        public List<Vector2Int> GetNeighbors(Vector2Int pos, bool includeDiagonals = true)
        {
            var dirs = includeDiagonals ? AllDirs : CardinalDirs;
            var result = new List<Vector2Int>(includeDiagonals ? 8 : 4);
            foreach (var dir in dirs)
            {
                var neighbor = pos + dir;
                if (IsInBounds(neighbor)) result.Add(neighbor);
            }
            return result;
        }

        // ─── Coordenadas mundo ↔ grid ─────────────────────────────────────────────

        /// <summary>
        /// Posición 3D de Unity para el centro de un tile.
        /// Altitude es dato del tile, no dimensión del grid (ADR-006, ADR-007).
        /// </summary>
        public Vector3 TileToWorld(int x, int y)
        {
            float alt = _tiles[y * _width + x].Altitude;
            return new Vector3(x, alt, y);
        }
        public Vector3 TileToWorld(Vector2Int pos) => TileToWorld(pos.x, pos.y);

        /// <summary>Tile más cercano a una posición de mundo.</summary>
        public Vector2Int WorldToTile(Vector3 worldPos) =>
            new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.z));

        // ─── Acceso al array para sistemas de solo lectura ────────────────────────

        /// <summary>
        /// Expone el array de tiles para Pathfinder y futuros Jobs.
        /// Contrato: el caller NO modifica el array directamente — usa SetTile/SetFlag.
        /// En Fase 4, este método retornará NativeArray&lt;TileData&gt; para compatibilidad con Burst.
        /// </summary>
        public TileData[] GetTilesReadOnly() => _tiles;

        // ─── Generación sintética para el spike de A* ─────────────────────────────

        /// <summary>
        /// Crea un grid de prueba con un obstáculo en forma de U y un río de agua (BiomeType.Water).
        /// Fuerza al pathfinder a demostrar evasión de obstáculos sólidos y de tiles de agua.
        /// Reemplazar por generación procedural en Fase 2.
        /// </summary>
        private void GenerateSyntheticGrid()
        {
            // Base: llanura uniforme
            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                {
                    _tiles[y * _width + x] = new TileData
                    {
                        Humidity = 0.3f,
                        Fertility = 0.5f,
                        Temperature = 0.5f,
                        Altitude = 0f,
                        Biome = BiomeType.Plains,
                        Flags = TileFlags.None,
                    };
                }

            // Obstáculo en forma de U (apertura arriba): fuerza rutas no triviales
            for (int y = 4; y <= 11; y++) SetFlag(6, y, TileFlags.Obstacle, true);  // brazo izquierdo
            for (int y = 4; y <= 11; y++) SetFlag(13, y, TileFlags.Obstacle, true);  // brazo derecho
            for (int x = 6; x <= 13; x++) SetFlag(x, 4, TileFlags.Obstacle, true);  // fondo

            // Río vertical: demuestra que el pathfinder evita agua igual que obstáculos sólidos.
            // Agentes que puedan cruzar agua (post-MVP) recibirán un predicado IsWalkable distinto.
            // Gap en y=9..10: permite al pathfinder cruzar el río y demostrar la evasión parcial.
            for (int y = 0; y < _height; y++)
            {
                if (y == 9 || y == 10) continue;    // hueco transitable en el centro del río
                int idx = y * _width + 16;
                _tiles[idx].Biome = BiomeType.Water;
                _tiles[idx].Humidity = 1.0f;    // saturación máxima por coherencia visual
            }
        }
    }
}
