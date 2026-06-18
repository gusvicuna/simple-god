# ADR-002: Comunicación entre Agentes y Grid

**Estado:** Aceptado  
**Fecha:** 2026-06  
**Proyecto:** Simple God

---

## Decisión

Usar una **arquitectura híbrida Pull/Push**:
- **Pull (Spatial Query):** para la percepción ambiental de los agentes
- **Push (Eventos):** para cambios discretos del mundo (poderes divinos, ciclos globales)

---

## Contexto

Los agentes (aldeanos, lobos, conejos) necesitan información del Grid para tomar decisiones. El Grid puede cambiar por acciones del jugador (Lluvia, Rayo) o por ciclos del sistema (día/noche, crecimiento de flora).

El desafío es balancear reactividad con rendimiento, especialmente con múltiples agentes con distintos rangos de visión.

---

## Alternativas Consideradas

### Opción A — Push puro (suscripción por tile)
Cada agente se suscribe a las tiles dentro de su rango de visión. El Grid notifica a los agentes cuando cambia.

```csharp
// Al entrar al rango:
tile.OnChanged += agent.OnTileChanged;

// Al salir del rango:
tile.OnChanged -= agent.OnTileChanged;
```

- ✅ Los agentes solo procesan información cuando hay cambios reales
- ❌ Con agentes en movimiento, hay suscripciones/desuscripciones constantes
- ❌ El overhead de gestionar eventos puede superar el costo de consultar directamente
- ❌ Difícil de debuggear: los eventos crean dependencias implícitas

### Opción B — Pull puro (consulta cada frame)
Cada agente consulta el Grid en cada Update().

```csharp
void Update()
{
    TileData current = _grid.GetTile(_position);
    // evaluar...
}
```

- ✅ Simple de implementar
- ❌ Redundante: la mayoría de frames el Grid no cambió
- ❌ Con 500 agentes, esto satura el Main Thread

### Opción C — Híbrido Pull/Push *(elegida)*
```csharp
// PUSH: cambios del mundo notifican a sistemas globales
public static event Action<Vector2Int, TileData> OnTileChanged;

// Ejemplo: el jugador lanza Lluvia
GridManager.OnTileChanged?.Invoke(tilePos, updatedTile);
FloraSystem.OnTileChanged += RegrowGrass; // Flora reacciona

// PULL: el agente consulta cuando necesita decidir (no cada frame)
public void EvaluateActions()
{
    TileData[] nearby = _grid.GetTilesInRadius(_position, _visionRange);
    float bestScore = _utilityAI.Score(nearby);
}
```

- ✅ Push para eventos discretos y bien definidos (jugador, ciclos)
- ✅ Pull controlado: el agente consulta solo al tomar decisiones
- ✅ Sin overhead de suscripción dinámica por movimiento
- ✅ Compatible con time-slicing del AIManager (ver ADR-003)

---

## Razonamiento

El problema del Push puro con agentes móviles es que el costo de mantener las suscripciones actualizadas puede ser mayor que simplemente consultar el Grid cuando se necesita. Con 500 agentes moviéndose, esto se convierte en una tormenta de eventos.

La solución es reconocer que hay dos tipos de comunicación distintos en el sistema:

1. **Cambios del mundo** → pocos, discretos, muchos receptores → Push tiene sentido
2. **Percepción del agente** → frecuente, local, un receptor → Pull controlado es más eficiente

El Pull del agente no es "pull cada frame" sino "pull cuando toca decidir", coordinado por el AIManager. Esto convierte una consulta potencialmente costosa en algo predecible y distribuido en el tiempo.

---

## Consecuencias

- `GridManager` expone un evento estático `OnTileChanged` para sistemas globales
- `GridManager` expone `GetTilesInRadius(Vector2Int center, int radius)` para agentes
- Con el array plano de structs (ADR-001), `GetTilesInRadius` es un loop sobre índices contiguos — eficiente por diseño
- Los sistemas de Flora y Clima se suscriben a `OnTileChanged` (suscripción estática, no dinámica)
- Los agentes nunca se suscriben directamente a tiles individuales

---

## Relevancia para el Portafolio

Esta arquitectura demuestra comprensión de **sistemas ecológicos reactivos** y **comunicación entre sistemas**. El diseño híbrido también anticipa la optimización del frame budget al evitar polling innecesario.
