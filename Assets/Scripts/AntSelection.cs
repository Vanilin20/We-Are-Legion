using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Вибір мурах тапом і ручні команди.
//  тап по мурасі              — вибрати лише її (тап по вибраній ще раз — прибрати її з вибору);
//  вибрані воїни + тап по воїну — додати його до загону;
//  кнопка WAR                  — вибрати всіх воїнів (ще раз — зняти вибір);
//  вибрані + тап по жуку        — усі вибрані атакують жука;
//  вибрана + тап по землі       — прокопати цю клітинку першою (лише перша вибрана);
//  вибрана + тап по їжі         — підібрати (лише перша вибрана);
//  вибрані + тап по підлозі     — піти туди (усі);
//  тап туди, де нічого не можна — зняти вибір.
// У режимі «Копати» не працює: там палець позначає клітинки.
public class AntSelection : MonoBehaviour
{
    public static AntSelection Instance { get; private set; }

    [Header("Тап")]
    [SerializeField] private float tapMaxTime = 0.35f;
    [SerializeField] private float tapMaxMovePixels = 20f;
    [Tooltip("Наскільки точно треба влучити по жуку, клітинок")]
    [SerializeField] private float beetleTapRadius = 0.8f;

    [Header("Кружок виділення")]
    [SerializeField] private Color ringColor = new Color(0.4f, 1f, 0.4f, 0.9f);
    [SerializeField] private float ringSizeCells = 1.1f;
    [SerializeField] private int ringSortingOrder = 3;

    private readonly List<AntAgent> selected = new List<AntAgent>();
    private readonly List<Transform> rings = new List<Transform>();
    private Sprite ringSprite;

    // Перша вибрана (для HUD і команд копати/підібрати).
    public AntAgent Selected => selected.Count > 0 ? selected[0] : null;
    public IReadOnlyList<AntAgent> SelectedAnts => selected;
    public int Count => selected.Count;

    private float downTime;
    private Vector2 downPos;
    private bool downOverUI;
    private bool pressed;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        selected.RemoveAll(a => a == null); // мураху знищено

        if (!DigInput.IsActive) ReadTap();

        UpdateRings();
    }

    // ---------- Вибір ----------

    public void Select(AntAgent ant)
    {
        selected.Clear();
        if (ant != null) selected.Add(ant);
    }

    public void Deselect() => selected.Clear();

    public void Remove(AntAgent ant) => selected.Remove(ant);

    public bool IsSelected(AntAgent ant) => selected.Contains(ant);

    // Кнопка WAR: усі воїни колонії. Якщо вже вибрані саме вони — зняти вибір.
    public void SelectAllWarriors()
    {
        Colony colony = Colony.Instance;
        if (colony == null) return;

        var warriors = new List<AntAgent>();
        foreach (AntAgent a in colony.Ants)
            if (a != null && a.IsAlive && a.Group == AntGroup.Warrior) warriors.Add(a);

        bool same = warriors.Count > 0 && warriors.Count == selected.Count && selected.TrueForAll(warriors.Contains);
        selected.Clear();
        if (!same) selected.AddRange(warriors);
        if (warriors.Count == 0) Debug.Log("У колонії немає воїнів — збери їх у конструкторі");
    }

    private bool AllWarriors()
    {
        if (selected.Count == 0) return false;
        foreach (AntAgent a in selected) if (a.Group != AntGroup.Warrior) return false;
        return true;
    }

    // ---------- Ввід ----------

    private void ReadTap()
    {
        bool down, up;
        Vector2 pos;
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            down = t.phase == TouchPhase.Began;
            up = t.phase == TouchPhase.Ended;
            pos = t.position;
        }
        else
        {
            down = Input.GetMouseButtonDown(0);
            up = Input.GetMouseButtonUp(0);
            pos = Input.mousePosition;
        }

        if (down)
        {
            pressed = true;
            downTime = Time.unscaledTime;
            downPos = pos;
            downOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        if (up && pressed)
        {
            pressed = false;
            bool quick = Time.unscaledTime - downTime <= tapMaxTime;
            bool still = (pos - downPos).magnitude <= tapMaxMovePixels;
            if (quick && still && !downOverUI) HandleTap(pos);
        }
    }

    private void HandleTap(Vector2 screen)
    {
        Vector3 world = Camera.main.ScreenToWorldPoint(screen);
        world.z = 0f;

        AntAgent tapped = AntAt(world);
        if (tapped != null)
        {
            if (selected.Contains(tapped)) selected.Remove(tapped);
            else if (tapped.Group == AntGroup.Warrior && AllWarriors()) selected.Add(tapped); // загін воїнів
            else Select(tapped);
            return;
        }

        if (selected.Count == 0) return;

        EnemyBeetle beetle = EnemyBeetle.At(world, beetleTapRadius);
        if (beetle != null)
        {
            Attack(beetle);
            return;
        }

        GridManager grid = GridManager.At(world);
        if (grid == null) { Deselect(); return; }
        Vector3Int cell = grid.WorldToCell(world);
        AntAgent first = Selected;

        FoodItem food = FoodItem.At(grid, cell);
        if (food != null && food.Task != null)
        {
            if (!first.AssignManual(food.Task, Colony.Instance.Queue(AntGroup.Worker)))
                Debug.Log("Мураха не може взяти їжу в " + cell + " (недосяжно або руки повні)");
        }
        else if (grid.IsDiggable(cell))
        {
            DigTask dig = grid.GetOrMarkDigTask(cell);
            if (!first.AssignManual(dig, Colony.Instance.Queue(AntGroup.Builder)))
                Debug.Log("Мураха не може дійти до клітинки " + cell + " — позначку лишено в черзі");
        }
        else if (grid.IsWalkable(cell))
        {
            // усі вибрані йдуть туди; зайві самі відійдуть на сусідні клітинки
            foreach (AntAgent a in selected)
                if (!a.AssignManual(new MoveTask(grid, cell), null))
                    Debug.Log(a.name + " не може дійти до клітинки " + cell);
        }
        else
        {
            Deselect();
        }
    }

    private void Attack(EnemyBeetle beetle)
    {
        int sent = 0;
        foreach (AntAgent a in selected)
        {
            if (a.AssignManual(new AttackTask(beetle), null)) sent++;
            else Debug.Log(a.name + " не може дійти до жука");
        }
        if (sent > 0) Debug.Log("Атака: " + sent + " мурах(и) на " + beetle.name);
    }

    private static AntAgent AntAt(Vector3 world)
    {
        foreach (Collider2D c in Physics2D.OverlapPointAll(world))
        {
            AntAgent a = c.GetComponentInParent<AntAgent>();
            if (a != null && a.enabled) return a;
        }
        return null;
    }

    // ---------- Кружки ----------

    private void UpdateRings()
    {
        while (rings.Count < selected.Count) rings.Add(CreateRing());

        for (int i = 0; i < rings.Count; i++)
        {
            Transform ring = rings[i];
            bool on = i < selected.Count;
            if (ring.gameObject.activeSelf != on) ring.gameObject.SetActive(on);
            if (!on) continue;

            AntAgent ant = selected[i];
            ring.position = ant.transform.position;
            if (ring.gameObject.scene != ant.gameObject.scene) SceneUtil.MoveToSceneOf(ring.gameObject, ant.gameObject);
        }
    }

    private Transform CreateRing()
    {
        if (ringSprite == null)
        {
            const int size = 64;
            var tex = new Texture2D(size, size) { filterMode = FilterMode.Point };
            float r = size / 2f - 1f;
            float thickness = 4f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                tex.SetPixel(x, y, d <= r && d >= r - thickness ? Color.white : Color.clear);
            }
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size); // 1 юніт
        }

        var go = new GameObject("SelectionRing");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ringSprite;
        sr.color = ringColor;
        sr.sortingOrder = ringSortingOrder;

        float cell = GridManager.Instance != null ? GridManager.Instance.CellWorldSize : 1f;
        go.transform.localScale = Vector3.one * cell * ringSizeCells;
        go.SetActive(false);
        return go.transform;
    }
}
