using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Режим «Копати»: свайп по землі позначає клітинки під копання.
// Свайп, що почався з уже позначеної клітинки, знімає позначки.
// Вішається на кнопку; OnClick кнопки -> ToggleDigMode.
public class DigInput : MonoBehaviour
{
    public static bool IsActive { get; private set; }

    [SerializeField] private Color onColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color offColor = Color.white;
    // Латиницею, бо в шрифтах проєкту поки немає кирилиці.
    [SerializeField] private string offText = "DIG";
    [SerializeField] private string onText = "DONE";

    private Image buttonImage;
    private TMP_Text label;

    private bool stroke;
    private bool eraseStroke;
    private Vector3Int lastCell;

    void Awake()
    {
        buttonImage = GetComponent<Image>();
        label = GetComponentInChildren<TMP_Text>();
        Refresh();
    }

    void OnDisable()
    {
        SetMode(false);
    }

    public void ToggleDigMode() => SetMode(!IsActive);

    private void SetMode(bool on)
    {
        IsActive = on;
        CameraFollow.DragBlocked = on;
        stroke = false;
        Refresh();
    }

    private void Refresh()
    {
        if (buttonImage != null) buttonImage.color = IsActive ? onColor : offColor;
        if (label != null) label.text = IsActive ? onText : offText;
    }

    void Update()
    {
        if (!IsActive || GridManager.Instance == null) return;

        bool down, held;
        Vector2 screen;
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            down = t.phase == TouchPhase.Began;
            held = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
            screen = t.position;
        }
        else
        {
            down = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            screen = Input.mousePosition;
        }

        if (!held) { stroke = false; return; }

        // Копати можна лише там, де є стіни (у мурашнику).
        Vector3 world = Camera.main.ScreenToWorldPoint(screen);
        GridManager grid = GridManager.At(world);
        if (grid == null || !grid.HasWalls) { stroke = false; return; }
        Vector3Int cell = grid.WorldToCell(world);

        if (down)
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (overUI) return;
            stroke = true;
            eraseStroke = grid.IsMarked(cell);
            lastCell = cell;
            Apply(grid, cell);
            return;
        }

        if (!stroke || cell == lastCell) return;

        // Швидкий свайп перескакує клітинки — заповнюємо проміжок.
        Vector3Int from = lastCell;
        int steps = Mathf.Max(Mathf.Abs(cell.x - from.x), Mathf.Abs(cell.y - from.y));
        for (int i = 1; i <= steps; i++)
        {
            float k = (float)i / steps;
            var c = new Vector3Int(
                Mathf.RoundToInt(Mathf.Lerp(from.x, cell.x, k)),
                Mathf.RoundToInt(Mathf.Lerp(from.y, cell.y, k)), 0);
            Apply(grid, c);
        }
        lastCell = cell;
    }

    private void Apply(GridManager grid, Vector3Int cell)
    {
        if (eraseStroke) grid.Unmark(cell);
        else grid.Mark(cell);
    }
}
