# ADR-004: Implementación de Behavior Trees

**Estado:** Aceptado  
**Fecha:** 2026-06  
**Proyecto:** Simple God

---

## Decisión

Implementar el sistema de Behavior Trees **desde cero en C#**, sin usar assets de terceros ni el Behavior Package oficial de Unity, usando la variante **BT con Memoria + Interrupción Selectiva**.

---

## Contexto

Los agentes necesitan ejecutar acciones complejas que pueden tomar múltiples frames (moverse a un destino, comer, huir). El BT es la capa de ejecución que traduce la decisión de la UtilityAI en pasos concretos, manejando éxito, fallo y estados intermedios.

La decisión de implementarlo desde cero tiene implicaciones directas en el tiempo de desarrollo y en el valor del portafolio.

---

## Alternativas Consideradas

### Opción A — Unity Behavior Package (oficial, gratuito)
Paquete lanzado por Unity en 2024, integrado con el editor.

- ✅ Sin costo, integración nativa con Unity
- ✅ Editor visual incluido
- ❌ Relativamente nuevo, documentación y comunidad limitadas
- ❌ No demuestra comprensión arquitectónica en una entrevista técnica

### Opción B — Behavior Designer (Opsive, ~$90)
El asset de BT más usado en la industria Unity.

- ✅ Estándar de la industria, muy documentado
- ✅ Editor visual robusto, amplia comunidad
- ❌ Costo económico
- ❌ El reclutador puede preguntar si entiendes la arquitectura o solo usas la herramienta
- ❌ Dependencia de un asset externo en el portafolio

### Opción C — Implementación desde cero *(elegida)*
Sistema propio en C# puro.

- ✅ Demuestra comprensión real de la arquitectura en entrevistas técnicas
- ✅ Sin dependencias externas — el portafolio es completamente propio
- ✅ Control total sobre la integración con UtilityAI y AIManager
- ✅ Preparado para extender con nodos personalizados sin limitaciones
- ⚠️ Mayor tiempo de desarrollo inicial
- ⚠️ Sin editor visual (se compensa con Debug UI en runtime)

---

## Variantes de Behavior Tree Consideradas

Existen tres variantes principales de BT, con características distintas:

### BT Clásico (sin memoria)
Cada tick el árbol se evalúa desde la raíz. No recuerda dónde estaba.

- ✅ Simple de implementar y muy reactivo
- ❌ Ineficiente — recorre nodos ya evaluados cada tick
- ❌ Sin estado persistente — imposible ejecutar acciones multi-frame de forma natural

### BT con Memoria
El árbol recuerda qué nodo estaba ejecutando y continúa desde ahí cuando retorna RUNNING.

```
Tick 1: Raíz → Selector → Sequence(Comer) → Action(Moverse) → RUNNING
               ↓ guarda puntero aquí
Tick 2:                    Sequence(Comer) → Action(Moverse) → RUNNING
               ↑ continúa desde donde estaba, no desde la raíz
```

- ✅ Eficiente — no recorre el árbol completo cada tick
- ✅ Permite acciones multi-frame naturalmente
- ✅ Estado claro y predecible
- ⚠️ Sin interrupción, el agente ignora cambios críticos hasta terminar la acción actual

### BT con Interrupción Total
Cualquier cambio de contexto puede abortar el nodo actual y re-evaluar desde la raíz.

- ✅ Máxima reactividad
- ❌ Comportamiento errático — el agente cambia de acción constantemente
- ❌ Difícil de debuggear con muchos interruptores activos

### BT con Memoria + Interrupción Selectiva *(elegida)*
Combina la memoria del estado con interrupciones acotadas a condiciones críticas.

```
Aldeano yendo a comer → lobo aparece a 2 tiles → interrumpe y huye    ✅ creíble
Aldeano yendo a comer → ve recursos mejores     → termina lo que hace ✅ consistente
```

- ✅ Comportamiento creíble sin ser errático
- ✅ Solo condiciones críticas interrumpen — miedo extremo, hambre mortal
- ✅ Patrón estándar en simulaciones de NPCs — RimWorld, The Sims
- ✅ Control explícito sobre qué interrumpe y qué no

---

## Arquitectura del Sistema

### Contrato base

Todo nodo retorna uno de tres estados:

```csharp
public enum NodeState { SUCCESS, FAILURE, RUNNING }

public abstract class BehaviorNode
{
    public abstract NodeState Evaluate(AgentContext ctx);
}
```

### Nodos compuestos

```csharp
// Sequence: AND lógico — falla si cualquier hijo falla
public class SequenceNode : BehaviorNode
{
    private List<BehaviorNode> _children;
    private int _runningIndex = 0; // memoria: índice del hijo en RUNNING

    public override NodeState Evaluate(AgentContext ctx)
    {
        for (int i = _runningIndex; i < _children.Count; i++)
        {
            var result = _children[i].Evaluate(ctx);
            if (result == NodeState.RUNNING)
            {
                _runningIndex = i; // guarda dónde se quedó
                return NodeState.RUNNING;
            }
            if (result == NodeState.FAILURE)
            {
                _runningIndex = 0; // reinicia al fallar
                return NodeState.FAILURE;
            }
        }
        _runningIndex = 0;
        return NodeState.SUCCESS;
    }

    public void Abort() => _runningIndex = 0; // interrupción selectiva
}

// Selector: OR lógico — tiene éxito si cualquier hijo tiene éxito
public class SelectorNode : BehaviorNode
{
    private List<BehaviorNode> _children;
    private int _runningIndex = 0;

    public override NodeState Evaluate(AgentContext ctx)
    {
        for (int i = _runningIndex; i < _children.Count; i++)
        {
            var result = _children[i].Evaluate(ctx);
            if (result == NodeState.RUNNING)
            {
                _runningIndex = i;
                return NodeState.RUNNING;
            }
            if (result == NodeState.SUCCESS)
            {
                _runningIndex = 0;
                return NodeState.SUCCESS;
            }
        }
        _runningIndex = 0;
        return NodeState.FAILURE;
    }

    public void Abort() => _runningIndex = 0;
}
```

### Nodos hoja

```csharp
// Condition: pregunta algo del contexto — no tiene estado
public class ConditionNode : BehaviorNode
{
    private Func<AgentContext, bool> _condition;

    public override NodeState Evaluate(AgentContext ctx)
        => _condition(ctx) ? NodeState.SUCCESS : NodeState.FAILURE;
}

// Action: ejecuta algo, puede tardar múltiples frames
public abstract class ActionNode : BehaviorNode
{
    // Las acciones concretas implementan este método
    // y retornan RUNNING mientras estén en progreso
    public virtual void Abort() { } // limpieza al ser interrumpida
}
```

### Interrupción Selectiva

Solo condiciones críticas abortan la acción en curso:

```csharp
public class AgentBrain : MonoBehaviour
{
    private ActionType _currentAction = ActionType.None;

    public void OnStateTick(AgentState updatedState)
    {
        _localState = updatedState;

        // Verifica interrupción antes de continuar
        if (IsCriticalInterrupt())
        {
            _behaviorTree.Abort();
            _currentAction = ActionType.None;
        }

        // Si no hay acción en curso, UtilityAI elige una nueva
        if (_currentAction == ActionType.None)
            _currentAction = _utilityAI.Evaluate(_localState, GetContext());

        // BT ejecuta o continúa el subárbol de la acción actual
        NodeState result = _behaviorTree.Execute(_currentAction, GetContext());

        // Si terminó, libera el slot para que UtilityAI vuelva a evaluar
        if (result != NodeState.RUNNING)
            _currentAction = ActionType.None;
    }

    bool IsCriticalInterrupt()
    {
        // Solo condiciones que ponen en riesgo la supervivencia inmediata
        return (_localState.Fear > 0.8f && _currentAction != ActionType.Flee)
            || (_localState.Hunger > 0.95f && _currentAction != ActionType.Eat);
    }
}
```

### Ejemplo: árbol del aldeano con memoria e interrupción

```
Selector (¿Qué hago?)
├── Sequence (Huir del peligro)
│   ├── Condition: ¿Depredador en rango?
│   └── Action: Huir              ← RUNNING tick a tick hasta escapar
│                                    puede ser interrumpido por Hunger > 0.95
├── Sequence (Comer)
│   ├── Condition: ¿Hay comida accesible?
│   ├── Action: Moverse a comida  ← RUNNING, interrumpible por Fear > 0.8
│   └── Action: Comer             ← RUNNING hasta terminar
├── Sequence (Rezar)
│   ├── Condition: ¿Altar visible?
│   └── Action: Rezar             ← RUNNING, interrumpible por condiciones críticas
└── Action: Vagar                 ← fallback, siempre SUCCESS en un tick
```

---

## Consecuencias

- El BT es independiente de Unity (C# puro) — testeable sin levantar el editor
- Cada nodo compuesto guarda su `_runningIndex` — la memoria del estado vive en el árbol
- `Abort()` propaga hacia abajo limpiando el estado de todos los hijos
- La interrupción selectiva garantiza reactividad solo donde importa
- La Debug UI puede mostrar el nodo activo leyendo `_runningIndex` de cada compuesto
- Si en Fase 4 se requiere un editor visual, se puede añadir serialización sin cambiar la lógica

---

## Relevancia para el Portafolio

Implementar un BT con Memoria + Interrupción Selectiva desde cero demuestra comprensión profunda de **arquitecturas de comportamiento modulares** y sus trade-offs — la diferencia entre un BT clásico y uno con estado es exactamente el tipo de pregunta que hace un Technical Director en una entrevista de IA para videojuegos.
