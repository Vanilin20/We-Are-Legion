using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Вікно конструктора мурах (AntConstructor). Вішається на Canvas; кнопка ANT викликає Toggle.
// Вибір тіла, ніг і щелепи (по 3 варіанти), стати збірки, ціна, вибір групи й замовлення в королеви.
// Вікно будується кодом при першому відкритті; написи латиницею, бо в шрифті поки немає кирилиці.
public class ConstructorWindow : MonoBehaviour
{
    public static ConstructorWindow Instance { get; private set; }

    [Tooltip("Порожньо — береться шрифт перших написів на Canvas")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Vector2 panelSize = new Vector2(660f, 510f);
    [SerializeField] private Color panelColor = new Color(0.09f, 0.07f, 0.05f, 0.95f);
    [SerializeField] private Color textColor = new Color(0.95f, 0.92f, 0.85f);
    [SerializeField] private Color okColor = new Color(0.5f, 0.95f, 0.5f);
    [SerializeField] private Color badColor = new Color(1f, 0.45f, 0.4f);

    private static readonly AntGroup[] Groups = { AntGroup.Builder, AntGroup.Worker, AntGroup.Warrior };

    private GameObject panel;
    private AntBuild build;
    private AntGroup group = AntGroup.Worker;

    private readonly Dictionary<AntPart, Image> partButtons = new Dictionary<AntPart, Image>();
    private readonly Dictionary<AntGroup, Image> groupButtons = new Dictionary<AntGroup, Image>();
    private TMP_Text statsText, costText, queueText, messageText;
    private Image orderButton;
    private float messageTimer;
    private float refreshTimer;

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- Відкрити / закрити ----------

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (panel == null) Build();
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        Refresh();
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (!IsOpen) return;
        if (Colony.Instance != null && Colony.Instance.IsGameOver) { Close(); return; }

        if (messageTimer > 0f)
        {
            messageTimer -= Time.unscaledDeltaTime;
            if (messageTimer <= 0f) messageText.text = "";
        }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f) Refresh(); // їжа й черга змінюються самі
    }

    // ---------- Дії ----------

    private void Pick(AntPart part)
    {
        build = build.With(part);
        Refresh();
    }

    private void PickGroup(AntGroup g)
    {
        group = g;
        Refresh();
    }

    private void Order()
    {
        AntQueen queen = AntQueen.Instance;
        if (queen == null) { Say("NO QUEEN IN THE ANTHILL", badColor); return; }

        switch (queen.PlaceOrder(build, group))
        {
            case AntQueen.OrderResult.Ok: Say("ORDERED: " + GroupName(group) + " (-" + build.Cost + " FOOD)", okColor); break;
            case AntQueen.OrderResult.NotEnoughFood: Say("NOT ENOUGH FOOD", badColor); break;
            case AntQueen.OrderResult.Limit: Say("COLONY LIMIT " + queen.MaxAnts + " ANTS", badColor); break;
            default: Say("QUEEN IS NOT READY", badColor); break;
        }
        Refresh();
    }

    private void Say(string text, Color c)
    {
        messageText.text = text;
        messageText.color = c;
        messageTimer = 3f;
    }

    // ---------- Оновлення ----------

    private void Refresh()
    {
        refreshTimer = 0.25f;
        if (panel == null) return;

        foreach (var kv in partButtons)
        {
            bool on = build.Get(kv.Key.slot) == kv.Key;
            SetPicked(kv.Value, kv.Key.color, on);
        }
        foreach (var kv in groupButtons)
            SetPicked(kv.Value, GroupColor(kv.Key), kv.Key == group);

        statsText.text = "HP " + build.Hp
                       + "    SPEED " + Num(build.Speed)
                       + "    CARRY " + build.Carry
                       + "    DIG " + Num(build.DigTime) + "s"
                       + "    DMG " + build.Damage;

        int food = Storage.Instance != null ? Storage.Instance.Food : 0;
        costText.text = "COST " + build.Cost + " FOOD   (STORAGE " + food + ")";

        AntQueen queen = AntQueen.Instance;
        bool can = queen != null && queen.CanOrder(build) == AntQueen.OrderResult.Ok;
        costText.color = food >= build.Cost ? textColor : badColor;
        orderButton.color = can ? new Color(0.35f, 0.7f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);

        if (queen == null) queueText.text = "NO QUEEN";
        else
        {
            string q = "ANTS " + queen.Population + "/" + queen.MaxAnts
                     + "   EGGS " + queen.EggsCount
                     + "   QUEUE " + queen.OrdersCount;
            if (queen.OrdersCount > 0) q += "   NEXT EGG IN " + Mathf.CeilToInt(queen.LayCooldown) + "s";
            q += "\nEGG HATCHES IN " + Mathf.RoundToInt(queen.HatchTime) + "s";
            queueText.text = q;
        }
    }

    private static void SetPicked(Image img, Color c, bool on)
    {
        img.color = on ? c : Color.Lerp(c, Color.black, 0.55f);
        Outline o = img.GetComponent<Outline>();
        if (o != null) o.enabled = on;
    }

    // ---------- Побудова вікна ----------

    private void Build()
    {
        build = AntBuild.Basic;
        if (font == null)
        {
            TMP_Text any = GetComponentInChildren<TMP_Text>(true);
            font = any != null ? any.font : TMP_Settings.defaultFontAsset;
        }

        panel = new GameObject("ConstructorWindow", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = panelSize;
        prt.anchoredPosition = Vector2.zero;
        panel.AddComponent<Image>().color = panelColor; // ще й ловить тапи, щоб вони не йшли у світ

        float w = panelSize.x, h = panelSize.y;
        float top = h / 2f;

        Label("ANT CONSTRUCTOR", new Vector2(0f, top - 32f), new Vector2(w - 120f, 40f), 28f, TextAlignmentOptions.Center);
        Button("X", new Vector2(w / 2f - 32f, top - 32f), new Vector2(44f, 44f), new Color(0.5f, 0.2f, 0.2f), Close);

        // три рядки частин
        string[] slotNames = { "BODY", "LEGS", "JAW" };
        float rowY = top - 92f;
        float colX0 = -w / 2f + 220f;
        const float btnW = 140f, btnH = 46f, gap = 12f;
        foreach (AntPartSlot slot in System.Enum.GetValues(typeof(AntPartSlot)))
        {
            Label(slotNames[(int)slot], new Vector2(-w / 2f + 75f, rowY), new Vector2(120f, btnH), 22f, TextAlignmentOptions.Left);
            List<AntPart> list = AntPartCatalog.Of(slot);
            for (int i = 0; i < list.Count; i++)
            {
                AntPart part = list[i];
                string text = part.displayName + (part.isBasic ? "" : "  +" + part.Cost);
                Image img = Button(text, new Vector2(colX0 + i * (btnW + gap), rowY), new Vector2(btnW, btnH), part.color,
                    () => Pick(part), darkText: true);
                AddOutline(img);
                partButtons[part] = img;
            }
            rowY -= btnH + 14f;
        }

        statsText = Label("", new Vector2(0f, rowY + 4f), new Vector2(w - 40f, 34f), 20f, TextAlignmentOptions.Center);
        rowY -= 50f;

        // група
        Label("GROUP", new Vector2(-w / 2f + 75f, rowY), new Vector2(120f, btnH), 22f, TextAlignmentOptions.Left);
        for (int i = 0; i < Groups.Length; i++)
        {
            AntGroup g = Groups[i];
            Image img = Button(GroupName(g), new Vector2(colX0 + i * (btnW + gap), rowY), new Vector2(btnW, btnH), GroupColor(g),
                () => PickGroup(g), darkText: true);
            AddOutline(img);
            groupButtons[g] = img;
        }
        rowY -= btnH + 22f;

        // ціна й замовлення
        costText = Label("", new Vector2(-70f, rowY), new Vector2(w - 220f, 40f), 22f, TextAlignmentOptions.Left);
        orderButton = Button("ORDER", new Vector2(w / 2f - 95f, rowY), new Vector2(160f, 52f), new Color(0.35f, 0.7f, 0.3f), Order);
        rowY -= 46f;

        messageText = Label("", new Vector2(0f, rowY), new Vector2(w - 40f, 30f), 20f, TextAlignmentOptions.Center);
        queueText = Label("", new Vector2(0f, -top + 32f), new Vector2(w - 40f, 50f), 17f, TextAlignmentOptions.Center);
        queueText.color = new Color(0.8f, 0.78f, 0.7f);

        panel.SetActive(false);
    }

    private TMP_Text Label(string text, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align, Transform parent = null)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent != null ? parent : panel.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = textColor;
        t.raycastTarget = false;
        return t;
    }

    private Image Button(string text, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, bool darkText = false)
    {
        var go = new GameObject("Button " + text, typeof(RectTransform));
        go.transform.SetParent(panel.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = color;
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);

        TMP_Text t = Label(text, Vector2.zero, size, 19f, TextAlignmentOptions.Center, go.transform);
        if (darkText) t.color = new Color(0.08f, 0.06f, 0.04f);
        return img;
    }

    private static void AddOutline(Image img)
    {
        var o = img.gameObject.AddComponent<Outline>();
        o.effectColor = Color.white;
        o.effectDistance = new Vector2(3f, -3f);
        o.enabled = false;
    }

    private static string Num(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static string GroupName(AntGroup g)
    {
        switch (g)
        {
            case AntGroup.Builder: return "BUILDER";
            case AntGroup.Worker: return "WORKER";
            default: return "WARRIOR";
        }
    }

    private static Color GroupColor(AntGroup g)
    {
        switch (g)
        {
            case AntGroup.Builder: return new Color(0.85f, 0.7f, 0.35f);
            case AntGroup.Worker: return new Color(0.55f, 0.8f, 0.5f);
            default: return new Color(0.9f, 0.45f, 0.4f);
        }
    }
}
