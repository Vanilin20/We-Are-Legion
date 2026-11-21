using System.Collections.Generic;
using UnityEngine;

// Черга завдань однієї групи. Без пріоритетів: беруться за порядком створення.
// Недосяжне завдання не блокує чергу — його пропускаємо, доки до нього не з'явиться шлях.
public class TaskQueue
{
    private readonly List<AntTask> order = new List<AntTask>();
    private readonly Dictionary<AntTask, AntAgent> takenBy = new Dictionary<AntTask, AntAgent>();

    public int Count => order.Count;

    public bool Contains(AntTask task) => order.Contains(task);

    public AntAgent Owner(AntTask task)
    {
        return takenBy.TryGetValue(task, out AntAgent a) ? a : null;
    }

    public bool IsTakenBy(AntTask task, AntAgent ant) => Owner(task) == ant;

    public void Add(AntTask task)
    {
        if (!order.Contains(task)) order.Add(task);
    }

    public void Remove(AntTask task)
    {
        order.Remove(task);
        takenBy.Remove(task);
    }

    // Закріпити завдання за мурахою (ручна команда — без пошуку).
    public void Claim(AntTask task, AntAgent ant)
    {
        Add(task);
        takenBy[task] = ant;
    }

    public void Release(AntTask task, AntAgent ant)
    {
        if (IsTakenBy(task, ant)) takenBy.Remove(task);
    }

    // Перше вільне досяжне завдання. path — шлях до місця роботи (без стартової клітинки).
    public bool TryTake(AntAgent ant, GridManager grid, Vector3Int antCell, out AntTask task, out List<Vector3Int> path)
    {
        task = null;
        path = null;
        if (order.Count == 0) return false;

        Dictionary<Vector3Int, Vector3Int> reach = grid.BuildReachMap(antCell);
        Colony colony = Colony.Instance;
        System.Func<Vector3Int, bool> isFree = c => colony == null || !colony.IsReservedByOther(c, ant);

        for (int i = 0; i < order.Count; i++)
        {
            AntTask t = order[i];
            if (takenBy.ContainsKey(t)) continue;
            if (!t.IsValid(grid)) { Remove(t); i--; continue; }

            // недосяжно або всі місця біля завдання зайняті іншими мурахами — відкладаємо
            List<Vector3Int> p = grid.PathFor(reach, antCell, t, isFree);
            if (p == null) continue;

            takenBy[t] = ant;
            task = t;
            path = p;
            return true;
        }
        return false;
    }
}
