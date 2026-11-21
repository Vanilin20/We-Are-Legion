using System.Collections.Generic;
using UnityEngine;

// Їжа, що лежить на клітинці: крихта (+1) або м'ясо жука (+8).
// Сама ставить завдання «підібрати» в чергу робітників.
public class FoodItem : MonoBehaviour
{
    private static readonly List<FoodItem> all = new List<FoodItem>();

    [SerializeField] private int amount = 1;
    [SerializeField] private Color placeholderColor = new Color(0.95f, 0.85f, 0.45f);
    [SerializeField] private float placeholderSizeCells = 0.3f;

    public int Amount => amount;
    public GridManager Grid { get; private set; }
    public Vector3Int Cell { get; private set; }
    public PickupTask Task { get; private set; }

    // Викликається, коли їжу підібрали (для відродження крихт).
    public event System.Action<FoodItem> PickedUp;

    public static FoodItem Spawn(GridManager grid, Vector3Int cell, int amount, string name = "Food")
    {
        var go = new GameObject(name);
        go.transform.position = grid.CellCenter(cell);
        SceneUtil.MoveToSceneOf(go, grid.gameObject);
        var f = go.AddComponent<FoodItem>();
        f.amount = amount;
        if (amount > 1) f.placeholderColor = new Color(0.8f, 0.25f, 0.25f); // м'ясо
        return f;
    }

    public static FoodItem At(GridManager grid, Vector3Int cell)
    {
        foreach (FoodItem f in all)
            if (f.Grid == grid && f.Cell == cell) return f;
        return null;
    }

    void Start()
    {
        Grid = GridManager.At(transform.position);
        if (Grid == null || Colony.Instance == null)
        {
            Debug.LogWarning("FoodItem " + name + ": не знайшов сітку або колонію");
            enabled = false;
            return;
        }

        Cell = Grid.WorldToCell(transform.position);
        transform.position = Grid.CellCenter(Cell);
        float size = Grid.CellWorldSize * placeholderSizeCells * (amount > 1 ? 1.6f : 1f);
        PlaceholderSprites.Ensure(gameObject, true, placeholderColor, size, 1);

        all.Add(this);
        Task = new PickupTask(this);
        Colony.Instance.Queue(AntGroup.Worker).Add(Task);
    }

    // Підібрати: повертає, скільки їжі.
    public int Take()
    {
        PickedUp?.Invoke(this);
        Destroy(gameObject);
        return amount;
    }

    void OnDestroy()
    {
        all.Remove(this);
        if (Task != null && Colony.Instance != null)
            Colony.Instance.Queue(AntGroup.Worker).Remove(Task);
    }
}
