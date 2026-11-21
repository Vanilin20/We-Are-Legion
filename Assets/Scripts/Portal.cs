using System.Collections.Generic;
using UnityEngine;

// Вхід між локаціями (мурашник <-> поверхня). Два входи з однаковим linkId — пара.
// Мураха, що дійшла до клітинки входу, з'являється на клітинці парного входу.
public class Portal : MonoBehaviour
{
    private static readonly List<Portal> all = new List<Portal>();
    public static IReadOnlyList<Portal> All => all;

    [SerializeField] private string linkId = "main";

    public string LinkId => linkId;
    public GridManager Grid { get; private set; }
    public Vector3Int Cell { get; private set; }

    public Portal Linked
    {
        get
        {
            foreach (Portal p in all)
                if (p != this && p.linkId == linkId && p.Grid != null) return p;
            return null;
        }
    }

    void Start()
    {
        Grid = GridManager.At(transform.position);
        if (Grid == null) { Debug.LogError("Portal " + name + ": не знайшов сітку"); return; }
        Cell = Grid.WorldToCell(transform.position);
        all.Add(this);
    }

    void OnDestroy()
    {
        all.Remove(this);
    }
}
