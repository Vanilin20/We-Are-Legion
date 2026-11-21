using System.Collections.Generic;
using UnityEngine;

// Мураха: бере завдання з черги своєї групи, іде до місця (можливо, через вхід в іншу локацію) і працює.
// Ручне завдання від гравця виконує першим, поза чергою.
// Їсть зі складу; голодна втрачає здоров'я; на нулі гине, вантаж падає на землю.
[RequireComponent(typeof(Rigidbody2D))]
public class AntAgent : MonoBehaviour
{
    public enum State { Idle, Move, Work }

    [Header("Група")]
    [SerializeField] private AntGroup group = AntGroup.Builder;

    [Header("Стати (поки без конструктора)")]
    [SerializeField] private int maxHp = 4;
    [Tooltip("Клітинок за секунду")]
    [SerializeField] private float speedCells = 2f;
    [Tooltip("Секунд на одну клітинку")]
    [SerializeField] private float digTime = 1f;
    [Tooltip("Скільки предметів несе за раз")]
    [SerializeField] private int carryCapacity = 2;

    [Header("Голод")]
    [Tooltip("Раз на скільки секунд мураха з'їдає 1 їжу")]
    [SerializeField] private float eatInterval = 60f;
    [Tooltip("Раз на скільки секунд голодна мураха втрачає 1 HP")]
    [SerializeField] private float starveInterval = 20f;

    [Header("Поведінка")]
    [SerializeField] private float searchInterval = 0.5f;
    [SerializeField] private float turnSpeed = 720f;
    [Tooltip("Як далеко шукати вільну клітинку, щоб відійти від іншої мурахи")]
    [SerializeField] private int sidestepRange = 6;
    [Tooltip("Поки руки не повні — підібрати ще їжу, якщо вона не далі стількох клітинок")]
    [SerializeField] private int extraPickupRange = 8;

    public AntGroup Group => group;
    public int MaxHp => maxHp;
    public int Hp { get; private set; }
    public bool Hungry { get; private set; }
    public float DigTime => digTime;
    public State CurrentState { get; private set; } = State.Idle;
    public AntTask CurrentTask => task;
    public bool IsManual { get; private set; }
    public GridManager Grid => grid;

    // Вантаж: кожен предмет — скільки їжі в ньому (крихта 1, м'ясо 8).
    private readonly List<int> cargo = new List<int>();
    public int CargoCount => cargo.Count;
    public int CarryCapacity => carryCapacity;
    public bool CanCarryMore => cargo.Count < carryCapacity;

    private Animator anim;
    private SpriteRenderer body;
    private GridManager grid;
    private Colony colony;
    private AntStatusView status;

    private AntTask task;
    private TaskQueue taskQueue; // з якої черги завдання; null — своє (відійти, віднести)
    private Route route;
    private int legIndex;
    private int pathIndex;
    private float workTimer;
    private float searchTimer;
    private float eatTimer;
    private float flashTimer;
    private bool dead;

    void Awake()
    {
        anim = GetComponent<Animator>();
        body = GetComponent<SpriteRenderer>();
        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic; // рухаємо самі, по клітинках
        rb.linearVelocity = Vector2.zero;
        Hp = maxHp;
        eatTimer = Random.Range(eatInterval * 0.5f, eatInterval); // щоб не їли всі разом
    }

    void Start()
    {
        grid = GridManager.At(transform.position) ?? GridManager.Instance;
        colony = Colony.Instance;
        if (grid == null || colony == null)
        {
            Debug.LogError("AntAgent: у сцені немає GridManager або Colony");
            enabled = false;
            return;
        }
        colony.Register(this);
        transform.position = grid.CellCenter(MyCell);
        status = AntStatusView.Create(this);
    }

    void OnDestroy()
    {
        if (colony != null) colony.Unregister(this);
        if (status != null) Destroy(status.gameObject);
    }

    void OnDisable()
    {
        DropTask();
    }

    private Vector3Int MyCell => grid.WorldToCell(transform.position);

    // Клітинка, яку мураха займає: де стоїть/працює, а в русі — куди йде.
    public GridManager RestGrid => CurrentState == State.Move && route != null ? route.FinalGrid : grid;

    public Vector3Int RestCell
    {
        get
        {
            if (grid == null) return new Vector3Int(int.MinValue, int.MinValue, 0);
            if (CurrentState == State.Move && route != null) return route.FinalCell(MyCell);
            return MyCell;
        }
    }

    private bool IsFree(GridManager g, Vector3Int cell) => !colony.IsReservedByOther(g, cell, this);

    void Update()
    {
        if (dead) return;

        UpdateHunger();

        switch (CurrentState)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Move: UpdateMove(); break;
            case State.Work: UpdateWork(); break;
        }

        if (anim != null) anim.SetBool("IsRunning", CurrentState == State.Move);
        UpdateFlash();
    }

    // ---------- Команди ----------

    // Ручне завдання від гравця. queue — черга, в якій лежить завдання (або null).
    // false — туди не дійти.
    public bool AssignManual(AntTask newTask, TaskQueue queue)
    {
        if (grid == null || dead || newTask == null || !newTask.IsValid(this)) return false;

        var router = new Router();
        // спершу вільне місце; якщо всі зайняті — однаково йдемо (наказ гравця)
        Route r = router.Find(grid, MyCell, newTask, IsFree) ?? router.Find(grid, MyCell, newTask, null);
        if (r == null) return false;

        DropTask();

        if (queue != null)
        {
            AntAgent owner = queue.Owner(newTask);
            if (owner != null && owner != this) owner.CancelTask();
            queue.Claim(newTask, this);
        }

        Begin(newTask, queue, r, manual: true);
        return true;
    }

    public void CancelTask() => DropTask();

    // ---------- Вантаж ----------

    public void AddCargo(int amount)
    {
        if (amount > 0) cargo.Add(amount);
    }

    public int UnloadCargo()
    {
        int sum = 0;
        foreach (int a in cargo) sum += a;
        cargo.Clear();
        return sum;
    }

    // ---------- Здоров'я ----------

    public void Damage(int amount)
    {
        if (dead || amount <= 0) return;
        Hp = Mathf.Max(0, Hp - amount);
        flashTimer = 0.25f;
        if (Hp == 0) Die();
    }

    private void Die()
    {
        dead = true;
        DropTask();

        // вантаж падає на землю
        Vector3Int cell = MyCell;
        foreach (int amount in cargo)
            FoodItem.Spawn(grid, cell, amount, amount > 1 ? "Meat" : "Crumb");
        cargo.Clear();

        if (AntSelection.Instance != null && AntSelection.Instance.Selected == this)
            AntSelection.Instance.Deselect();

        Destroy(gameObject);
    }

    private void UpdateHunger()
    {
        eatTimer -= Time.deltaTime;
        if (eatTimer > 0f) return;

        Storage storage = Storage.Instance;
        if (storage != null && storage.TryTake(1))
        {
            Hungry = false;
            eatTimer = eatInterval;
            return;
        }

        if (Hungry) Damage(1);  // уже голодна — шкода
        Hungry = true;
        eatTimer = starveInterval; // наступна спроба поїсти (або шкода) через 20 с
    }

    private void UpdateFlash()
    {
        if (body == null) return;
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            body.color = Color.Lerp(Color.white, Color.red, flashTimer / 0.25f);
        }
        else if (body.color != Color.white) body.color = Color.white;
    }

    // ---------- Стани ----------

    private void UpdateIdle()
    {
        searchTimer -= Time.deltaTime;
        if (searchTimer > 0f) return;
        searchTimer = searchInterval;

        TaskQueue queue = colony.Queue(group);

        if (cargo.Count > 0)
        {
            // руки не повні — підбираємо ще, якщо поруч
            if (CanCarryMore && queue.TryTake(this, grid, MyCell, out AntTask more, out Route moreRoute, extraPickupRange))
            {
                Begin(more, queue, moreRoute, manual: false);
                return;
            }
            if (TryBeginDeliver()) return;
        }
        else if (queue.TryTake(this, grid, MyCell, out AntTask t, out Route r))
        {
            Begin(t, queue, r, manual: false);
            return;
        }

        // Без роботи й стоїмо на клітинці іншої мурахи — відходимо вбік.
        if (colony.IsReservedByOther(grid, MyCell, this))
        {
            List<Vector3Int> side = grid.PathToNearestFree(MyCell, c => IsFree(grid, c), sidestepRange);
            if (side != null)
                Begin(new MoveTask(grid, side[side.Count - 1]), null, Router.Single(grid, side), manual: false);
        }
    }

    private bool TryBeginDeliver()
    {
        Storage storage = Storage.Instance;
        if (storage == null || storage.Grid == null) return false;
        var deliver = new DeliverTask(storage);
        Route r = new Router().Find(grid, MyCell, deliver, IsFree) ?? new Router().Find(grid, MyCell, deliver, null);
        if (r == null) return false;
        Begin(deliver, null, r, manual: false);
        return true;
    }

    private void UpdateMove()
    {
        if (!TaskStillValid()) { DropTask(); return; }

        Route.Leg leg = route.Legs[legIndex];
        if (pathIndex >= leg.Path.Count) { EndOfLeg(); return; }

        Vector3Int next = leg.Path[pathIndex];
        if (!grid.IsWalkable(next)) { DropTask(); return; }

        Vector3 target = grid.CellCenter(next);
        FaceTowards(target);
        float step = speedCells * grid.CellWorldSize * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, target, step);

        if ((transform.position - target).sqrMagnitude < 0.0001f)
        {
            pathIndex++;
            if (pathIndex >= leg.Path.Count) EndOfLeg();
        }
    }

    // Кінець відрізка: або перехід через вхід, або місце роботи.
    private void EndOfLeg()
    {
        Route.Leg leg = route.Legs[legIndex];
        if (leg.ExitPortal != null)
        {
            Portal exit = leg.ExitPortal.Linked;
            if (exit == null) { DropTask(); return; }
            grid = exit.Grid;
            transform.position = grid.CellCenter(exit.Cell);
            SceneUtil.MoveToSceneOf(gameObject, grid.gameObject);
            legIndex++;
            pathIndex = 0;
            if (route.Legs[legIndex].Path.Count == 0) StartWork();
            return;
        }
        StartWork();
    }

    private void UpdateWork()
    {
        if (!TaskStillValid()) { DropTask(); return; }

        if (task.StandNextTo) FaceTowards(grid.CellCenter(task.Cell));
        workTimer += Time.deltaTime;
        if (workTimer < task.WorkTime(this)) return;

        AntTask done = task;
        TaskQueue doneQueue = taskQueue;
        ClearTask();
        done.Complete(this);
        if (doneQueue != null) doneQueue.Remove(done);
        searchTimer = 0f; // одразу шукаємо далі (наприклад, віднести вантаж)
    }

    // ---------- Службове ----------

    private void Begin(AntTask t, TaskQueue queue, Route r, bool manual)
    {
        task = t;
        taskQueue = queue;
        route = r;
        legIndex = 0;
        pathIndex = 0;
        IsManual = manual;
        if (r.Legs.Count == 1 && r.Legs[0].Path.Count == 0) StartWork();
        else CurrentState = State.Move;
    }

    private void StartWork()
    {
        CurrentState = State.Work;
        workTimer = 0f;
    }

    // Гравець міг зняти позначку або віддати завдання іншій мурахі.
    private bool TaskStillValid()
    {
        if (task == null || !task.IsValid(this)) return false;
        return taskQueue == null || taskQueue.IsTakenBy(task, this);
    }

    private void DropTask()
    {
        if (task != null && taskQueue != null) taskQueue.Release(task, this);
        bool hadTask = task != null;
        ClearTask();
        // докручуємо до центру клітинки, щоб не застрягти між клітинками
        if (hadTask && grid != null) transform.position = grid.CellCenter(MyCell);
    }

    private void ClearTask()
    {
        task = null;
        taskQueue = null;
        route = null;
        IsManual = false;
        CurrentState = State.Idle;
        searchTimer = 0f;
    }

    // Спрайт мурахи дивиться вгору, тому -90 (як у PlayerRotation).
    private void FaceTowards(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        Quaternion want = Quaternion.Euler(0f, 0f, angle);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
    }
}
