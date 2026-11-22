using System.Collections.Generic;
using UnityEngine;

// Королева: приймає замовлення з конструктора й відкладає яйця.
// Їжа знімається одразу при замовленні; яйце — не частіше ніж раз на layInterval,
// мураха виходить через hatchTime. Не рухається, не входить у групи й не рахується в поразці.
public class AntQueen : MonoBehaviour, ICellBlocker
{
    public static AntQueen Instance { get; private set; }

    public enum OrderResult { Ok, NotEnoughFood, Limit, NotReady }

    private struct Order
    {
        public AntBuild Build;
        public AntGroup Group;
    }

    [Header("Яйця")]
    [Tooltip("Яйце не частіше ніж раз на стільки секунд")]
    [SerializeField] private float layInterval = 20f;
    [Tooltip("Через скільки секунд з яйця виходить мураха")]
    [SerializeField] private float hatchTime = 30f;
    [Tooltip("Найбільше мурах у колонії (разом з яйцями й чергою)")]
    [SerializeField] private int maxAnts = 50;
    [Tooltip("Як далеко від королеви шукати вільне місце для яйця")]
    [SerializeField] private int eggRange = 6;

    [Header("Мураха")]
    [Tooltip("Необов'язково. Порожньо — копіюємо мураху, що вже є в сцені. " +
             "Не став сюди Prefabs/ANT: на ньому старий Player (DontDestroyOnLoad).")]
    [SerializeField] private AntAgent antPrefab;

    [Header("Заглушка (поки немає спрайта)")]
    [SerializeField] private Color placeholderColor = new Color(0.62f, 0.32f, 0.78f);
    [SerializeField] private float placeholderSizeCells = 1.3f;

    private readonly Queue<Order> orders = new Queue<Order>();
    private readonly List<AntEgg> eggs = new List<AntEgg>();
    private float layCooldown;
    private GameObject template;
    private int hatched;

    public GridManager Grid { get; private set; }
    public Vector3Int Cell { get; private set; }
    GridManager ICellBlocker.BlockGrid => Grid;
    Vector3Int ICellBlocker.BlockCell => Cell;

    public int MaxAnts => maxAnts;
    public int OrdersCount => orders.Count;
    public int EggsCount => eggs.Count;
    public float HatchTime => hatchTime;
    // Скільки секунд до наступного яйця (0 — можна хоч зараз).
    public float LayCooldown => Mathf.Max(0f, layCooldown);

    // Живі мурахи + яйця + замовлення, що чекають.
    public int Population
    {
        get
        {
            int ants = Colony.Instance != null ? Colony.Instance.Ants.Count : 0;
            return ants + eggs.Count + orders.Count;
        }
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Grid = GridManager.At(transform.position) ?? GridManager.Instance;
        if (Grid == null || Colony.Instance == null)
        {
            Debug.LogError("AntQueen: у сцені немає GridManager або Colony");
            enabled = false;
            return;
        }

        Vector3Int cell = Grid.WorldToCell(transform.position);
        if (!Grid.TryNearestWalkable(cell, 10, out Vector3Int free))
            Debug.LogWarning("AntQueen: поруч немає розкопаної клітинки — постав королеву в кімнату");
        Cell = free;
        transform.position = Grid.CellCenter(Cell);
        PlaceholderSprites.Ensure(gameObject, true, placeholderColor, Grid.CellWorldSize * placeholderSizeCells, 1);

        Colony.Instance.AddBlocker(this);
        PrepareTemplate();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (Colony.Instance != null) Colony.Instance.RemoveBlocker(this);
    }

    // ---------- Замовлення ----------

    public OrderResult CanOrder(AntBuild build)
    {
        if (!enabled || Grid == null || Storage.Instance == null) return OrderResult.NotReady;
        if (Population >= maxAnts) return OrderResult.Limit;
        if (Storage.Instance.Food < build.Cost) return OrderResult.NotEnoughFood;
        return OrderResult.Ok;
    }

    // Їжа знімається одразу; мураха вийде з яйця, коли до неї дійде черга.
    public OrderResult PlaceOrder(AntBuild build, AntGroup group)
    {
        OrderResult r = CanOrder(build);
        if (r != OrderResult.Ok) return r;
        if (!Storage.Instance.TryTake(build.Cost)) return OrderResult.NotEnoughFood;
        orders.Enqueue(new Order { Build = build, Group = group });
        return OrderResult.Ok;
    }

    // ---------- Яйця ----------

    void Update()
    {
        if (layCooldown > 0f) layCooldown -= Time.deltaTime;
        if (layCooldown > 0f || orders.Count == 0) return;

        if (TryLay(orders.Peek()))
        {
            orders.Dequeue();
            layCooldown = layInterval;
        }
        else
        {
            layCooldown = 1f; // немає місця для яйця — спробуємо трохи згодом
        }
    }

    private bool TryLay(Order order)
    {
        Colony colony = Colony.Instance;
        List<Vector3Int> path = Grid.PathToNearestFree(Cell, c => !colony.IsReservedByOther(Grid, c, null), eggRange);
        if (path == null || path.Count == 0) return false;

        AntEgg egg = AntEgg.Spawn(this, Grid, path[path.Count - 1], order.Build, order.Group, hatchTime);
        eggs.Add(egg);
        Debug.Log("Королева відклала яйце: " + order.Group + " (вилупиться через " + hatchTime + " с)");
        return true;
    }

    // Викликає яйце, коли дозріло.
    public void Hatch(AntEgg egg)
    {
        eggs.Remove(egg);
        AntAgent ant = SpawnAnt(egg.Build, egg.Group, egg.Cell);
        if (ant == null) Debug.LogError("AntQueen: не вдалося створити мураху — у сцені немає жодної мурахи для копії, признач antPrefab");
    }

    public void ForgetEgg(AntEgg egg) => eggs.Remove(egg);

    private AntAgent SpawnAnt(AntBuild build, AntGroup group, Vector3Int cell)
    {
        GameObject source = antPrefab != null ? antPrefab.gameObject : PrepareTemplate();
        if (source == null) return null;

        GameObject go = Instantiate(source, Grid.CellCenter(cell), Quaternion.identity);
        SceneUtil.MoveToSceneOf(go, Grid.gameObject);
        hatched++;
        go.name = "Ant " + group + " " + hatched;

        AntAgent ant = go.GetComponent<AntAgent>();
        ant.ApplyBuild(build, group); // до Start: стати й HP із конструктора
        return ant;
    }

    // Прихована копія мурахи зі сцени: з неї робимо нових (Awake у копії не запускається, бо батько вимкнений).
    private GameObject PrepareTemplate()
    {
        if (template != null) return template;

        AntAgent src = null;
        if (Colony.Instance != null)
            foreach (AntAgent a in Colony.Instance.Ants)
                if (a != null) { src = a; break; }
        if (src == null) src = FindFirstObjectByType<AntAgent>();
        if (src == null) return null;

        var holder = new GameObject("AntTemplate (hidden)");
        holder.SetActive(false);
        SceneUtil.MoveToSceneOf(holder, gameObject);
        template = Instantiate(src.gameObject, holder.transform);
        template.name = "Ant";
        template.transform.localRotation = Quaternion.identity;
        // Мітки кольорів створює сама мураха в Start — у копії їх бути не має.
        foreach (AntLook look in template.GetComponentsInChildren<AntLook>(true))
            Destroy(look.gameObject);
        return template;
    }
}
