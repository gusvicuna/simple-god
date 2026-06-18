# ADR-005: Elección del Motor de Juego

**Estado:** Aceptado  
**Fecha:** 2026-06  
**Proyecto:** Simple God

---

## Decisión

Usar **Unity** con **C#** como motor de desarrollo principal.

---

## Contexto

La elección del motor afecta directamente el tiempo de desarrollo, las herramientas disponibles, la compatibilidad con las decisiones de arquitectura ya tomadas (Job System, Burst Compiler, Profiler) y la relevancia del proyecto para el mercado laboral.

Este es un proyecto de aprendizaje y portafolio técnico, no un producto comercial. Por lo tanto, el criterio de licenciamiento comercial tiene menor peso que la velocidad de desarrollo y la profundidad técnica demostrable.

---

## Alternativas Consideradas

### Godot
- ✅ Open source, sin royalties ni restricciones comerciales
- ✅ Comunidad creciente, buen soporte para 2D
- ✅ Motor de interés personal a largo plazo
- ❌ Requiere aprender GDScript o la integración C# (menos madura que en Unity)
- ❌ Añade tiempo de aprendizaje del engine al tiempo de desarrollo del proyecto
- ❌ El objetivo es demostrar dominio técnico en IA y simulación, no en aprender un engine nuevo

### Unreal Engine
- ✅ Estándar de la industria AAA, gráficos de alta calidad
- ✅ Herramientas de IA nativas (Behavior Trees visuales, EQS)
- ❌ Orientado a proyectos 3D de alta fidelidad visual — sobredimensionado para este scope
- ❌ Sin experiencia previa en el engine
- ❌ C++ como lenguaje principal añade complejidad innecesaria para los objetivos del proyecto

### Engine custom
- ✅ Demuestra conocimiento profundo de bajo nivel
- ✅ Proyecto interesante a largo plazo
- ❌ El tiempo de desarrollo del engine consumiría el tiempo del proyecto completo
- ❌ La mayoría de empresas de videojuegos usan engines existentes — un engine custom no es lo que buscan en un portafolio de IA
- ❌ Fuera del scope y objetivos actuales

### Unity *(elegido)*
- ✅ Experiencia previa: proyectos propios, curso universitario, ayudantías explicando el engine
- ✅ C# maduro con ecosistema robusto (Job System, Burst Compiler, Profiler)
- ✅ Las decisiones de arquitectura ya tomadas (ADR-001 a ADR-004) se apoyan en herramientas Unity específicas
- ✅ Amplia demanda laboral — Unity sigue siendo el engine más usado en estudios independientes y medianos
- ✅ Proyecto de portafolio y aprendizaje: el modelo de licenciamiento comercial (royalties por ventas) no aplica
- ⚠️ Unity cobra un porcentaje sobre ventas al superar cierto umbral de ingresos — irrelevante para este proyecto

---

## Razonamiento

La elección de un motor para un proyecto de portafolio debe maximizar la profundidad técnica demostrable, no el aprendizaje del motor en sí. Usar Unity permite concentrar el tiempo de desarrollo en los sistemas de IA, simulación y optimización que son el objetivo real del proyecto.

La experiencia previa con Unity no es solo comodidad — significa que el tiempo de desarrollo no se gasta en aprender APIs básicas del engine, sino en construir arquitecturas no triviales encima de él. Eso es lo que diferencia este proyecto de un tutorial.

Godot es una alternativa legítima y de interés personal, pero aprenderlo simultáneamente al desarrollo añadiría una variable de riesgo innecesaria al cronograma.

---

## Consecuencias

- Versión a usar: Unity LTS más reciente estable (minimiza bugs de versión en un proyecto largo)
- Las herramientas de optimización de Fase 4 (Job System, Burst Compiler, Unity Profiler) están disponibles nativamente
- El proyecto no tiene restricciones de licenciamiento al no ser comercial
- Godot queda como motor de aprendizaje para un proyecto futuro independiente de este

---

## Relevancia para el Portafolio

Unity sigue siendo el engine más representado en ofertas laborales de estudios independientes y medianos. Demostrar dominio técnico profundo en Unity — más allá del uso básico del editor — es directamente valorable en entrevistas técnicas para posiciones de IA en videojuegos.
