# ADR-006: Perspectiva y Espacio de Coordenadas

**Estado:** Aceptado  
**Fecha:** 2026-06  
**Proyecto:** Simple God

---

## Decisión

Usar un **Grid 2D como espacio de datos**, con un **mundo visualmente 3D** y cámara isométrica real. La variable `Altitude` existe como dato de cada tile, pero no como dimensión del Grid.

---

## Contexto

El juego es un simulador de dios con vista isométrica. El GDD define un Grid como estructura central del mundo. La decisión de perspectiva afecta directamente la estructura de datos del Grid (ADR-001), el pathfinding (ADR-008) y la complejidad del MVP.

A futuro, el proyecto aspira a soportar un mundo con capas de altitud real al estilo Timberborn — simulaciones con terreno volumétrico donde los agentes pueden construir en altura. Esta visión debe informar las decisiones actuales sin imponerse al scope del MVP.

---

## Distinción Fundamental

Espacio de juego y representación visual son decisiones separadas:

```
Espacio de juego  → dónde viven los datos y la lógica (Grid, pathfinding, IA)
Representación    → cómo se renderiza en pantalla (3D, cámara, materiales)
```

Es posible — y conveniente — tener un Grid 2D con un mundo visualmente 3D.

---

## Alternativas Consideradas

### Opción A — 2D puro (datos y visual)
Grid 2D con sprites y cámara ortográfica isométrica simulada visualmente.

- ✅ Implementación más simple
- ❌ La ilusión isométrica en 2D tiene limitaciones visuales evidentes
- ❌ Migrar a 3D real en el futuro requeriría reescribir el sistema de renderizado completo

### Opción B — Grid 2D, mundo visualmente 3D *(elegida)*
Grid de datos plano (`TileData[]`), mundo renderizado con GameObjects 3D y cámara isométrica real.

```csharp
// Los datos siguen siendo un array plano — igual que ADR-001
TileData[] _grid = new TileData[width * height];
int index = y * width + x; // sin cambios

// Altitude es un dato de la tile, no una dimensión del grid
public struct TileData
{
    public float Humidity;
    public float Fertility;
    public float Temperature;
    public float Altitude;    // ← determina la altura visual del tile, no del índice
    public BiomeType Biome;
}

// El GameObject de cada tile se posiciona en 3D usando Altitude
Vector3 worldPos = new Vector3(x, tileData.Altitude, y);
```

- ✅ Los datos siguen siendo simples, eficientes y compatibles con ADR-001
- ✅ El mundo se ve en 3D desde el primer día
- ✅ `Altitude` permite terreno con variación visual sin complicar el pathfinding
- ✅ Migrar a grid volumétrico en el futuro es una refactorización controlada, no una reescritura
- ✅ Compatible con Job System y Burst Compiler (Fase 4)

### Opción C — Grid 3D volumétrico desde el inicio (estilo Timberborn completo)
Grid con tres dimensiones `grid[x, y, z]` con soporte de capas de altitud desde el MVP.

```csharp
// Índice volumétrico
int index = z * width * height + y * width + x;
```

- ✅ Preparado para la visión a largo plazo sin refactorización
- ❌ Triplica la complejidad del pathfinding desde el MVP
- ❌ Scope creep — añade meses de desarrollo sin beneficio jugable inmediato
- ❌ Un grid 20x20x1 paga el costo de la tercera dimensión sin usarla

---

## Razonamiento

La Opción B respeta el principio de no pagar costos de complejidad antes de necesitarlos. El MVP no requiere capas de altitud jugables — solo un mundo que se vea tridimensional y que tenga variación de terreno expresada en la variable `Altitude`.

La clave del diseño es que `Altitude` ya existe como dato en `TileData` desde el día uno. Cuando el proyecto esté listo para soportar un mundo volumétrico, el salto a Opción C es:

1. Cambiar el índice del array de `y * width + x` a `z * width * height + y * width + x`
2. Extender el pathfinding para considerar la dimensión Z
3. Ajustar el GridManager para gestionar capas

Eso es una refactorización planificada, no una reescritura. La arquitectura actual no cierra esa puerta.

---

## Consecuencias

- El Grid sigue siendo un array plano 1D con índice `y * width + x` (sin cambios a ADR-001)
- Cada tile se renderiza como un GameObject 3D posicionado en `Vector3(x, altitude, y)`
- La cámara es una cámara 3D de Unity con ángulo isométrico (~45° horizontal, ~30° vertical)
- El pathfinding (ADR-008) opera sobre coordenadas 2D — sin necesidad de considerar altura como dimensión navegable en el MVP
- Los agentes son primitivas 3D (cápsulas) posicionadas sobre los tiles correspondientes
- La variable `Altitude` permite generar terreno con variación visual desde el MVP

---

## Visión a Futuro

La referencia de diseño a largo plazo es **Timberborn**: simulación con terreno volumétrico, capas de altitud jugables y agentes que construyen en altura. La Opción B es el paso previo natural a esa visión — establece el mundo 3D sin asumir la complejidad volumétrica prematuramente.

---

## Relevancia para el Portafolio

Esta decisión demuestra capacidad de **planificar para la escalabilidad sin caer en over-engineering** — una habilidad valorada en ingeniería de videojuegos. La separación explícita entre espacio de datos y representación visual también refleja el enfoque híbrido POO/DOD del proyecto.
