using System.Collections.Generic;
using UnityEngine;

// Те, що займає клітинку, але не є мурахою: королева, яйце, жук.
// Мурахи можуть проходити крізь таку клітинку, але не зупиняються на ній.
public interface ICellBlocker
{
    GridManager BlockGrid { get; }
    Vector3Int BlockCell { get; }
}

// Колонія: усі мурахи й черги завдань по групах. Стежить за поразкою.
public class Colony : MonoBehaviour
{
    public static Colony Instance { get; private set; }

    [Tooltip("Що показати, коли не лишилось жодної мурахи")]
    [SerializeField] private GameOverScreen gameOverScreen;

    private readonly Dictionary<AntGroup, TaskQueue> queues = new Dictionary<AntGroup, TaskQueue>();
    private readonly List<AntAgent> ants = new List<AntAgent>();
    private readonly List<ICellBlocker> blockers = new List<ICellBlocker>();
    private bool hadAnts;
    private bool gameOver;

    public IReadOnlyList<AntAgent> Ants => ants;
    public bool IsGameOver => gameOver;

    void Awake()
    {
        Instance = this;
        foreach (AntGroup g in System.Enum.GetValues(typeof(AntGroup)))
            queues[g] = new TaskQueue();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Поразка: у колонії не лишилось жодної мурахи (королева не рахується).
        if (hadAnts && !gameOver && ants.Count == 0)
        {
            gameOver = true;
            if (gameOverScreen != null) gameOverScreen.Show();
            else Debug.Log("Колонія загинула");
        }
    }

    public TaskQueue Queue(AntGroup group) => queues[group];

    public void Register(AntAgent ant)
    {
        if (!ants.Contains(ant)) ants.Add(ant);
        hadAnts = true;
    }

    public void Unregister(AntAgent ant) => ants.Remove(ant);

    public void AddBlocker(ICellBlocker b)
    {
        if (b != null && !blockers.Contains(b)) blockers.Add(b);
    }

    public void RemoveBlocker(ICellBlocker b) => blockers.Remove(b);

    // Чи стоїть на клітинці королева, яйце або жук (крім ignore).
    public bool IsBlocked(GridManager grid, Vector3Int cell, ICellBlocker ignore = null)
    {
        foreach (ICellBlocker b in blockers)
            if (b != ignore && b.BlockGrid == grid && b.BlockCell == cell) return true;
        return false;
    }

    // Чи зайнята клітинка іншою мурахою (вона там стоїть, працює або вже туди йде).
    // Мурахи в русі можуть проходити одна крізь одну — займають лише місце, де зупиняться.
    // Королева, яйця й жуки теж займають свої клітинки.
    public bool IsReservedByOther(GridManager grid, Vector3Int cell, AntAgent me)
    {
        foreach (AntAgent a in ants)
            if (a != me && a.enabled && a.RestGrid == grid && a.RestCell == cell) return true;
        return IsBlocked(grid, cell);
    }

    public int CountIn(AntGroup group)
    {
        int n = 0;
        foreach (AntAgent a in ants) if (a.Group == group) n++;
        return n;
    }
}
