using UnityEngine;

// Склад колонії: скільки їжі є. Робітники приносять, мурахи з'їдають.
public class Storage : MonoBehaviour
{
    public static Storage Instance { get; private set; }

    [SerializeField] private int startFood = 10;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.35f, 0.15f);

    public int Food { get; private set; }
    public GridManager Grid { get; private set; }
    public Vector3Int Cell { get; private set; }

    void Awake()
    {
        Instance = this;
        Food = startFood;
    }

    void Start()
    {
        Grid = GridManager.At(transform.position);
        if (Grid == null) { Debug.LogError("Storage: не знайшов сітку"); return; }
        Cell = Grid.WorldToCell(transform.position);
        transform.position = Grid.CellCenter(Cell);
        PlaceholderSprites.Ensure(gameObject, false, placeholderColor, Grid.CellWorldSize * 0.8f, 1);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Add(int amount)
    {
        if (amount > 0) Food += amount;
    }

    public bool TryTake(int amount)
    {
        if (Food < amount) return false;
        Food -= amount;
        return true;
    }
}
