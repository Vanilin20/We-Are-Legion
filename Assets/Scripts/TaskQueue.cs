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

    // Перше вільне досяжне завдання. maxSteps > 0 — лише не далі стількох клітинок.
    public bool TryTake(AntAgent ant, GridManager antGrid, Vector3Int antCell, out AntTask task, out Route route,
        int maxSteps = 0)
    {
        task = null;
        route = null;
        if (order.Count == 0) return false;

        var router = new Router();
        Colony colony = Colony.Instance;
        System.Func<GridManager, Vector3Int, bool> isFree =
            (g, c) => colony == null || !colony.IsReservedByOther(g, c, ant);

        for (int i = 0; i < order.Count; i++)
        {
            AntTask t = order[i];
            if (takenBy.ContainsKey(t)) continue;
            if (!t.IsValid(ant)) continue;
            // їжу під охороною жука самі не беремо — лише за наказом гравця
            if (t is PickupTask && EnemyBeetle.Guards(t.Grid, t.Cell)) continue;

            // недосяжно або всі місця біля завдання зайняті іншими мурахами — відкладаємо
            Route r = router.Find(antGrid, antCell, t, isFree);
            if (r == null) continue;
            if (maxSteps > 0 && (r.Legs.Count > 1 || r.Legs[0].Path.Count > maxSteps)) continue;

            takenBy[t] = ant;
            task = t;
            route = r;
            return true;
        }
        return false;
    }
}
