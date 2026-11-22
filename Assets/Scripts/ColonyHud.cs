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

        string s = "FOOD " + (Storage.Instance != null ? Storage.Instance.Food : 0) + "   "
                 + "BUILD " + colony.CountIn(AntGroup.Builder)
                 + "   WORK " + colony.CountIn(AntGroup.Worker)
                 + "   WAR " + colony.CountIn(AntGroup.Warrior);

        AntQueen queen = AntQueen.Instance;
        if (queen != null)
        {
            s += "\nANTS " + colony.Ants.Count + "/" + queen.MaxAnts;
            if (queen.EggsCount > 0) s += "   EGGS " + queen.EggsCount;
            if (queen.OrdersCount > 0) s += "   QUEUE " + queen.OrdersCount + " (NEXT EGG " + Mathf.CeilToInt(queen.LayCooldown) + "s)";
        }

        AntSelection selection = AntSelection.Instance;
        AntAgent sel = selection != null ? selection.Selected : null;
        if (sel != null && selection.Count > 1)
        {
            int hp = 0, max = 0;
            foreach (AntAgent a in selection.SelectedAnts) { hp += a.Hp; max += a.MaxHp; }
            s += "\nSQUAD " + selection.Count + "  HP " + hp + "/" + max;
        }
        else if (sel != null)
            s += "\n" + GroupName(sel.Group) + "  HP " + sel.Hp + "/" + sel.MaxHp + "  " + StateName(sel)
               + (sel.CargoCount > 0 ? "  CARRY " + sel.CargoCount + "/" + sel.CarryCapacity : "")
               + (sel.Hungry ? "  HUNGRY" : "");

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
