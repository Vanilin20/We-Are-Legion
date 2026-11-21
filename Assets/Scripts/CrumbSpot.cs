using UnityEngine;

// Місце, де лежить крихта. Коли її підібрали — нова з'являється тут же через respawnTime.
public class CrumbSpot : MonoBehaviour
{
    [SerializeField] private float respawnTime = 60f;
    [SerializeField] private int amount = 1;

    private FoodItem current;
    private float timer = -1f;

    void Start()
    {
        SpawnNow();
    }

    void Update()
    {
        if (current != null || timer < 0f) return;
        timer -= Time.deltaTime;
        if (timer <= 0f) SpawnNow();
    }

    private void SpawnNow()
    {
        timer = -1f;
        GridManager grid = GridManager.At(transform.position);
        if (grid == null) { Debug.LogWarning("CrumbSpot " + name + ": поза сіткою"); return; }

        current = FoodItem.Spawn(grid, grid.WorldToCell(transform.position), amount, "Crumb");
        current.PickedUp += OnPickedUp;
    }

    private void OnPickedUp(FoodItem f)
    {
        current = null;
        timer = respawnTime;
    }
}
