using System.Collections.Generic;
using UnityEngine;

// Маршрут мурахи: один або кілька відрізків. Між відрізками — перехід через вхід.
public class Route
{
    public class Leg
    {
        public GridManager Grid;
        public List<Vector3Int> Path;  // без стартової клітинки
        public Portal ExitPortal;      // куди вийти в кінці відрізка; null — останній відрізок
    }

    public readonly List<Leg> Legs = new List<Leg>();

    public GridManager FinalGrid => Legs[Legs.Count - 1].Grid;

    public Vector3Int FinalCell(Vector3Int startCell)
    {
        for (int i = Legs.Count - 1; i >= 0; i--)
        {
            Leg leg = Legs[i];
            if (leg.Path.Count > 0) return leg.Path[leg.Path.Count - 1];
            if (i > 0) return Legs[i - 1].ExitPortal.Linked.Cell;
        }
        return startCell;
    }
}

// Пошук маршрутів у межах локації та між локаціями (через входи).
// Хвилі BFS кешуються на час одного пошуку.
public class Router
{
    private readonly Dictionary<(GridManager, Vector3Int), Dictionary<Vector3Int, Vector3Int>> reachCache =
        new Dictionary<(GridManager, Vector3Int), Dictionary<Vector3Int, Vector3Int>>();

    private Dictionary<Vector3Int, Vector3Int> Reach(GridManager grid, Vector3Int from)
    {
        if (!reachCache.TryGetValue((grid, from), out var r))
        {
            r = grid.BuildReachMap(from);
            reachCache[(grid, from)] = r;
        }
        return r;
    }

    // isFree(grid, cell) — чи вільне місце для роботи (для завдань «стати поруч»).
    public Route Find(GridManager fromGrid, Vector3Int from, AntTask task,
        System.Func<GridManager, Vector3Int, bool> isFree)
    {
        GridManager target = task.Grid;
        if (target == null) return null;

        if (target == fromGrid)
        {
            var path = fromGrid.PathFor(Reach(fromGrid, from), from, task, c => isFree == null || isFree(fromGrid, c));
            return path == null ? null : Single(fromGrid, path);
        }

        foreach (Portal p in Portal.All)
        {
            if (p.Grid != fromGrid) continue;
            Portal exit = p.Linked;
            if (exit == null || exit.Grid != target) continue;

            List<Vector3Int> toPortal = fromGrid.PathTo(Reach(fromGrid, from), from, p.Cell);
            if (toPortal == null) continue;

            List<Vector3Int> rest = target.PathFor(Reach(target, exit.Cell), exit.Cell, task,
                c => isFree == null || isFree(target, c));
            if (rest == null) continue;

            var route = new Route();
            route.Legs.Add(new Route.Leg { Grid = fromGrid, Path = toPortal, ExitPortal = p });
            route.Legs.Add(new Route.Leg { Grid = target, Path = rest });
            return route;
        }
        return null;
    }

    public static Route Single(GridManager grid, List<Vector3Int> path)
    {
        var route = new Route();
        route.Legs.Add(new Route.Leg { Grid = grid, Path = path });
        return route;
    }
}
