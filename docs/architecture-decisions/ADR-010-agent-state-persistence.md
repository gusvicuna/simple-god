# ADR-010: Persistencia del Estado de los Agentes

**Estado:** Aceptado  
**Fecha:** 2026-06  
**Proyecto:** Simple God

---

## Decisión

Usar una **arquitectura dual**: el AIManager es dueño de un array centralizado de `AgentState` (structs) para actualización cache-friendly y compatibilidad con Job System. Cada `AgentBrain` mantiene una copia local sincronizada por tick para decisiones, Behavior Tree y Debug UI.

---

## Contexto

Cada agente tiene un estado interno que cambia constantemente:

```
Hunger → sube con el tiempo, baja al comer
Fear   → sube cerca de depredadores o desastres, baja en zonas seguras
Faith  → sube al rezar o ver milagros, baja con el tiempo
```

La decisión de dónde viven estos datos afecta directamente la eficiencia de iteración del AIManager, la compatibilidad futura con Job System y cómo la UtilityAI accede al estado para calcular scores.

---

## Alternativas Consideradas

### Opción A — Datos dentro del AgentBrain (MonoBehaviour)

```csharp
public class AgentBrain : MonoBehaviour
{
    public float Hunger;
    public float Fear;
    public float Faith;
}
```

- ✅ Simple e intuitivo — el agente tiene acceso directo a su estado
- ❌ Datos dispersos en el heap — cache misses al iterar sobre 500 agentes
- ❌ Incompatible con Job System — MonoBehaviours no pueden pasarse a Jobs

### Opción B — Struct separado dentro del AgentBrain

```csharp
public struct AgentState { public float Hunger, Fear, Faith; }

public class AgentBrain : MonoBehaviour
{
    public AgentState State;
}
```

- ✅ Separación explícita de datos y lógica
- ✅ `AgentState` pasable a métodos sin exponer el MonoBehaviour completo
- ❌ Sigue disperso en el heap — el AIManager aún salta entre objetos al iterar
- ⚠️ Parcialmente compatible con Job System — el struct se puede copiar pero no es óptimo

### Opción C — Array centralizado en AIManager + copia local en AgentBrain *(elegida)*

```csharp
// AIManager: dueño del array central
private AgentState[] _states;

// AgentBrain: copia local sincronizada por tick
private AgentState _localState;
```

- ✅ Array central contiguo en memoria — iteración cache-friendly sobre todos los agentes
- ✅ Compatible directamente con `NativeArray<AgentState>` para Job System en Fase 4
- ✅ AgentBrain tiene acceso inmediato a su estado local para BT y Debug UI
- ✅ Sincronización simple — struct se copia por valor, sin referencias compartidas
- ⚠️ Requiere sincronización explícita entre array central y copia local cada tick

---

## Estructura de Datos

```csharp
// Struct puro — solo datos, sin lógica
public struct AgentState
{
    public float Hunger;   // [0, 1] — 1 = hambre crítica
    public float Fear;     // [0, 1] — 1 = pánico total
    public float Faith;    // [0, 1] — 1 = fe máxima

    // Metadatos útiles para el AIManager
    public bool  IsAlive;
    public int   GridIndex; // posición actual en el Grid (y * width + x)
}
```

---

## Implementación

### AIManager — dueño y actualizador del estado central

```csharp
public class AIManager : MonoBehaviour
{
    private AgentState[] _states;  // array central cache-friendly
    private AgentBrain[] _brains;  // referencias para sincronización

    public void Tick()
    {
        for (int i = 0; i < _states.Length; i++)
        {
            if (!_states[i].IsAlive) continue;

            // Actualización del estado — itera sobre datos contiguos en memoria
            _states[i].Hunger = Mathf.Clamp01(_states[i].Hunger + _hungerRate);
            _states[i].Fear   = CalculateFear(i);
            _states[i].Faith  = CalculateFaith(i);

            // Sincroniza copia local y dispara decisión
            _brains[i].OnStateTick(_states[i]);
        }
    }

    public void RegisterAgent(AgentBrain brain, int index)
    {
        _brains[index] = brain;
        _states[index] = new AgentState { IsAlive = true };
    }

    // Llamado por el AgentBrain cuando ejecuta una acción
    // (ej. comer baja el Hunger en el array central)
    public void ApplyStateChange(int agentIndex, AgentStateChange change)
    {
        _states[agentIndex].Hunger = Mathf.Clamp01(_states[agentIndex].Hunger + change.HungerDelta);
        _states[agentIndex].Faith  = Mathf.Clamp01(_states[agentIndex].Faith  + change.FaithDelta);
        _states[agentIndex].Fear   = Mathf.Clamp01(_states[agentIndex].Fear   + change.FearDelta);
    }
}
```

### AgentBrain — usa la copia local para decisiones y visualización

```csharp
public class AgentBrain : MonoBehaviour
{
    private AgentState   _localState;  // copia local sincronizada
    private UtilityAI    _utilityAI;
    private BehaviorTree _behaviorTree;
    private int          _agentIndex;  // índice en el array del AIManager

    // Llamado por AIManager cada tick
    public void OnStateTick(AgentState updatedState)
    {
        _localState = updatedState; // copia por valor — simple y segura

        // Con el estado fresco, decide y actúa
        ActionType best = _utilityAI.Evaluate(_localState);
        _behaviorTree.Execute(best, _localState);
    }

    // Acceso de lectura para Debug UI y otros sistemas
    public AgentState GetState() => _localState;
}
```

### AgentStateChange — struct para cambios discretos

```csharp
// Cambios que las acciones aplican al estado
// (separados del tick para no mezclar responsabilidades)
public struct AgentStateChange
{
    public float HungerDelta;
    public float FearDelta;
    public float FaithDelta;

    // Ejemplos de uso:
    // Comer:  HungerDelta = -0.4f
    // Rezar:  FaithDelta  = +0.2f
    // Huir:   FearDelta   = -0.1f (alivio al escapar)
}
```

---

## Flujo por Tick

```
GameLoop.SimulationTick()
    └── AIManager.Tick()
            │
            ├── for i in _states[]               ← iteración cache-friendly
            │       actualizar Hunger, Fear, Faith
            │       _brains[i].OnStateTick(_states[i])
            │               │
            │               ├── _localState = updatedState  ← copia por valor
            │               ├── UtilityAI.Evaluate(_localState)
            │               └── BehaviorTree.Execute(bestAction)
            │                       │
            │                       └── acción ejecutada
            │                           AIManager.ApplyStateChange(index, change)
            │                                   └── modifica _states[index] inmediatamente
            │
            └── siguiente agente
```

---

## Migración a Job System en Fase 4

La arquitectura está diseñada para que la migración a Job System no requiera cambiar `AgentBrain`:

```csharp
// Fase 4: reemplazar el array managed por NativeArray
NativeArray<AgentState> _states = new NativeArray<AgentState>(count, Allocator.Persistent);

// Job paralelo para actualización de estados
[BurstCompile]
struct UpdateAgentStatesJob : IJobParallelFor
{
    public NativeArray<AgentState> States;
    public float HungerRate;

    public void Execute(int index)
    {
        if (!States[index].IsAlive) return;
        var state = States[index];
        state.Hunger = math.clamp(state.Hunger + HungerRate, 0f, 1f);
        States[index] = state;
    }
}

// AgentBrain.OnStateTick() no cambia — sigue recibiendo su AgentState por valor
```

---

## Consecuencias

- `AgentState` es un struct — se copia por valor en la sincronización, sin riesgo de referencias compartidas
- El AIManager es la única fuente de verdad del estado — el AgentBrain nunca modifica el array central directamente
- Cambios de estado por acciones (comer, rezar) van a través de `ApplyStateChange()` — punto único de mutación
- La Debug UI lee `AgentBrain.GetState()` — sin acoplamiento al AIManager
- En Fase 4, `AgentState[]` se reemplaza por `NativeArray<AgentState>` sin tocar AgentBrain

---

## Relevancia para el Portafolio

Esta arquitectura demuestra comprensión de **Data-Oriented Design aplicado a sistemas de agentes** — separación explícita de datos y lógica, iteración cache-friendly y planificación para paralelismo. La doble representación (array central + copia local) refleja un patrón común en simulaciones de alto rendimiento.
