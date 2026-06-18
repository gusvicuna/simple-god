# ADR-001: Representación del Grid en Memoria

**Estado:** Aceptado  
**Fecha:** 2026-06  
**Proyecto:** Simple God

---

## Decisión

Representar el Grid como un **array plano de structs** (`TileData[]`) con índice calculado manualmente (`y * width + x`).

```csharp
[System.Flags]
public enum TileFlags : byte
{
    None     = 0,
    Obstacle = 1 << 0,  // árbol, roca, construcción
    OnFire   = 1 << 1,  // tile en llamas
    Flooded  = 1 << 2,  // cubierta de agua dinámica (mecánica de fluidos futura)
    // Reservado: Sacred = 1 << 3, Cursed = 1 << 4 ...
}

public struct TileData
{
    public float     Humidity;      // 0..1: sequía → saturación
    public float     Fertility;     // 0..1: capacidad de cultivo
    public float     Temperature;   // 0..1: frío → calor
    public float     Altitude;      // posición Y del tile en el mundo 3D
    public BiomeType Biome;
    public TileFlags Flags;         // estados dinámicos del tile (bitmask, 1 byte)

    // Propiedades de conveniencia — solo enmascaran el campo Flags
    public readonly bool HasObstacle => (Flags & TileFlags.Obstacle) != 0;
    public readonly bool IsOnFire    => (Flags & TileFlags.OnFire)   != 0;
    public readonly bool IsFlooded   => (Flags & TileFlags.Flooded)  != 0;
}

private TileData[] _grid = new TileData[width * height];
```

---

## Contexto

El Grid Manager es la columna vertebral del sistema. Es leído cada tick por todos los agentes (aldeanos, lobos, conejos) para tomar decisiones de Utility AI, y modificado por los poderes divinos del jugador y el ciclo día/noche.

Con 500 agentes como objetivo de Fase 4, el acceso al Grid necesita ser eficiente en memoria y compatible con el Job System de Unity.

---

## Alternativas Consideradas

### Opción A — Array 2D de clases (`Tile[,]`)
```csharp
Tile[,] grid = new Tile[20, 20]; // Tile es una clase
```
- ✅ Sintaxis intuitiva, fácil de leer
- ❌ Los objetos `Tile` viven en el heap de forma dispersa
- ❌ Cada acceso puede provocar un cache miss
- ❌ Incompatible con Job System (no se puede pasar managed objects a Jobs)

### Opción B — Arrays paralelos de primitivos
```csharp
float[] humidity    = new float[400];
float[] fertility   = new float[400];
float[] temperature = new float[400];
float[] altitude    = new float[400];
```
- ✅ Máxima eficiencia de cache por variable
- ✅ Forma más pura de Data-Oriented Design
- ✅ Ideal para operaciones vectorizadas con Burst Compiler
- ❌ Coordinar 4+ arrays para leer una tile es verboso y propenso a errores
- ❌ Legibilidad reducida, especialmente para un portafolio técnico

### Opción C — Array plano de structs *(elegida)*
```csharp
TileData[] grid = new TileData[400]; // TileData es un struct
```
- ✅ Datos contiguos en memoria (cache-friendly)
- ✅ Compatible con Job System y Burst Compiler
- ✅ Legible: todos los datos de una tile están agrupados
- ✅ Puerta abierta a migrar a Opción B si el profiler lo exige
- ⚠️ Los structs se copian por valor — modificar requiere escribir de vuelta al array

---

## Razonamiento

La diferencia entre clases y structs en un array es fundamental:

- Un `Tile[]` (clase) es un array de **referencias** — los datos reales están dispersos en el heap. La CPU tiene que saltar por la memoria para leerlos.
- Un `TileData[]` (struct) es un array de **valores** — los datos están contiguos en memoria. La CPU los carga en cache de un solo viaje.

La Opción C ofrece el balance correcto para el estado actual del proyecto: eficiencia de memoria desde el día 1, sin sacrificar la legibilidad que un Technical Director espera ver en un portafolio.

La Opción B es superior en pureza DOD pero su ventaja solo se materializa en operaciones masivamente paralelas, que corresponden a la Fase 4 con Burst Compiler. Prematuramente optimizar a Opción B añadiría complejidad sin beneficio medible en un grid 20x20.

---

## Consecuencias

- El índice de acceso es manual: `int index = y * Width + x`
- Al modificar una tile hay que escribir explícitamente de vuelta: `_grid[index].Humidity = value` (no copiar el struct primero)
- Compatible con `NativeArray<TileData>` para Jobs en Fase 4 sin cambiar la estructura de datos
- Separa explícitamente datos (struct) de lógica (GridManager como clase), alineado con el enfoque híbrido POO/DOD del proyecto

---

## Decisión complementaria: TileFlags (bitmask) y BiomeType.Water

`TileData` usa dos mecanismos distintos para representar el agua:

- **`BiomeType.Water`** — agua permanente (ríos, lagos). Es un dato del bioma, inmutable durante el juego. Bloqueante para la mayoría de agentes en el MVP.
- **`TileFlags.Flooded`** (post-MVP) — estado dinámico: agua que se propaga por el grid. A diferencia del bioma permanente, ciertos agentes podrán atravesarlo.

Esta distinción es intencional: un aldeano no puede cruzar un río, pero podría vadear una zona inundada temporalmente. En el MVP solo existe `BiomeType.Water` como bloqueador. El flag `Flooded` queda reservado en el enum para la Fase post-MVP.

El campo `TileFlags Flags` usa un `byte` (bitmask) en lugar de campos `bool` separados por dos razones:
1. **Layout de memoria**: un `byte` ocupa 1 byte; múltiples `bool` en C# ocupan N bytes. El patrón es correcto para DOD aunque la diferencia sea pequeña en un grid 20×20.
2. **Extensibilidad**: nuevos estados (fuego, tiles sagradas, etc.) no cambian el tamaño del struct ni la API — solo se añade un bit al enum.

---

## Visión a Futuro: Mecánicas de Fluidos

El campo `Humidity` en `TileData` y el bit reservado `Flooded` en `TileFlags` están diseñados para soportar una mecánica de fluidos dinámica al estilo de Timberborn: inundaciones que se propagan por el grid, desastres naturales y formación de ríos.

La visión es que `Humidity` actúe como presión de agua por tile y que una simulación por tick la propague hacia tiles adyacentes de menor `Altitude`. Cuando supera el umbral de saturación, el tile recibe el flag `Flooded`. A diferencia de `BiomeType.Water`, el flag `Flooded` es transitorio y su efecto sobre la walkability depende del tipo de agente.

Esta mecánica **no pertenece al MVP**. La arquitectura actual no la implementa, pero está diseñada para que su adición no requiera modificar `TileData` ni la estructura del Grid.

---

## Relevancia para el Portafolio

Esta decisión demuestra comprensión de **cache locality**, **Data-Oriented Design** y **planificación para escalabilidad** — directamente alineado con el objetivo de optimización del frame budget y 500 agentes a 60 FPS.
