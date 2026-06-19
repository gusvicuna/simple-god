using System;
using System.Collections.Generic;

namespace SimpleGod.AI.Pathfinding
{
    /// <summary>
    /// Min-heap binario genérico. Usado por Pathfinder como Open List de A*.
    ///
    /// C# incluye PriorityQueue&lt;T,P&gt; desde .NET 6, pero la versión de Mono que empaqueta
    /// Unity no la expone en todas las plataformas destino. Implementación propia garantiza
    /// portabilidad y demuestra comprensión de la estructura de datos en contexto de entrevista.
    ///
    /// Complejidad: inserción O(log n), extracción O(log n), peek O(1).
    /// </summary>
    public class MinHeap<T> where T : IComparable<T>
    {
        private readonly List<T> _data = new();

        public int Count => _data.Count;
        public bool IsEmpty => _data.Count == 0;

        // ─── API pública ──────────────────────────────────────────────────────────

        public void Add(T item)
        {
            _data.Add(item);
            BubbleUp(_data.Count - 1);
        }

        /// <summary>Extrae y retorna el elemento mínimo. Lanza error si el heap está vacío.</summary>
        public T ExtractMin()
        {
            if (_data.Count == 0)
                throw new InvalidOperationException("ExtractMin llamado sobre heap vacío.");

            T min = _data[0];
            int last = _data.Count - 1;

            // Mueve el último elemento a la raíz y reordena hacia abajo
            _data[0] = _data[last];
            _data.RemoveAt(last);
            if (_data.Count > 0) SiftDown(0);

            return min;
        }

        /// <summary>Lee el mínimo sin extraerlo. Lanza error si el heap está vacío.</summary>
        public T Peek() =>
            _data.Count > 0
                ? _data[0]
                : throw new InvalidOperationException("Peek llamado sobre heap vacío.");

        // ─── Mantenimiento del orden del heap ─────────────────────────────────────

        // Sube el elemento en `index` hasta que el invariante de min-heap se restaure.
        private void BubbleUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_data[index].CompareTo(_data[parent]) >= 0) break;  // ya en orden
                Swap(index, parent);
                index = parent;
            }
        }

        // Baja el elemento en `index` hasta que el invariante de min-heap se restaure.
        private void SiftDown(int index)
        {
            int count = _data.Count;
            while (true)
            {
                int smallest = index;
                int left = 2 * index + 1;
                int right = 2 * index + 2;

                if (left < count && _data[left].CompareTo(_data[smallest]) < 0) smallest = left;
                if (right < count && _data[right].CompareTo(_data[smallest]) < 0) smallest = right;

                if (smallest == index) break;   // ya en orden
                Swap(index, smallest);
                index = smallest;
            }
        }

        private void Swap(int a, int b) => (_data[a], _data[b]) = (_data[b], _data[a]);
    }
}
