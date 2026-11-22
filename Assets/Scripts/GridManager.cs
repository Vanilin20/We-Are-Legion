using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Сітка однієї локації (мурашник або поверхня) поверх Tilemap:
// wallTilemap  — земля/стіни (те, що копаємо); на поверхні може бути порожнім,
// floorTilemap — підлога (по ній ходять).
// Копання = прибрати клітинку зі стін.
public class GridManager : MonoBehaviour
{
    private static readonly List<GridManager> all = new List<GridManager>();
    public static IReadOnlyList<GridManager> All => all;

    // Головна локація — мурашник (позначка isMain). Для старого коду, що знає лише одну сітку.
    public static GridManager Instance { get; private set; }

    // Локація, в межах якої лежить точка світу.
    public static GridManager At(Vector3 world)
    {
        foreach (GridManager g in all)
            if (g.InBounds(g.WorldToCell(world))) return g;
        return null;
    }

    [Header("Локація")]
    [SerializeField] private string locationName = "Anthill";
    [SerializeField] private bool isMain = true;

    [Header("Tilemap")]
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap floorTilemap;
    [Tooltip("Ставиться на місце викопаної стіни, якщо під нею немає підлоги")]
    [SerializeField] private TileBase floorTile;

    [Header("Позначка під копання")]
    [SerializeField] private Color markColor = new Color(1f, 0.85f, 0.2f, 0.45f);

    [Header("Пошук шляху")]
    [SerializeField] private int maxSearchCells = 20000;

    public float CellWorldSize { get; private set; } = 1f;

    // Позначені клітинки -> завдання копання (у черзі будівельників).
    private readonly Dictionary<Vector3Int, DigTask> digTasks = new Dictionary<Vector3Int, DigTask>();

    private Tilemap markTilemap;
    private Tile markTile;
    private BoundsInt walkBounds;

    private static readonly Vector3Int[] Dirs =
    {
        Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right
    };

    public string LocationName => locationName;
    public bool HasWalls => wallTilemap != null;

    // Сітка, за якою рахуємо клітинки (стіни й підлога вирівняні однаково).
    private Tilemap CellMap => wallTilemap != null ? wallTilemap : floorTilemap;

    void Awake()
    {
        if (floorTilemap == null)
        {
            Debug.LogError("GridManager " + name + ": не призначено floorTilemap");
            enabled = false;
            return;
        }

        all.Add(this);
        if (isMain || Instance == null) Instance = this;

        CellWorldSize = (CellMap.CellToWorld(Vector3Int.right) - CellMap.CellToWorld(Vector3Int.zero)).magnitude;

        floorTilemap.CompressBounds();
        if (wallTilemap != null) wallTilemap.CompressBounds();
        walkBounds = CellMap.cellBounds;

        if (wallTilemap != null) CreateMarkLayer();
    }

    void OnDestroy()
    {
        all.Remove(this);
        if (Instance == this) Instance = all.Count > 0 ? all[0] : null;
    }

    // ---------- Клітинки ----------

    public Vector3Int WorldToCell(Vector3 world) => CellMap.WorldToCell(world);

    public Vector3 CellCenter(Vector3Int cell)
    {
        Vector3 p = CellMap.GetCellCenterWorld(cell);
        p.z = 0f;
        return p;
    }

    public bool InBounds(Vector3Int cell) => Contains(walkBounds, cell);
    public BoundsInt CellBounds => walkBounds;

    public bool HasWall(Vector3Int cell) => wallTilemap != null && wallTilemap.HasTile(cell);

    public bool IsWalkable(Vector3Int cell)
    {
        return Contains(walkBounds, cell) && !HasWall(cell) && floorTilemap.HasTile(cell);
    }

    // Найближча прохідна клітинка (по колу, що розширюється). false — у межах maxRadius немає.
    public bool TryNearestWalkable(Vector3Int from, int maxRadius, out Vector3Int result)
    {
        result = from;
        if (IsWalkable(from)) return true;
        for (int r = 1; r <= maxRadius; r++)
        {
            bool found = false;
            float best = float.MaxValue;
            for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != r) continue; // лише край квадрата
                var c = new Vector3Int(from.x + x, from.y + y, 0);
                if (!IsWalkable(c)) continue;
                float d = x * x + y * y;
                if (d < best) { best = d; result = c; found = true; }
            }
            if (found) return true;
        }
        return false;
    }

    // Крайній ряд стін не копаємо, щоб мурахи не вийшли за мапу.
    public bool IsDiggable(Vector3Int cell)
    {
        BoundsInt b = walkBounds;
        bool inside = cell.x > b.xMin && cell.x < b.xMax - 1 && cell.y > b.yMin && cell.y < b.yMax - 1;
        return inside && HasWall(cell);
    }

    public bool Dig(Vector3Int cell)
    {
        if (!HasWall(cell)) return false;

        wallTilemap.SetTile(cell, null);
        if (!floorTilemap.HasTile(cell) && floorTile != null)
            floorTilemap.SetTile(cell, floorTile);

        Unmark(cell);
        return true;
    }

    // ---------- Позначки ----------

    private static TaskQueue BuilderQueue => Colony.Instance.Queue(AntGroup.Builder);

    public bool Mark(Vector3Int cell)
    {
        if (!IsDiggable(cell) || digTasks.ContainsKey(cell)) return false;
        var task = new DigTask(this, cell);
        digTasks[cell] = task;
        BuilderQueue.Add(task);
        markTilemap.SetTile(cell, markTile);
        return true;
    }

    public void Unmark(Vector3Int cell)
    {
        if (digTasks.TryGetValue(cell, out DigTask task))
        {
            digTasks.Remove(cell);
            if (Colony.Instance != null) BuilderQueue.Remove(task);
        }
        if (markTilemap != null) markTilemap.SetTile(cell, null);
    }

    public bool IsMarked(Vector3Int cell) => digTasks.ContainsKey(cell);

    public DigTask GetDigTask(Vector3Int cell)
    {
        return digTasks.TryGetValue(cell, out DigTask t) ? t : null;
    }

    // Позначити (якщо ще ні) і повернути завдання копання. null — тут копати не можна.
    public DigTask GetOrMarkDigTask(Vector3Int cell)
    {
        Mark(cell);
        return GetDigTask(cell);
    }

    // ---------- Пошук шляху (BFS по сітці) ----------

    // Хвиля від start по прохідних клітинках. Ключ — клітинка, значення — звідки прийшли.
    public Dictionary<Vector3Int, Vector3Int> BuildReachMap(Vector3Int start)
    {
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var frontier = new Queue<Vector3Int>();
        cameFrom[start] = start;
        frontier.Enqueue(start);

        while (frontier.Count > 0 && cameFrom.Count < maxSearchCells)
        {
            Vector3Int cur = frontier.Dequeue();
            foreach (Vector3Int d in Dirs)
            {
                Vector3Int next = cur + d;
                if (cameFrom.ContainsKey(next) || !IsWalkable(next)) continue;
                cameFrom[next] = cur;
                frontier.Enqueue(next);
            }
        }
        return cameFrom;
    }

    // Шлях до прохідної клітинки поруч із target. null — недосяжно.
    // isFree (необов'язково) — пропускати клітинки, де вже стоїть інша мураха.
    // Шлях не містить стартову клітинку; порожній список — вже стоїмо поруч.
    public List<Vector3Int> PathToNeighbor(Dictionary<Vector3Int, Vector3Int> reach, Vector3Int start, Vector3Int target,
        System.Func<Vector3Int, bool> isFree = null)
    {
        Vector3Int best = default;
        int bestLen = int.MaxValue;
        bool found = false;

        foreach (Vector3Int d in Dirs)
        {
            Vector3Int n = target + d;
            if (!reach.ContainsKey(n)) continue;
            if (isFree != null && !isFree(n)) continue;
            int len = PathLength(reach, start, n);
            if (len < bestLen) { bestLen = len; best = n; found = true; }
        }
        if (!found) return null;

        var path = new List<Vector3Int>();
        Vector3Int c = best;
        while (c != start)
        {
            path.Add(c);
            c = reach[c];
        }
        path.Reverse();
        return path;
    }

    // Шлях на саму клітинку. null — недосяжно.
    public List<Vector3Int> PathTo(Dictionary<Vector3Int, Vector3Int> reach, Vector3Int start, Vector3Int target)
    {
        if (!reach.ContainsKey(target)) return null;
        var path = new List<Vector3Int>();
        Vector3Int c = target;
        while (c != start)
        {
            path.Add(c);
            c = reach[c];
        }
        path.Reverse();
        return path;
    }

    public List<Vector3Int> PathFor(Dictionary<Vector3Int, Vector3Int> reach, Vector3Int start, AntTask task,
        System.Func<Vector3Int, bool> isFree = null)
    {
        // На саму клітинку йдемо, навіть якщо там хтось стоїть: вільна мураха сама відійде.
        if (task.StandNextTo) return PathToNeighbor(reach, start, task.Cell, isFree);
        return PathTo(reach, start, task.Cell);
    }

    // Шлях до найближчої вільної прохідної клітинки не далі maxSteps. null — немає.
    public List<Vector3Int> PathToNearestFree(Vector3Int start, System.Func<Vector3Int, bool> isFree, int maxSteps)
    {
        var cameFrom = new Dictionary<Vector3Int, Vector3Int> { [start] = start };
        var frontier = new Queue<(Vector3Int cell, int depth)>();
        frontier.Enqueue((start, 0));

        while (frontier.Count > 0)
        {
            var (cur, depth) = frontier.Dequeue();
            if (cur != start && isFree(cur)) return PathTo(cameFrom, start, cur);
            if (depth >= maxSteps) continue;

            foreach (Vector3Int d in Dirs)
            {
                Vector3Int next = cur + d;
                if (cameFrom.ContainsKey(next) || !IsWalkable(next)) continue;
                cameFrom[next] = cur;
                frontier.Enqueue((next, depth + 1));
            }
        }
        return null;
    }

    private static int PathLength(Dictionary<Vector3Int, Vector3Int> reach, Vector3Int start, Vector3Int cell)
    {
        int len = 0;
        while (cell != start) { cell = reach[cell]; len++; }
        return len;
    }

    // ---------- Службове ----------

    private void CreateMarkLayer()
    {
        var go = new GameObject("DigMarks");
        go.transform.SetParent(wallTilemap.transform.parent, false);
        go.transform.localPosition = wallTilemap.transform.localPosition;
        go.transform.localRotation = wallTilemap.transform.localRotation;
        go.transform.localScale = wallTilemap.transform.localScale;

        markTilemap = go.AddComponent<Tilemap>();
        markTilemap.tileAnchor = wallTilemap.tileAnchor;
        var r = go.AddComponent<TilemapRenderer>();
        var wallRenderer = wallTilemap.GetComponent<TilemapRenderer>();
        r.sortingLayerID = wallRenderer.sortingLayerID;
        r.sortingOrder = wallRenderer.sortingOrder + 1;

        const int px = 4;
        var tex = new Texture2D(px, px) { filterMode = FilterMode.Point };
        var pixels = new Color[px * px];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        float ppu = px / wallTilemap.layoutGrid.cellSize.x;
        markTile = ScriptableObject.CreateInstance<Tile>();
        markTile.sprite = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), ppu);
        markTile.color = markColor;
    }

    private static bool Contains(BoundsInt b, Vector3Int c)
    {
        return c.x >= b.xMin && c.x < b.xMax && c.y >= b.yMin && c.y < b.yMax;
    }
}
