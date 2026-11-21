using UnityEngine;
using UnityEngine.EventSystems;

// Вибір мурахи тапом і ручні команди.
//  тап по мурасі            — вибрати (тап по вибраній ще раз — зняти вибір);
//  вибрана + тап по землі    — прокопати цю клітинку першою;
//  вибрана + тап по підлозі  — піти туди;
//  тап туди, де нічого не можна — зняти вибір.
// У режимі «Копати» не працює: там палець позначає клітинки.
public class AntSelection : MonoBehaviour
{
    public static AntSelection Instance { get; private set; }

    [Header("Тап")]
    [SerializeField] private float tapMaxTime = 0.35f;
    [SerializeField] private float tapMaxMovePixels = 20f;

    [Header("Кружок виділення")]
    [SerializeField] private Color ringColor = new Color(0.4f, 1f, 0.4f, 0.9f);
    [SerializeField] private float ringSizeCells = 1.1f;
    [SerializeField] private int ringSortingOrder = 3;

    public AntAgent Selected { get; private set; }

    private Transform ring;
    private float downTime;
    private Vector2 downPos;
    private bool downOverUI;
    private bool pressed;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        CreateRing();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (Selected == null && ring.gameObject.activeSelf) Deselect(); // мураху знищено

        if (!DigInput.IsActive) ReadTap();

        if (Selected != null)
        {
            ring.position = Selected.transform.position;
            if (ring.gameObject.scene != Selected.gameObject.scene) SceneUtil.MoveToSceneOf(ring.gameObject, Selected.gameObject);
        }
    }

    public void Select(AntAgent ant)
    {
        Selected = ant;
        ring.gameObject.SetActive(ant != null);
    }

    public void Deselect() => Select(null);

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
            if (tapped == Selected) Deselect();
            else Select(tapped);
            return;
        }

        if (Selected == null) return;

        GridManager grid = GridManager.At(world);
        if (grid == null) { Deselect(); return; }
        Vector3Int cell = grid.WorldToCell(world);

        FoodItem food = FoodItem.At(grid, cell);
        if (food != null && food.Task != null)
        {
            if (!Selected.AssignManual(food.Task, Colony.Instance.Queue(AntGroup.Worker)))
                Debug.Log("Мураха не може взяти їжу в " + cell + " (недосяжно або руки повні)");
        }
        else if (grid.IsDiggable(cell))
        {
            DigTask dig = grid.GetOrMarkDigTask(cell);
            if (!Selected.AssignManual(dig, Colony.Instance.Queue(AntGroup.Builder)))
                Debug.Log("Мураха не може дійти до клітинки " + cell + " — позначку лишено в черзі");
        }
        else if (grid.IsWalkable(cell))
        {
            if (!Selected.AssignManual(new MoveTask(grid, cell), null))
                Debug.Log("Мураха не може дійти до клітинки " + cell);
        }
        else
        {
            Deselect();
        }
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

    // ---------- Кружок ----------

    private void CreateRing()
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

        var go = new GameObject("SelectionRing");
        ring = go.transform;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size); // 1 юніт
        sr.color = ringColor;
        sr.sortingOrder = ringSortingOrder;

        float cell = GridManager.Instance != null ? GridManager.Instance.CellWorldSize : 1f;
        ring.localScale = Vector3.one * cell * ringSizeCells;
        go.SetActive(false);
    }
}
