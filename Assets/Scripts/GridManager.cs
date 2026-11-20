using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Сітка мурашника поверх двох Tilemap:
// wallTilemap  — земля/стіни (те, що копаємо),
// floorTilemap — підлога (по ній ходять).
// Копання = прибрати клітинку зі стін.
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Tilemap")]
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap floorTilemap;
    [Tooltip("Ставиться на місце викопаної стіни, якщо під нею немає підлоги")]
    [SerializeField] private TileBase floorTile;

    [Header("Позначка під копання")]
    [SerializeField] private Color markColor = new Color(1f, 0.85f, 0.2f, 0.45f);

    [Header("Пошук шляху")]
    [SerializeField] private int maxSearchCells = 20000;

    public DigTaskQueue DigQueue { get; } = new DigTaskQueue();
    public float CellWorldSize { get; private set; } = 1f;

    private Tilemap markTilemap;
    private Tile markTile;
    private BoundsInt walkBounds;

    private static readonly Vector3Int[] Dirs =
    {
        Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right
    };

    void Awake()
    {
        Instance = this;

        if (wallTilemap == null || floorTilemap == null)
        {
            Debug.LogError("GridManager: не призначено wallTilemap або floorTilemap");
            enabled = false;
            return;
        }

        CellWorldSize = (wallTilemap.CellToWorld(Vector3Int.right) - wallTilemap.CellToWorld(Vector3Int.zero)).magnitude;

        wallTilemap.CompressBounds();
        floorTilemap.CompressBounds();
        walkBounds = wallTilemap.cellBounds;

        CreateMarkLayer();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- Клітинки ----------

    public Vector3Int WorldToCell(Vector3 world) => wallTilemap.WorldToCell(world);

    public Vector3 CellCenter(Vector3Int cell)
    {
        Vector3 p = wallTilemap.GetCellCenterWorld(cell);
        p.z = 0f;
        return p;
    }

    public bool HasWall(Vector3Int cell) => wallTilemap.HasTile(cell);

    public bool IsWalkable(Vector3Int cell)
    {
        return Contains(walkBounds, cell) && !wallTilemap.HasTile(cell) && floorTilemap.HasTile(cell);
    }

    // Крайній ряд стін не копаємо, щоб мурахи не вийшли за мапу.
    public bool IsDiggable(Vector3Int cell)
    {
        BoundsInt b = walkBounds;
        bool inside = cell.x > b.xMin && cell.x < b.xMax - 1 && cell.y > b.yMin && cell.y < b.yMax - 1;
        return inside && wallTilemap.HasTile(cell);
    }

    public bool Dig(Vector3Int cell)
    {
        if (!wallTilemap.HasTile(cell)) return false;

        wallTilemap.SetTile(cell, null);
        if (!floorTilemap.HasTile(cell) && floorTile != null)
            floorTilemap.SetTile(cell, floorTile);

        Unmark(cell);
        return true;
    }

    // ---------- Позначки ----------

    public bool Mark(Vector3Int cell)
    {
        if (!IsDiggable(cell) || !DigQueue.Add(cell)) return false;
        markTilemap.SetTile(cell, markTile);
        return true;
    }

    public void Unmark(Vector3Int cell)
    {
        DigQueue.Remove(cell);
        markTilemap.SetTile(cell, null);
    }

    public bool IsMarked(Vector3Int cell) => DigQueue.Contains(cell);

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

    // Шлях до будь-якої прохідної клітинки поруч із target. null — недосяжно.
    // Шлях не містить стартову клітинку; порожній список — вже стоїмо поруч.
    public List<Vector3Int> PathToNeighbor(Dictionary<Vector3Int, Vector3Int> reach, Vector3Int start, Vector3Int target)
    {
        Vector3Int best = default;
        int bestLen = int.MaxValue;
        bool found = false;

        foreach (Vector3Int d in Dirs)
        {
            Vector3Int n = target + d;
            if (!reach.ContainsKey(n)) continue;
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
