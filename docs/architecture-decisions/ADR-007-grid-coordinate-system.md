# ADR-007: Sistema de Coordenadas del Grid

**Estado:** Aceptado  
**Fecha:** 2026-06  
**Proyecto:** Simple God

---

## Decisión

Usar un **grid cuadrado con coordenadas offset cartesianas**. Cada tile se identifica por `(x, y)` y se mapea directamente al mundo 3D. El problema de distancias diagonales se resuelve con costos diferenciados en el pathfinding.

---

## Contexto

La forma de los tiles y su sistema de coordenadas afecta directamente la estructura del Grid (ADR-001), el pathfinding (ADR-008), el sistema de construcciones a futuro y la legibilidad del código. La decisión debe ser coherente con la visión de terreno volumétrico (ADR-006) y los juegos de referencia del proyecto.

---

## Alternativas Consideradas

### Opción A — Grid cuadrado con coordenadas offset *(elegida)*

Cada tile tiene una posición `(x, y)` como una tabla cartesiana.

```
(0,2) (1,2) (2,2)
(0,1) (1,1) (2,1)
(0,0) (1,0) (2,0)
```

Mapeo directo al mundo 3D:
```csharp
Vector3 WorldPosition(int x, int y, float altitude) =>
    new Vector3(x, altitude, y);
```

Vecinos cardinales y diagonales:
```csharp
// 4 vecinos cardinales
static readonly Vector2Int[] CardinalNeighbors = {
    new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
};

// 8 vecinos totales incluyendo diagonales
static readonly Vector2Int[] AllNeighbors = {
    new(1,0), new(-1,0), new(0,1), new(0,-1),   // cardinales
    new(1,1), new(1,-1), new(-1,1), new(-1,-1)   // diagonales
};
```

- ✅ Mapeo trivial a coordenadas 3D de Unity
- ✅ Construcciones rectangulares alineadas naturalmente con el grid
- ✅ Compatible con la visión de terreno volumétrico (ADR-006)
- ✅ Juegos de referencia directos: RimWorld, Dwarf Fortress, Timberborn
- ✅ Legible y familiar para cualquier programador
- ⚠️ Las diagonales no son equidistantes — se resuelve con costos diferenciados en A*

### Opción B — Grid hexagonal con coordenadas axiales

Tiles hexagonales con 6 vecinos todos equidistantes.

- ✅ Distancias uniformes en todas las direcciones — pathfinding más natural
- ✅ Sin el problema de diagonales
- ❌ Mapeo al mundo 3D más complejo
- ❌ Construcciones rectangulares difíciles de alinear con tiles hexagonales
- ❌ Inconsistente con la visión de terreno volumétrico al estilo Timberborn
- ❌ Los juegos de referencia del proyecto no usan hexágonos

### Opción C — Grid hexagonal con coordenadas cúbicas

Extensión del sistema axial con tres ejes `(q, r, s)` donde `q + r + s = 0`.

- ✅ Operaciones matemáticas más elegantes (distancia, rotación)
- ❌ Complejidad adicional que solo se justifica en grids hexagonales puros
- ❌ Mismas desventajas que Opción B para construcciones y visión a futuro

---

## Razonamiento

La decisión principal no es entre sistemas de coordenadas — es entre **tiles cuadrados o hexagonales**. El sistema de coordenadas es consecuencia de esa forma.

Los tiles hexagonales tienen la ventaja de distancias uniformes, pero penalizan severamente el sistema de construcciones. Alinear edificios rectangulares sobre un grid hexagonal requiere lógica adicional compleja y resulta visualmente antinatural.

Los tiles cuadrados son la elección correcta para un proyecto que aspira a soportar construcciones y terreno volumétrico. Los juegos de referencia más cercanos al diseño de Simple God — RimWorld, Dwarf Fortress, Timberborn — usan este sistema por las mismas razones.

El problema de las diagonales es real pero tiene una solución estándar y bien conocida en la industria: costos diferenciados en A*.

---

## Solución al Problema de Diagonales

La distancia entre tiles cardinales es `1.0`, pero entre tiles diagonales es `√2 ≈ 1.414`. Si A* trata ambas como costo uniforme, los agentes explotan las diagonales tomando rutas que recorren más espacio en menos pasos.

La solución es diferenciar el costo de movimiento:

```csharp
const float CARDINAL_COST = 1.0f;
const float DIAGONAL_COST = 1.414f; // √2

float MoveCost(Vector2Int from, Vector2Int to)
{
    bool isDiagonal = (to.x - from.x) != 0 && (to.y - from.y) != 0;
    return isDiagonal ? DIAGONAL_COST : CARDINAL_COST;
}
```

Esto hace que A* elija rutas naturales — el movimiento diagonal solo ocurre cuando genuinamente acorta el camino. La implementación completa se detalla en ADR-008.

---

## Consecuencias

- El índice del array sigue siendo `y * width + x` (sin cambios a ADR-001)
- El mapeo a coordenadas 3D de Unity es `new Vector3(x, altitude, y)`
- El pathfinding (ADR-008) implementa costos diferenciados cardinal/diagonal
- Las construcciones a futuro se alinean naturalmente con el grid cuadrado
- La migración a grid volumétrico (ADR-006, visión Timberborn) extiende el índice a `z * width * height + y * width + x` sin cambiar la lógica de coordenadas 2D

---

## Relevancia para el Portafolio

La elección del sistema de coordenadas refleja comprensión de las **implicaciones sistémicas de decisiones de diseño** — cómo la forma de un tile afecta pathfinding, construcciones y escalabilidad futura. Documentar el problema de las diagonales y su solución demuestra que la decisión fue informada, no arbitraria.
