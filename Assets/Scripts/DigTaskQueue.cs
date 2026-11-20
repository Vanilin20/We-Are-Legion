using System.Collections.Generic;
using UnityEngine;

// Черга клітинок під копання. Без пріоритетів: беруться за порядком позначення.
// Недосяжна клітинка не блокує чергу — її пропускаємо, поки до неї не прокопають хід.
public class DigTaskQueue
{
    private readonly List<Vector3Int> order = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> cells = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, AntAgent> takenBy = new Dictionary<Vector3Int, AntAgent>();

    public int Count => order.Count;

    public bool Contains(Vector3Int cell) => cells.Contains(cell);

    public bool IsTakenBy(Vector3Int cell, AntAgent ant)
    {
        return takenBy.TryGetValue(cell, out AntAgent a) && a == ant;
    }

    public bool Add(Vector3Int cell)
    {
        if (!cells.Add(cell)) return false;
        order.Add(cell);
        return true;
    }

    public void Remove(Vector3Int cell)
    {
        if (!cells.Remove(cell)) return;
        order.Remove(cell);
        takenBy.Remove(cell);
    }

    // Перше вільне досяжне завдання для мурахи. path — шлях до клітинки поруч.
    public bool TryTake(AntAgent ant, GridManager grid, Vector3Int antCell, out Vector3Int task, out List<Vector3Int> path)
    {
        task = default;
        path = null;
        if (order.Count == 0) return false;

        Dictionary<Vector3Int, Vector3Int> reach = grid.BuildReachMap(antCell);
        foreach (Vector3Int cell in order)
        {
            if (takenBy.ContainsKey(cell)) continue;

            List<Vector3Int> p = grid.PathToNeighbor(reach, antCell, cell);
            if (p == null) continue; // недосяжно — відкладаємо

            takenBy[cell] = ant;
            task = cell;
            path = p;
            return true;
        }
        return false;
    }

    public void Release(Vector3Int cell, AntAgent ant)
    {
        if (IsTakenBy(cell, ant)) takenBy.Remove(cell);
    }
}
