using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Кнопка «Поверхня / Мурашник»: переносить камеру між локаціями.
// Запам'ятовує, де камера була в кожній локації.
public class LocationSwitcher : MonoBehaviour
{
    // Латиницею, бо в шрифтах проєкту поки немає кирилиці. Напис — куди перейти.
    [SerializeField] private string toSurfaceText = "SURFACE";
    [SerializeField] private string toAnthillText = "ANTHILL";

    private readonly Dictionary<GridManager, Vector3> cameraPos = new Dictionary<GridManager, Vector3>();
    private TMP_Text label;

    void Awake()
    {
        label = GetComponentInChildren<TMP_Text>();
    }

    void Update()
    {
        if (label == null) return;
        GridManager cur = Current();
        label.text = cur != null && !cur.HasWalls ? toAnthillText : toSurfaceText;
    }

    private static GridManager Current()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;
        return GridManager.At(cam.transform.position) ?? GridManager.Instance;
    }

    public void Toggle()
    {
        Camera cam = Camera.main;
        GridManager cur = Current();
        if (cam == null || cur == null) return;

        GridManager next = null;
        foreach (GridManager g in GridManager.All)
            if (g != cur) { next = g; break; }
        if (next == null) return;

        cameraPos[cur] = cam.transform.position;
        Vector3 p = cameraPos.TryGetValue(next, out Vector3 saved) ? saved : DefaultView(next);
        p.z = cam.transform.position.z;
        cam.transform.position = p;
    }

    // Уперше — дивимося на вхід у цій локації.
    private static Vector3 DefaultView(GridManager grid)
    {
        foreach (Portal p in Portal.All)
            if (p.Grid == grid) return grid.CellCenter(p.Cell);
        return grid.transform.position;
    }
}
