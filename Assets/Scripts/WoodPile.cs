using UnityEngine;

public class WoodPile : MonoBehaviour
{
    [Header("Деревина")]
    [SerializeField] private int totalWood = 100;
    [SerializeField] private float collectPerSecond = 10f;

    private AntInventory collectorInventory;
    private bool isCollecting = false;
    private float collectBuffer = 0f;

    public int TotalWood => totalWood;
    public bool IsEmpty => totalWood <= 0;

    public void StartCollecting(AntInventory inventory)
    {
        collectorInventory = inventory;
        isCollecting = true;
    }

    public void StopCollecting()
    {
        isCollecting = false;
        collectorInventory = null;
        collectBuffer = 0f;
    }

    void Update()
    {
        if (!isCollecting || collectorInventory == null) return;
        if (collectorInventory.IsFull || IsEmpty)
        {
            StopCollecting();
            return;
        }

        collectBuffer += collectPerSecond * Time.deltaTime;

        if (collectBuffer >= 1f)
        {
            int toCollect = Mathf.FloorToInt(collectBuffer);
            int taken = collectorInventory.AddWood(Mathf.Min(toCollect, totalWood));
            totalWood -= taken;
            collectBuffer -= toCollect;

            if (IsEmpty) Destroy(gameObject);
        }
    }
}