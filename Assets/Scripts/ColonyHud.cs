using TMPro;
using UnityEngine;

// Мінімальна інформація: скільки мурах у групах і HP вибраної мурахи.
// Латиницею, бо в шрифтах проєкту поки немає кирилиці.
public class ColonyHud : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private float refreshInterval = 0.25f;

    private float timer;

    void Awake()
    {
        if (text == null) text = GetComponent<TMP_Text>();
    }

    void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = refreshInterval;

        Colony colony = Colony.Instance;
        if (colony == null || text == null) return;

        string s = "BUILD " + colony.CountIn(AntGroup.Builder)
                 + "   WORK " + colony.CountIn(AntGroup.Worker)
                 + "   WAR " + colony.CountIn(AntGroup.Warrior);

        AntAgent sel = AntSelection.Instance != null ? AntSelection.Instance.Selected : null;
        if (sel != null)
            s += "\n" + GroupName(sel.Group) + "  HP " + sel.Hp + "/" + sel.MaxHp + "  " + StateName(sel);

        text.text = s;
    }

    private static string GroupName(AntGroup g)
    {
        switch (g)
        {
            case AntGroup.Builder: return "BUILDER";
            case AntGroup.Worker: return "WORKER";
            default: return "WARRIOR";
        }
    }

    private static string StateName(AntAgent a)
    {
        string s = a.CurrentState.ToString().ToUpper();
        return a.IsManual ? s + " (ORDER)" : s;
    }
}
