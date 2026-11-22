using System.Collections.Generic;
using UnityEngine;

// Жук-охоронець (EnemyBeetle). Стоїть біля своєї точки на поверхні, не патрулює й не заходить у мурашник.
//  Охорона  — стоїть, зрідка повертається на місці;
//  Виявлення — мураха ближче ніж guardRadius клітинок;
//  Реакція  — іде до найближчої мурахи й б'є раз на attackInterval;
//             далі ніж leashRadius від точки не відходить — повертається (від жука можна втекти);
//  Кінець   — здоров'я 0: зникає й лишає м'ясо (+8).
// Числа — з GDD («Антагоністичні елементи»).
public class EnemyBeetle : MonoBehaviour, ICellBlocker
{
    public enum State { Guard, Chase, Return }

    private static readonly List<EnemyBeetle> all = new List<EnemyBeetle>();
    public static IReadOnlyList<EnemyBeetle> All => all;

    [Header("Стати")]
    [SerializeField] private int maxHp = 12;
    [SerializeField] private int damage = 2;
    [Tooltip("Секунд між ударами")]
    [SerializeField] private float attackInterval = 1.5f;
    [Tooltip("Клітинок за секунду")]
    [SerializeField] private float speedCells = 1.5f;
    [Tooltip("Помічає мураху, ближчу за стільки клітинок")]
    [SerializeField] private float guardRadius = 3f;
    [Tooltip("Далі від своєї точки не відходить")]
    [SerializeField] private float leashRadius = 5f;
    [Tooltip("Скільки їжі в м'ясі, що лишається")]
    [SerializeField] private int meat = 8;

    [Header("Вигляд (поки заглушка)")]
    [Tooltip("Спрайт жука, що дивиться вгору. Порожньо — коло-заглушка")]
    [SerializeField] private Sprite sprite;
    [SerializeField] private Color placeholderColor = new Color(0.42f, 0.22f, 0.12f);
    [SerializeField] private float sizeCells = 0.95f;
    [SerializeField] private int sortingOrder = 2;
    [SerializeField] private float turnSpeed = 360f;

    private static readonly Vector3Int[] Dirs =
    {
        Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right
    };

    public int Hp { get; private set; }
    public int MaxHp => maxHp;
    public bool IsAlive => Hp > 0 && grid != null;
    public State CurrentState { get; private set; } = State.Guard;
    public GridManager Grid => grid;
    public Vector3Int Cell => grid != null ? grid.WorldToCell(transform.position) : homeCell;
    public Vector3Int HomeCell => homeCell;
    GridManager ICellBlocker.BlockGrid => grid;
    Vector3Int ICellBlocker.BlockCell => Cell;

    // Жук загинув (для відродження).
    public event System.Action<EnemyBeetle> Died;

    private GridManager grid;
    private Vector3Int homeCell;
    private AntAgent target;
    private List<Vector3Int> path;
    private int pathIndex;
    private float attackTimer;
    private float thinkTimer;
    private float idleTurnTimer;
    private float flashTimer;
    private float cell = 1f;

    private Transform visual;
    private SpriteRenderer body;
    private SpriteRenderer head;
    private WorldBar bar;
    private Quaternion wantRotation = Quaternion.identity;
    private Sprite[] walkFrames;
    private float walkTime;
    private Vector3 knock;

    // Чи стереже живий жук цю клітинку (у радіусі охорони від своєї точки або від того місця, де він зараз).
    // Сюди мурахи самі не ходять по їжу — лише за наказом гравця.
    public static bool Guards(GridManager grid, Vector3Int cell)
    {
        foreach (EnemyBeetle b in all)
        {
            if (!b.IsAlive || b.grid != grid) continue;
            if (Dist(cell, b.homeCell) <= b.guardRadius + 1f || Dist(cell, b.Cell) <= b.guardRadius + 1f) return true;
        }
        return false;
    }

    // Жук під точкою світу (тап гравця). radiusCells — наскільки точно треба влучити.
    public static EnemyBeetle At(Vector3 world, float radiusCells = 0.75f)
    {
        EnemyBeetle best = null;
        float bestD = float.MaxValue;
        foreach (EnemyBeetle b in all)
        {
            if (!b.IsAlive) continue;
            float d = ((Vector2)(b.transform.position - world)).magnitude / b.cell;
            if (d <= radiusCells && d < bestD) { bestD = d; best = b; }
        }
        return best;
    }

    void Start()
    {
        grid = GridManager.At(transform.position);
        if (grid == null)
        {
            Debug.LogError("EnemyBeetle " + name + ": поза сіткою");
            enabled = false;
            return;
        }

        if (!grid.TryNearestWalkable(grid.WorldToCell(transform.position), 6, out homeCell))
            Debug.LogWarning("EnemyBeetle " + name + ": поруч немає підлоги");
        transform.position = grid.CellCenter(homeCell);
        cell = grid.CellWorldSize;
        Hp = maxHp;

        CreateVisual();
        all.Add(this);
        if (Colony.Instance != null) Colony.Instance.AddBlocker(this);
        idleTurnTimer = Random.Range(2f, 5f);
    }

    void OnDestroy()
    {
        all.Remove(this);
        if (Colony.Instance != null) Colony.Instance.RemoveBlocker(this);
    }

    void Update()
    {
        if (!IsAlive) return;

        attackTimer -= Time.deltaTime;
        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f) Think();

        bool adjacent = target != null && IsAdjacent(target.CurrentCell);
        if (adjacent)
        {
            FaceTowards(target.transform.position);
            if (attackTimer <= 0f)
            {
                attackTimer = attackInterval;
                target.Damage(damage, this);
            }
        }
        else
        {
            Move();
            if (CurrentState == State.Guard) IdleTurn();
        }

        UpdateVisual();
    }

    // ---------- Рішення ----------

    private void Think()
    {
        thinkTimer = 0.25f;

        if (target != null && !TargetValid(target)) target = null;
        if (target == null) target = FindAnt();

        if (target != null)
        {
            CurrentState = State.Chase;
            if (IsAdjacent(target.CurrentCell)) { path = null; return; }
            path = PathNextTo(target.CurrentCell);
            pathIndex = 0;
            if (path == null) target = null; // не підійти в межах своєї зони — облишимо
        }

        if (target == null)
        {
            if (Cell != homeCell)
            {
                CurrentState = State.Return;
                path = PathToCell(homeCell);
                pathIndex = 0;
            }
            else
            {
                CurrentState = State.Guard;
                path = null;
            }
        }
    }

    // Найближча мураха в радіусі охорони, що стоїть у межах зони жука.
    private AntAgent FindAnt()
    {
        Colony colony = Colony.Instance;
        if (colony == null) return null;

        AntAgent best = null;
        float bestD = float.MaxValue;
        Vector3Int me = Cell;
        foreach (AntAgent a in colony.Ants)
        {
            if (a == null || !a.IsAlive || a.Grid != grid) continue;
            Vector3Int c = a.CurrentCell;
            float d = Dist(c, me);
            if (d > guardRadius || Dist(c, homeCell) > leashRadius) continue;
            if (d < bestD) { bestD = d; best = a; }
        }
        return best;
    }

    private bool TargetValid(AntAgent a)
    {
        return a != null && a.IsAlive && a.Grid == grid && Dist(a.CurrentCell, homeCell) <= leashRadius + 1f;
    }

    // ---------- Удар по жуку ----------

    public void Damage(int amount, AntAgent attacker = null)
    {
        if (!IsAlive || amount <= 0) return;
        Hp = Mathf.Max(0, Hp - amount);
        flashTimer = 0.25f;

        // «відскакує» від удару
        if (attacker != null)
        {
            Vector3 away = transform.position - attacker.transform.position;
            away.z = 0f;
            if (away.sqrMagnitude > 0.0001f) knock = away.normalized * cell * 0.15f;
            // б'ють — відповідаємо, якщо нападник у нашій зоні
            if (target == null && TargetValid(attacker)) { target = attacker; thinkTimer = 0f; }
        }

        if (Hp == 0) Die();
    }

    private void Die()
    {
        Vector3Int c = Cell;
        if (!grid.IsWalkable(c)) c = homeCell;
        FoodItem.Spawn(grid, c, meat, "Meat");
        Died?.Invoke(this);
        Destroy(gameObject);
    }

    // ---------- Рух ----------

    private void Move()
    {
        if (path == null || pathIndex >= path.Count) return;

        Vector3 next = grid.CellCenter(path[pathIndex]);
        FaceTowards(next);
        transform.position = Vector3.MoveTowards(transform.position, next, speedCells * cell * Time.deltaTime);
        if ((transform.position - next).sqrMagnitude < 0.0001f)
        {
            pathIndex++;
            thinkTimer = 0f; // на кожній клітинці дивимось, куди далі
        }
    }

    // BFS лише в межах зони жука (не далі leashRadius від точки охорони).
    private Dictionary<Vector3Int, Vector3Int> Reach()
    {
        Vector3Int start = Cell;
        var cameFrom = new Dictionary<Vector3Int, Vector3Int> { [start] = start };
        var frontier = new Queue<Vector3Int>();
        frontier.Enqueue(start);
        while (frontier.Count > 0)
        {
            Vector3Int cur = frontier.Dequeue();
            foreach (Vector3Int d in Dirs)
            {
                Vector3Int n = cur + d;
                if (cameFrom.ContainsKey(n) || !grid.IsWalkable(n) || Dist(n, homeCell) > leashRadius) continue;
                cameFrom[n] = cur;
                frontier.Enqueue(n);
            }
        }
        return cameFrom;
    }

    private List<Vector3Int> PathNextTo(Vector3Int targetCell)
    {
        Dictionary<Vector3Int, Vector3Int> reach = Reach();
        Colony colony = Colony.Instance;
        // спершу вільне місце поруч (не на іншій мурасі чи жукові), потім будь-яке
        List<Vector3Int> p = grid.PathToNeighbor(reach, Cell, targetCell,
            c => colony == null || (!colony.IsBlocked(grid, c, this) && !AntRestsOn(colony, c)));
        return p ?? grid.PathToNeighbor(reach, Cell, targetCell);
    }

    private List<Vector3Int> PathToCell(Vector3Int c)
    {
        return grid.PathTo(Reach(), Cell, c);
    }

    private bool AntRestsOn(Colony colony, Vector3Int c)
    {
        foreach (AntAgent a in colony.Ants)
            if (a != null && a.enabled && a.RestGrid == grid && a.RestCell == c) return true;
        return false;
    }

    // ---------- Службове ----------

    private bool IsAdjacent(Vector3Int c)
    {
        Vector3Int d = c - Cell;
        return Mathf.Abs(d.x) <= 1 && Mathf.Abs(d.y) <= 1;
    }

    private static float Dist(Vector3Int a, Vector3Int b)
    {
        float dx = a.x - b.x, dy = a.y - b.y;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private void IdleTurn()
    {
        idleTurnTimer -= Time.deltaTime;
        if (idleTurnTimer > 0f) return;
        idleTurnTimer = Random.Range(2.5f, 6f);
        wantRotation = Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f);
    }

    // Спрайт дивиться вгору, тому -90 (як у мурахи).
    private void FaceTowards(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        wantRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void CreateVisual()
    {
        // Корінь не крутиться (щоб смужка HP лишалась рівною); крутиться дочірній Visual.
        var v = new GameObject("Visual");
        v.transform.SetParent(transform, false);
        visual = v.transform;

        body = v.AddComponent<SpriteRenderer>();
        body.sortingOrder = sortingOrder;

        // арт beetle_0/beetle_1 з Resources/Art (2 кадри ходи), якщо спрайт не призначено вручну
        if (sprite == null && GameArt.Get("beetle_0") != null)
        {
            walkFrames = new[] { GameArt.Get("beetle_0"), GameArt.Get("beetle_1") ?? GameArt.Get("beetle_0") };
            sprite = walkFrames[0];
        }
        if (sprite != null)
        {
            body.sprite = sprite;
            body.color = Color.white;
            float s = sprite.bounds.size.y > 0f ? cell * sizeCells / sprite.bounds.size.y : 1f;
            visual.localScale = Vector3.one * s;
        }
        else
        {
            body.sprite = PlaceholderSprites.Circle;
            body.color = placeholderColor;
            visual.localScale = new Vector3(0.85f, 1f, 1f) * cell * sizeCells;

            // голова — щоб було видно, куди дивиться
            var h = new GameObject("Head");
            h.transform.SetParent(visual, false);
            h.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            h.transform.localScale = new Vector3(0.5f, 0.35f, 1f);
            head = h.AddComponent<SpriteRenderer>();
            head.sprite = PlaceholderSprites.Circle;
            head.color = new Color(0.2f, 0.1f, 0.06f);
            head.sortingOrder = sortingOrder + 1;
        }

        bar = WorldBar.Create(transform, new Vector3(0f, 0.62f * cell, 0f), 0.8f * cell, 0.08f * cell,
            new Color(0.9f, 0.25f, 0.2f), 4);
        bar.SetVisible(false);
    }

    private void UpdateVisual()
    {
        visual.localRotation = Quaternion.RotateTowards(visual.localRotation, wantRotation, turnSpeed * Time.deltaTime);

        // хода: перебираємо кадри, поки йде
        if (walkFrames != null)
        {
            bool moving = path != null && pathIndex < path.Count;
            if (moving) walkTime += Time.deltaTime * 6f * speedCells / 1.5f;
            body.sprite = walkFrames[moving ? (int)walkTime % walkFrames.Length : 0];
        }

        knock = Vector3.MoveTowards(knock, Vector3.zero, cell * Time.deltaTime);
        visual.localPosition = knock;

        Color baseColor = sprite != null ? Color.white : placeholderColor;
        if (flashTimer > 0f) flashTimer -= Time.deltaTime;
        body.color = Color.Lerp(baseColor, new Color(1f, 0.25f, 0.25f), Mathf.Clamp01(flashTimer / 0.25f));

        bool hurt = Hp < maxHp;
        bar.SetVisible(hurt);
        if (hurt) bar.Set((float)Hp / maxHp);
    }
}
