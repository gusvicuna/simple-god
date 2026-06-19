using System;

namespace SimpleGod.Grid
{
    /// <summary>
    /// Estado ambiental de un tile. Struct para cache-friendliness (ADR-001).
    /// Datos puros: la lógica derivada (walkability, pathfinding) vive en GridManager y Pathfinder.
    /// </summary>
    public struct TileData
    {
        public float Humidity;      // 0..1: sequía → saturación; ≥0.9 bloquea el paso
        public float Fertility;     // 0..1: capacidad de cultivo
        public float Temperature;   // 0..1: frío → calor
        public float Altitude;      // posición Y del tile en el mundo 3D (ADR-006)
        public BiomeType Biome;
        public TileFlags Flags;         // estados dinámicos del tile (bitmask de 1 byte, ADR-001)

        // Propiedad de conveniencia — solo enmascara Flags, sin lógica adicional
        public readonly bool HasObstacle => (Flags & TileFlags.Obstacle) != 0;

        // Nota: el agua se representa con BiomeType.Water, no con un flag.
        // La distinción es intencional: Water es un bioma permanente; Flooded (post-MVP)
        // será un estado dinámico que algunos agentes podrán atravesar.
    }

    // ─── Tipos de bioma ───────────────────────────────────────────────────────────

    public enum BiomeType : byte
    {
        None = 0,
        Plains = 1,
        Forest = 2,
        Desert = 3,
        Water = 4,    // cuerpo de agua permanente (río, lago) — distinto de Flooded
    }

    // ─── Flags de estado dinámico ─────────────────────────────────────────────────

    /// <summary>
    /// Condiciones dinámicas que modifican o bloquean el paso a través de un tile.
    /// Bitmask en un byte: compacto en memoria y extensible sin cambiar el tamaño del struct (ADR-001).
    ///
    /// MVP: solo Obstacle. El agua permanente se representa con BiomeType.Water.
    /// </summary>
    [Flags]
    public enum TileFlags : byte
    {
        None = 0,
        Obstacle = 1 << 0,  // árbol, roca, construcción — bloquea el paso
    }
}
