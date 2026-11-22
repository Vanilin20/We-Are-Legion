using System.Collections.Generic;
using UnityEngine;

// Мураха: бере завдання з черги своєї групи, іде до місця (можливо, через вхід в іншу локацію) і працює.
// Ручне завдання від гравця виконує першим, поза чергою.
// Стати беруться зі збірки конструктора (ApplyBuild); мурахи, поставлені в сцену, — базові зі статами з інспектора.
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
    [Tooltip("Шкода за удар")]
    [SerializeField] private int damage = 1;
    [Tooltip("Секунд між ударами (у GDD усі мурахи б'ють раз на 1 с)")]
    [SerializeField] private float attackInterval = 1f;

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
    public float SpeedCells => speedCells;
    public int AttackDamage => damage;
    public float AttackInterval => attackInterval;
    public AntBuild Build { get; private set; }
    public State CurrentState { get; private set; } = State.Idle;
    public AntTask CurrentTask => task;
    public bool IsManual { get; private set; }
    public GridManager Grid => grid;
    public bool IsAlive => !dead && enabled;
    // Клітинка, де мураха зараз (у русі — найближча).
    public Vector3Int CurrentCell => grid != null ? MyCell : new Vector3Int(int.MinValue, int.MinValue, 0);

    // Вантаж: кожен предмет — скільки їжі в ньому (крихта 1, м'ясо 8).
    private readonly List<int> cargo = new List<int>();
    public int CargoCount => cargo.Count;
    public int CarryCapacity => carryCapacity;
    public bool CanCarryMore => cargo.Count < carryCapacity;
    public int CargoAmountAt(int i) => cargo[i];

    private Animator anim;
    private SpriteRenderer body;
    private GridManager grid;
    private Colony colony;
    private AntStatusView status;
    private AntLook look;

    private AntTask task;
    private TaskQueue taskQueue; // з якої черги завдання; null — своє (відійти, віднести)
    private Route route;
    private int legIndex;
    private int pathIndex;
    private float workTimer;
    private float searchTimer;
    private float eatTimer;
    private float flashTimer;
    private float repeatCooldown; // до наступного удару (не скидається, коли мураха доганяє ціль)
    private bool dead;
    // Куди вела ціль, коли будували маршрут (для рухомих цілей).
    private GridManager routedGrid;
    private Vector3Int routedCell;

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
        if (Build == null) Build = AntBuild.Basic;
        look = AntLook.Attach(this);
        look.Apply(Build);
        status = AntStatusView.Create(this);
    }

    // Стати й група з конструктора. Викликати одразу після Instantiate (до Start).
    public void ApplyBuild(AntBuild build, AntGroup newGroup)
    {
        Build = build;
        group = newGroup;
        maxHp = build.Hp;
        speedCells = build.Speed;
        digTime = build.DigTime;
        carryCapacity = build.Carry;
        damage = build.Damage;
        Hp = maxHp;
        if (look != null) look.Apply(build);
    }

    void OnDestroy()
    {
        if (colony != null) colony.Unregister(this);
        if (status != null) Destroy(status.gameObject);
    }

    void OnDisable()
    {
        // При вивантаженні сцени сітка вже може бути знищена — не вирівнюємось по клітинці.
        DropTask(snap: false);
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
        if (repeatCooldown > 0f) repeatCooldown -= Time.deltaTime;

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

    // source — хто вдарив (для журналу); null — голод.
    public void Damage(int amount, Object source = null)
    {
        if (dead || amount <= 0) return;
        Hp = Mathf.Max(0, Hp - amount);
        flashTimer = 0.25f;
        if (Hp == 0) { Die(source); return; }

        // Воїн без наказу гравця дає здачі жуку, що його б'є.
        if (source is EnemyBeetle beetle && group == AntGroup.Warrior && !IsManual && !(task is AttackTask))
        {
            var attack = new AttackTask(beetle);
            var router = new Router();
            Route r = router.Find(grid, MyCell, attack, IsFree) ?? router.Find(grid, MyCell, attack, null);
            if (r != null)
            {
                DropTask();
                Begin(attack, null, r, manual: false);
            }
        }
    }

    private void Die(Object killer)
    {
        dead = true;
        Debug.Log(name + " (" + group + ") загинула: " + (killer != null ? killer.name : "голод"));
        DropTask();

        // вантаж падає на землю
        Vector3Int cell = MyCell;
        foreach (int amount in cargo)
            FoodItem.Spawn(grid, cell, amount, amount > 1 ? "Meat" : "Crumb");
        cargo.Clear();

        if (AntSelection.Instance != null) AntSelection.Instance.Remove(this);

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
        float k = 0f;
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            k = Mathf.Clamp01(flashTimer / 0.25f);
        }
        if (body != null) body.color = Color.Lerp(Color.white, Color.red, k);
        if (look != null) look.SetFlash(k);
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
            else if (TargetShifted() && legIndex == route.Legs.Count - 1 && !Reroute()) DropTask();
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

        // Ціль відійшла — доганяємо.
        if (task.TargetMoves && !NextToTarget())
        {
            if (!Reroute()) DropTask();
            return;
        }

        if (task.StandNextTo) FaceTowards(grid.CellCenter(task.Cell));

        // Удари: перший — одразу, далі раз на WorkTime (як у GDD: «б'ють раз на 1 с»).
        if (task.Repeating)
        {
            if (repeatCooldown > 0f) return;
            repeatCooldown = task.WorkTime(this);
            task.Complete(this); // чи ціль ще жива — перевіримо наступного кадру
            return;
        }

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
        IsManual = manual;
        UseRoute(r);
    }

    private void UseRoute(Route r)
    {
        route = r;
        legIndex = 0;
        pathIndex = 0;
        routedGrid = task.Grid;
        routedCell = task.Cell;
        if (r.Legs.Count == 1 && r.Legs[0].Path.Count == 0) StartWork();
        else CurrentState = State.Move;
    }

    // Новий маршрут до того самого завдання (ціль зрушила). false — туди вже не дійти.
    private bool Reroute()
    {
        var router = new Router();
        Route r = router.Find(grid, MyCell, task, IsFree) ?? router.Find(grid, MyCell, task, null);
        if (r == null) return false;
        UseRoute(r);
        return true;
    }

    private bool TargetShifted()
    {
        return task != null && task.TargetMoves && (task.Grid != routedGrid || task.Cell != routedCell);
    }

    // Стоїмо впритул до цілі (по сторонах або навскіс).
    private bool NextToTarget()
    {
        if (task.Grid != grid) return false;
        Vector3Int d = task.Cell - MyCell;
        return Mathf.Abs(d.x) <= 1 && Mathf.Abs(d.y) <= 1;
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

    private void DropTask(bool snap = true)
    {
        if (task != null && taskQueue != null) taskQueue.Release(task, this);
        bool hadTask = task != null;
        ClearTask();
        // докручуємо до центру клітинки, щоб не застрягти між клітинками
        if (snap && hadTask && grid != null) transform.position = grid.CellCenter(MyCell);
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
