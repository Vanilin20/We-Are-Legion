using UnityEngine;

public class AntCollector : MonoBehaviour
{
    private AntInventory inventory;
    private WoodPile currentPile;

    void Start()
    {
        inventory = GetComponent<AntInventory>();
    }

    void Update()
    {
        if (currentPile == null) return;

        // Затискаєш — збираєш, відпустив — стоп
        if (CollectButton.IsHeld)
        {
            currentPile.StartCollecting(inventory);
        }
        else
        {
            currentPile.StopCollecting();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<WoodPile>(out WoodPile pile))
        {
            currentPile = pile;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent<WoodPile>(out WoodPile pile) && pile == currentPile)
        {
            currentPile.StopCollecting();
            currentPile = null;
        }
    }

    
}
