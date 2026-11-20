using System.Collections.Generic;
using UnityEngine;

// Мураха, що сама бере завдання з черги: іде до клітинки й копає її.
[RequireComponent(typeof(Rigidbody2D))]
public class AntAgent : MonoBehaviour
{
    public enum State { Idle, Move, Work }

    [Header("Стати (поки без конструктора)")]
    [Tooltip("Клітинок за секунду")]
    [SerializeField] private float speedCells = 2f;
    [Tooltip("Секунд на одну клітинку")]
    [SerializeField] private float digTime = 1f;

    [Header("Поведінка")]
    [SerializeField] private float searchInterval = 0.5f;
    [SerializeField] private float turnSpeed = 720f;

    public State CurrentState { get; private set; } = State.Idle;

    private Animator anim;
    private GridManager grid;
    private List<Vector3Int> path;
    private int pathIndex;
    private Vector3Int task;
    private bool hasTask;
    private float workTimer;
    private float searchTimer;

    void Awake()
    {
        anim = GetComponent<Animator>();
        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic; // рухаємо самі, по клітинках
        rb.linearVelocity = Vector2.zero;
    }

    void Start()
    {
        grid = GridManager.Instance;
        if (grid == null)
        {
            Debug.LogError("AntAgent: у сцені немає GridManager");
            enabled = false;
            return;
        }
        transform.position = grid.CellCenter(grid.WorldToCell(transform.position));
    }

    void OnDisable()
    {
        DropTask();
    }

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

    private void UpdateIdle()
    {
        searchTimer -= Time.deltaTime;
        if (searchTimer > 0f) return;
        searchTimer = searchInterval;

        Vector3Int myCell = grid.WorldToCell(transform.position);
        if (grid.DigQueue.TryTake(this, grid, myCell, out task, out path))
        {
            hasTask = true;
            pathIndex = 0;
            CurrentState = path.Count > 0 ? State.Move : State.Work;
            workTimer = 0f;
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
            if (pathIndex >= path.Count)
            {
                CurrentState = State.Work;
                workTimer = 0f;
            }
        }
    }

    private void UpdateWork()
    {
        if (!TaskStillValid()) { DropTask(); return; }

        FaceTowards(grid.CellCenter(task));
        workTimer += Time.deltaTime;
        if (workTimer < digTime) return;

        grid.Dig(task); // сам прибирає клітинку з черги
        hasTask = false;
        CurrentState = State.Idle;
        searchTimer = 0f;
    }

    // Гравець міг зняти позначку, поки мураха йшла.
    private bool TaskStillValid()
    {
        return hasTask && grid.DigQueue.IsTakenBy(task, this) && grid.HasWall(task);
    }

    private void DropTask()
    {
        if (hasTask && grid != null) grid.DigQueue.Release(task, this);
        hasTask = false;
        path = null;
        CurrentState = State.Idle;
        searchTimer = 0f;
        // докручуємо до центру клітинки, щоб не застрягти між клітинками
        if (grid != null) transform.position = grid.CellCenter(grid.WorldToCell(transform.position));
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
