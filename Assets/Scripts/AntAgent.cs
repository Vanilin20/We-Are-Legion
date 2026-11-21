using System.Collections.Generic;
using UnityEngine;

// Мураха: бере завдання з черги своєї групи, іде до місця й працює.
// Ручне завдання від гравця виконує першим, поза чергою.
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

    [Header("Поведінка")]
    [SerializeField] private float searchInterval = 0.5f;
    [SerializeField] private float turnSpeed = 720f;
    [Tooltip("Як далеко шукати вільну клітинку, щоб відійти від іншої мурахи")]
    [SerializeField] private int sidestepRange = 6;

    public AntGroup Group => group;
    public int MaxHp => maxHp;
    public int Hp { get; private set; }
    public float DigTime => digTime;
    public State CurrentState { get; private set; } = State.Idle;
    public AntTask CurrentTask => task;
    public bool IsManual { get; private set; }

    private Animator anim;
    private GridManager grid;
    private Colony colony;

    private AntTask task;
    private TaskQueue taskQueue; // з якої черги завдання; null — ручний перехід
    private List<Vector3Int> path;
    private int pathIndex;
    private float workTimer;
    private float searchTimer;

    void Awake()
    {
        anim = GetComponent<Animator>();
        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic; // рухаємо самі, по клітинках
        rb.linearVelocity = Vector2.zero;
        Hp = maxHp;
    }

    void Start()
    {
        grid = GridManager.Instance;
        colony = Colony.Instance;
        if (grid == null || colony == null)
        {
            Debug.LogError("AntAgent: у сцені немає GridManager або Colony");
            enabled = false;
            return;
        }
        colony.Register(this);
        transform.position = grid.CellCenter(MyCell);
    }

    void OnDestroy()
    {
        if (colony != null) colony.Unregister(this);
    }

    void OnDisable()
    {
        DropTask();
    }

    private Vector3Int MyCell => grid.WorldToCell(transform.position);

    // Клітинка, яку мураха займає: де стоїть/працює, а в русі — куди йде.
    public Vector3Int RestCell
    {
        get
        {
            if (grid == null) return new Vector3Int(int.MinValue, int.MinValue, 0);
            if (CurrentState == State.Move && path != null && path.Count > 0) return path[path.Count - 1];
            return MyCell;
        }
    }

    private bool IsFreeForMe(Vector3Int cell) => !colony.IsReservedByOther(cell, this);

    void Update()
    {
        switch (CurrentState)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Move: UpdateMove(); break;
            case State.Work: UpdateWork(); break;
        }

        if (anim != null) anim.SetBool("IsRunning", CurrentState == State.Move);
    }

    // ---------- Команди ----------

    // Ручне завдання від гравця. queue — черга, в якій лежить завдання (або null).
    // false — туди не дійти.
    public bool AssignManual(AntTask newTask, TaskQueue queue)
    {
        if (grid == null || newTask == null || !newTask.IsValid(grid)) return false;

        var reach = grid.BuildReachMap(MyCell);
        // спершу вільне місце; якщо всі зайняті — однаково йдемо (наказ гравця)
        List<Vector3Int> newPath = grid.PathFor(reach, MyCell, newTask, IsFreeForMe)
                                   ?? grid.PathFor(reach, MyCell, newTask);
        if (newPath == null) return false;

        DropTask();

        if (queue != null)
        {
            AntAgent owner = queue.Owner(newTask);
            if (owner != null && owner != this) owner.CancelTask();
            queue.Claim(newTask, this);
        }

        Begin(newTask, queue, newPath, manual: true);
        return true;
    }

    public void CancelTask() => DropTask();

    // ---------- Стани ----------

    private void UpdateIdle()
    {
        searchTimer -= Time.deltaTime;
        if (searchTimer > 0f) return;
        searchTimer = searchInterval;

        TaskQueue queue = colony.Queue(group);
        if (queue.TryTake(this, grid, MyCell, out AntTask t, out List<Vector3Int> p))
        {
            Begin(t, queue, p, manual: false);
            return;
        }

        // Без роботи й стоїмо на клітинці іншої мурахи — відходимо вбік.
        if (colony.IsReservedByOther(MyCell, this))
        {
            List<Vector3Int> side = grid.PathToNearestFree(MyCell, IsFreeForMe, sidestepRange);
            if (side != null) Begin(new MoveTask(side[side.Count - 1]), null, side, manual: false);
        }
    }

    private void UpdateMove()
    {
        if (!TaskStillValid()) { DropTask(); return; }

        Vector3Int next = path[pathIndex];
        if (!grid.IsWalkable(next)) { DropTask(); return; }

        Vector3 target = grid.CellCenter(next);
        FaceTowards(target);
        float step = speedCells * grid.CellWorldSize * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, target, step);

        if ((transform.position - target).sqrMagnitude < 0.0001f)
        {
            pathIndex++;
            if (pathIndex >= path.Count) StartWork();
        }
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
        done.Complete(this, grid);
        if (doneQueue != null) doneQueue.Remove(done);
    }

    // ---------- Службове ----------

    private void Begin(AntTask t, TaskQueue queue, List<Vector3Int> p, bool manual)
    {
        task = t;
        taskQueue = queue;
        path = p;
        pathIndex = 0;
        IsManual = manual;
        if (path.Count > 0) CurrentState = State.Move;
        else StartWork();
    }

    private void StartWork()
    {
        CurrentState = State.Work;
        workTimer = 0f;
    }

    // Гравець міг зняти позначку або віддати завдання іншій мурахі.
    private bool TaskStillValid()
    {
        if (task == null || !task.IsValid(grid)) return false;
        return taskQueue == null || taskQueue.IsTakenBy(task, this);
    }

    private void DropTask()
    {
        if (task != null && taskQueue != null) taskQueue.Release(task, this);
        bool wasMoving = task != null;
        ClearTask();
        // докручуємо до центру клітинки, щоб не застрягти між клітинками
        if (wasMoving && grid != null) transform.position = grid.CellCenter(MyCell);
    }

    private void ClearTask()
    {
        task = null;
        taskQueue = null;
        path = null;
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
