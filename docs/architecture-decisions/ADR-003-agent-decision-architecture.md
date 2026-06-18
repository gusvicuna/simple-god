# ADR-003: Arquitectura de Decisión de Agentes

**Estado:** Aceptado  
**Fecha:** 2026-06  
**Proyecto:** Simple God

---

## Decisión

Usar las tres capas en conjunto, cada una con su responsabilidad específica:

- **AIManager:** controla cuándo y en qué orden se evalúan los agentes (scheduling)
- **UtilityAI:** determina qué acción ejecutar, ponderando contexto y personalidad
- **AgentBrain:** cada agente tiene su propio cerebro con parámetros de personalidad individuales

---

## Contexto

Los agentes necesitan tomar decisiones autónomas considerando múltiples estímulos simultáneos (hambre, peligro, fe). A futuro, cada aldeano debe tener personalidad propia que matice sus decisiones. Con 500 agentes como objetivo, el sistema de decisión tiene que ser eficiente y distribuible en el tiempo.

---

## Alternativas Consideradas

### Opción A — Cada agente decide por su cuenta cada frame
```csharp
// En cada aldeano:
void Update()
{
    EvaluateAndAct();
}
```
- ✅ Simple de implementar inicialmente
- ❌ 500 agentes × evaluación completa = saturación del Main Thread
- ❌ Sin punto centralizado para optimizar o aplicar Job System después

### Opción B — AIManager centralizado decide por todos
```csharp
// Un manager itera sobre todos:
void Update()
{
    foreach (var agent in _agents)
        agent.Act(DecideFor(agent));
}
```
- ✅ Control centralizado, más fácil de optimizar
- ❌ La lógica de decisión vive fuera del agente — difícil de personalizar
- ❌ Mezcla scheduling con lógica de decisión

### Opción C — Capas separadas con responsabilidades distintas *(elegida)*

```csharp
// AIManager: solo scheduling
public class AIManager : MonoBehaviour
{
    private List<AgentBrain> _agents;
    private int _currentIndex = 0;

    void Update()
    {
        // Time-slicing: N agentes por frame, no todos a la vez
        int agentsPerFrame = Mathf.CeilToInt(_agents.Count / 10f);
        for (int i = 0; i < agentsPerFrame; i++)
        {
            _agents[_currentIndex].EvaluateAndAct();
            _currentIndex = (_currentIndex + 1) % _agents.Count;
        }
    }
}

// AgentBrain: cerebro individual con personalidad
public class AgentBrain : MonoBehaviour
{
    public PersonalityData Personality;
    private UtilityAI _utilityAI;
    private BehaviorTree _behaviorTree;

    public void EvaluateAndAct()
    {
        Action best = _utilityAI.Evaluate(GetContext(), Personality);
        _behaviorTree.Execute(best);
    }
}

// PersonalityData: modificadores individuales
public struct PersonalityData
{
    public float FearSensitivity;  // 0.5 = valiente, 2.0 = cobarde
    public float FaithBias;        // tendencia a rezar vs otras acciones
    public float SocialRadius;     // peso de las necesidades de otros aldeanos
}
```

---

## Razonamiento

La confusión frecuente es tratar AIManager y UtilityAI como opciones excluyentes. Son capas distintas:

- El **AIManager** responde a *cuándo* — distribuye la carga computacional en el tiempo
- La **UtilityAI** responde a *qué* — evalúa acciones con scores ponderados
- La **personalidad** responde a *cómo* — modifica los parámetros de scoring por agente

La personalidad no requiere cambiar la arquitectura de UtilityAI. Solo cambian los multiplicadores que cada agente aplica a sus scores:

```csharp
float ScoreFleeing(AgentContext ctx, PersonalityData p)
{
    float baseDanger = ctx.NearbyPredators * 0.4f;
    return baseDanger * p.FearSensitivity; // el cobarde huye antes
}
```

Mismo sistema para todos, comportamiento diferente por agente. Esto es escalable y demostrable.

La separación AIManager / AgentBrain también es la preparación natural para Job System en Fase 4: el AIManager ya tiene todos los agentes centralizados y puede distribuir su evaluación en workers paralelos con mínimos cambios estructurales.

---

## Consecuencias

- El AIManager aplica **time-slicing**: distribuye la evaluación de agentes en múltiples frames
- Cada agente tiene su propio `AgentBrain` con `PersonalityData` como struct
- La UtilityAI no sabe qué agente la está usando — recibe contexto y personalidad como parámetros
- El AIManager es el punto de entrada para Job System en Fase 4 sin refactorizar AgentBrain
- Los agentes no se evalúan en el mismo frame — introduce un desfase mínimo pero aceptable en la simulación

---

## Relevancia para el Portafolio

Esta arquitectura demuestra **Utility AI con curvas matemáticas**, **comportamiento escalable con múltiples estímulos** y **planificación para optimización futura** — áreas clave en cualquier posición de IA para videojuegos.
