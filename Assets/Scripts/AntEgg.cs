using UnityEngine;

// Яйце біля королеви. Дозріває hatchTime секунд, потім з нього виходить замовлена мураха.
// Займає клітинку (мурахи проходять крізь, але не зупиняються).
public class AntEgg : MonoBehaviour, ICellBlocker
{
    private AntQueen queen;
    private float hatchTime;
    private float left;
    private WorldBar bar;

    public AntBuild Build { get; private set; }
    public AntGroup Group { get; private set; }
    public GridManager Grid { get; private set; }
    public Vector3Int Cell { get; private set; }
    public float Progress => hatchTime > 0f ? 1f - left / hatchTime : 1f;
    GridManager ICellBlocker.BlockGrid => Grid;
    Vector3Int ICellBlocker.BlockCell => Cell;

    public static AntEgg Spawn(AntQueen queen, GridManager grid, Vector3Int cell, AntBuild build, AntGroup group, float hatchTime)
    {
        var go = new GameObject("Egg " + group);
        go.transform.position = grid.CellCenter(cell);
        SceneUtil.MoveToSceneOf(go, grid.gameObject);

        var egg = go.AddComponent<AntEgg>();
        egg.queen = queen;
        egg.Grid = grid;
        egg.Cell = cell;
        egg.Build = build;
        egg.Group = group;
        egg.hatchTime = hatchTime;
        egg.left = hatchTime;
        egg.BuildVisual(grid.CellWorldSize);

        if (Colony.Instance != null) Colony.Instance.AddBlocker(egg);
        return egg;
    }

    // Заглушка: овальне яйце з відтінком кольору тіла, смужка дозрівання над ним.
    private void BuildVisual(float cell)
    {
        var shell = new GameObject("Shell");
        shell.transform.SetParent(transform, false);
        shell.transform.localScale = new Vector3(0.38f, 0.5f, 1f) * cell;
        var sr = shell.AddComponent<SpriteRenderer>();
        sr.sprite = PlaceholderSprites.Circle;
        sr.color = Color.Lerp(new Color(0.97f, 0.95f, 0.88f), Build.Body.color, 0.25f);
        sr.sortingOrder = 1;

        bar = WorldBar.Create(transform, new Vector3(0f, 0.42f * cell, 0f), 0.6f * cell, 0.07f * cell,
            new Color(0.95f, 0.9f, 0.5f), 4);
        bar.Set(0f);
    }

    void Update()
    {
        left -= Time.deltaTime;
        bar.Set(Progress);
        if (left > 0f) return;

        if (queen != null) queen.Hatch(this);
        queen = null;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Colony.Instance != null) Colony.Instance.RemoveBlocker(this);
        if (queen != null) queen.ForgetEgg(this);
    }
}
