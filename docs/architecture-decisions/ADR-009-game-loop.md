# ADR-009: Ciclo de Actualización del Mundo (Game Loop)

**Estado:** Aceptado  
**Fecha:** 2026-06  
**Proyecto:** Simple God

---

## Decisión

Usar un **GameLoop central con tick de simulación configurable**, separado del framerate de Unity. Los eventos del jugador se procesan inmediatamente en cada frame. Los sistemas de simulación corren en orden explícito y predecible solo cuando el tick lo indica.

---

## Contexto

Simple God tiene múltiples sistemas que deben actualizarse cada frame o cada cierto intervalo: GridManager, FloraSystem, AIManager, DayNightCycle y PowerSystem. El orden de ejecución entre ellos importa — un agente que decide con datos del frame anterior puede comportarse incorrectamente. Unity no garantiza el orden de ejecución entre MonoBehaviours sin configuración explícita.

Adicionalmente, la simulación no es un juego de reacción rápida. El mundo autónomo puede avanzar a su propio ritmo sin afectar la experiencia del jugador, lo que abre oportunidades de optimización y control de velocidad.

---

## Decisiones Tomadas

### 1. Tick de simulación separado del framerate

El mundo autónomo corre a su propio ritmo, independiente de los FPS.

**Opción descartada — Todo en Unity Update():**
- Ata la velocidad de la simulación al framerate
- En máquinas lentas la simulación se ralentiza; en máquinas rápidas se acelera
- No permite control de velocidad de simulación

**Opción elegida — Tick de simulación propio:**
- El mundo avanza a un ritmo configurable y predecible
- Independiente del framerate — mismo comportamiento en cualquier máquina
- Habilita control de velocidad (1x/2x/3x) sin cambiar la lógica
- Simple God no es un juego de reacción rápida — el jugador no necesita que el mundo reaccione 60 veces por segundo

### 2. GameLoop central con orden explícito

Un único `GameLoop` coordina todos los sistemas en lugar de dejar que cada MonoBehaviour tenga su propio `Update()`.

**Opción descartada — Cada sistema con su propio Update():**
- Unity no garantiza el orden de ejecución entre MonoBehaviours
- El orden debe configurarse manualmente en Project Settings y es frágil
- Difícil de razonar sobre qué datos tiene cada sistema cuando corre

**Opción elegida — GameLoop central:**
- El orden de ejecución está documentado y es explícito en el código
- Un solo lugar para entender cómo avanza el mundo cada tick
- Fácil de extender con nuevos sistemas sin romper el orden existente

### 3. Tick configurable (no FixedUpdate)

**Opción descartada — FixedUpdate() de Unity:**
- Fijo en 50Hz por defecto — no configurable en runtime
- Diseñado para física, no para simulación de agentes
- No permite acelerar o pausar la simulación independientemente

**Opción elegida — Timer propio en Update():**
- Intervalo configurable en el Inspector y en runtime
- Permite control de velocidad de simulación
- El jugador puede pausar el mundo sin pausar la UI

---

## Implementación

```csharp
public class GameLoop : MonoBehaviour
{
    [SerializeField] private float _baseTickInterval = 0.5f;
    private float _tickInterval;
    private float _tickTimer = 0f;

    // Referencias a todos los sistemas
    [SerializeField] private DayNightCycle  _dayNightCycle;
    [SerializeField] private GridManager    _gridManager;
    [SerializeField] private FloraSystem    _floraSystem;
    [SerializeField] private AIManager      _aiManager;
    [SerializeField] private PowerSystem    _powerSystem;

    void Awake() => _tickInterval = _baseTickInterval;

    void Update()
    {
        // Eventos del jugador: siempre inmediatos, fuera del tick
        _powerSystem.ProcessPlayerInput();

        // Simulación: solo cuando toca el tick
        _tickTimer += Time.deltaTime;
        if (_tickTimer >= _tickInterval)
        {
            _tickTimer = 0f;
            SimulationTick();
        }
    }

    void SimulationTick()
    {
        _dayNightCycle.Tick();  // 1. Avanza el tiempo → afecta temperatura y luz
        _gridManager.Tick();    // 2. Actualiza variables del Grid con nueva temperatura
        _floraSystem.Tick();    // 3. Flora reacciona al Grid ya actualizado
        _aiManager.Tick();      // 4. Agentes deciden con datos completamente frescos
    }

    // Control de velocidad de simulación
    public void SetSimulationSpeed(float multiplier) =>
        _tickInterval = _baseTickInterval / multiplier;

    public void PauseSimulation() =>
        _tickInterval = float.MaxValue;

    public void ResumeSimulation() =>
        _tickInterval = _baseTickInterval;
}
```

---

## Orden de Ejecución y su Razón

El orden dentro de `SimulationTick()` no es arbitrario:

| Orden | Sistema | Por qué va aquí |
|---|---|---|
| 1 | DayNightCycle | Es la fuente de verdad del tiempo. Afecta temperatura y luz, que el Grid necesita para actualizarse |
| 2 | GridManager | Actualiza variables de tiles usando la nueva temperatura. Es la fuente de verdad del entorno |
| 3 | FloraSystem | Lee el Grid ya actualizado para crecer o morir. Depende de humedad y temperatura frescos |
| 4 | AIManager | Los agentes deciden con el estado del mundo completamente actualizado en este tick |

Invertir cualquiera de estos órdenes introduce un frame de desfase — los sistemas trabajan con datos del tick anterior.

---

## Separación: Jugador vs Simulación

Un aspecto crítico del diseño es que los poderes divinos del jugador se procesan **fuera del tick**:

```
Update() frame 1: PowerSystem.ProcessPlayerInput() → jugador lanza Lluvia
                  tick aún no toca → GridManager no corrió

Update() frame 2: PowerSystem.ProcessPlayerInput() → sin input
                  tick toca → GridManager lee la lluvia aplicada → FloraSystem reacciona
```

Esto garantiza que la intervención del jugador siempre se registra en el mundo antes del próximo tick, sin forzar un tick anticipado.

---

## Control de Velocidad de Simulación

La arquitectura de tick configurable habilita naturalmente el control de velocidad al estilo RimWorld:

```csharp
// Velocidades predefinidas
SetSimulationSpeed(1f);   // Normal  → tick cada 0.5s
SetSimulationSpeed(2f);   // Rápido  → tick cada 0.25s
SetSimulationSpeed(3f);   // Muy rápido → tick cada 0.167s
PauseSimulation();        // Pausa   → sin ticks
```

La lógica de los sistemas no cambia — solo cambia la frecuencia con la que se llaman.

---

## Consecuencias

- Todos los sistemas exponen un método `Tick()` en lugar de usar `Update()` propio
- El orden de ejecución está documentado en un único lugar — `GameLoop.SimulationTick()`
- Agregar un nuevo sistema es añadir una línea en `SimulationTick()` en el lugar correcto
- El control de velocidad es una feature de portafolio inmediata sin trabajo adicional
- El Profiler de Unity puede medir el costo de `SimulationTick()` como una unidad cohesiva

---

## Relevancia para el Portafolio

Un GameLoop central con tick de simulación configurable demuestra comprensión de **arquitectura de sistemas de juego** más allá del uso básico de Unity. La separación explícita entre input del jugador y simulación autónoma refleja el diseño de simulaciones como RimWorld y Timberborn — referencias directas del proyecto.
