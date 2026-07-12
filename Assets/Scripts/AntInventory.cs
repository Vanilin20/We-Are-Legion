using UnityEngine;
using UnityEngine.Events;

public class AntInventory : MonoBehaviour
{
    [Header("Інвентар")]
    [SerializeField] private int maxCapacity = 50;
    private int currentWood = 0;

    public int CurrentWood => currentWood;
    public int MaxCapacity => maxCapacity;
    public bool IsFull => currentWood >= maxCapacity;

    public UnityEvent<int, int> OnInventoryChanged; // поточне, максимум

    public int AddWood(int amount)
    {
        int canTake = Mathf.Min(amount, maxCapacity - currentWood);
        currentWood += canTake;
        OnInventoryChanged?.Invoke(currentWood, maxCapacity);
        return canTake; // скільки реально взяли
    }
}