using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Автотест конструктора, королеви й жуків. Запуск: меню We Are Legion → Автотест
// (або файл Logs/wal-autotest.flag). Сам входить у Play Mode, проганяє сценарій
// із прискореним часом і пише звіт у Logs/wal-autotest.txt, потім виходить з Play Mode.
[InitializeOnLoad]
public static class WeAreLegionAutoTest
{
    private const string RunKey = "wal.autotest.run";
    private const float Speed = 8f;

    private static string LogsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
    private static string FlagPath => Path.Combine(LogsDir, "wal-autotest.flag");
    private static string ReportPath => Path.Combine(LogsDir, "wal-autotest.txt");

    private static IEnumerator routine;
    private static StringBuilder report;
    private static int fails;

    static WeAreLegionAutoTest()
    {
        EditorApplication.update += Tick;
    }

    [MenuItem("We Are Legion/Автотест (конструктор, королева, жуки)")]
    public static void Run()
    {
        SessionState.SetBool(RunKey, true);
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            routine = null;
            if (File.Exists(FlagPath))
            {
                File.Delete(FlagPath);
                Run();
            }
            return;
        }

        if (!SessionState.GetBool(RunKey, false)) return;
        if (routine == null)
        {
            report = new StringBuilder();
            fails = 0;
            deathCount = 0;
            Application.logMessageReceived += OnLog;
            routine = Scenario();
        }

        bool more;
        try { more = routine.MoveNext(); }
        catch (System.Exception e)
        {
            Line("EXCEPTION: " + e);
            fails++;
            more = false;
        }
        if (!more) Finish();
    }

    private static void Finish()
    {
        SessionState.SetBool(RunKey, false);
        Application.logMessageReceived -= OnLog;
        Time.timeScale = 1f;
        Line("");
        Line(fails == 0 ? "RESULT: OK" : "RESULT: " + fails + " FAIL(S)");
        Directory.CreateDirectory(LogsDir);
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("Автотест завершено: " + ReportPath);
        routine = null;
        EditorApplication.isPlaying = false;
    }

    private static void OnLog(string msg, string stack, LogType type)
    {
        if (report == null) return;
        if (msg.Contains("загинула")) deathCount++;
        string t = type == LogType.Log ? "log" : type.ToString().ToUpper();
        report.AppendLine(string.Format("  [{0,6:0.0}s {1}] {2}", Time.time, t, msg));
        if (type == LogType.Error || type == LogType.Exception) { fails++; report.AppendLine("    " + stack.Replace("\n", "\n    ")); }
    }

    private static void Line(string s) => report.AppendLine(s);

    private static void Check(bool ok, string what)
    {
        Line((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) fails++;
    }

    private static IEnumerator Wait(float gameSeconds)
    {
        float end = Time.time + gameSeconds;
        while (Time.time < end) yield return null;
    }

    // ---------- Сценарій ----------

    private static IEnumerator Scenario()
    {
        // дати сценам прокинутись (поверхня догружається)
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 1.5f) yield return null;

        Line("=== We Are Legion autotest " + System.DateTime.Now);
        Colony colony = Colony.Instance;
        AntQueen queen = AntQueen.Instance;
        Storage storage = Storage.Instance;
        Check(colony != null, "Colony є");
        Check(queen != null && queen.enabled, "AntQueen є й увімкнена");
        Check(storage != null, "Storage є");
        Check(ConstructorWindow.Instance != null, "ConstructorWindow є на Canvas");
        Check(AntPartCatalog.All.Count == 9, "9 частин у каталозі (" + AntPartCatalog.All.Count + ")");
        if (colony == null || queen == null || storage == null) yield break;

        DumpSurface();
        Snapshot("старт");

        // --- Вікно конструктора
        ConstructorWindow.Instance.Open();
        yield return null;
        Check(ConstructorWindow.Instance.IsOpen, "вікно конструктора відкривається");
        ConstructorWindow.Instance.Close();

        // --- Збірки й ціни з GDD
        AntBuild warrior = Make("Body_Armored", "Legs_Basic", "Jaw_Fighter");
        AntBuild carrier = Make("Body_Light", "Legs_Cargo", "Jaw_Basic");
        AntBuild digger = Make("Body_Simple", "Legs_Basic", "Jaw_Digger");
        Check(AntBuild.Basic.Cost == 5 && AntBuild.Basic.Hp == 4 && Mathf.Approximately(AntBuild.Basic.Speed, 2f), "базова: 5 їжі, HP 4, швидкість 2");
        Check(warrior.Cost == 11 && warrior.Hp == 8 && Mathf.Approximately(warrior.Speed, 1.5f) && warrior.Damage == 2 && Mathf.Approximately(warrior.DigTime, 1.5f), "воїн: 11 їжі, HP 8, 1.5 кл/с, шкода 2, копання 1.5");
        Check(carrier.Cost == 11 && carrier.Hp == 2 && Mathf.Approximately(carrier.Speed, 2f) && carrier.Carry == 4, "носій: 11 їжі, HP 2, 2 кл/с, вантаж 4");
        Check(digger.Cost == 8 && Mathf.Approximately(digger.DigTime, 0.5f), "будівельник: 8 їжі, копання 0.5");

        // --- Замовлення
        int food0 = storage.Food;
        AntQueen.OrderResult r = queen.PlaceOrder(warrior, AntGroup.Warrior);
        Check(food0 < 11 ? r == AntQueen.OrderResult.NotEnoughFood : r == AntQueen.OrderResult.Ok,
            "замовлення воїна при " + food0 + " їжі → " + r);
        if (r == AntQueen.OrderResult.Ok) storage.Add(11); // повернемо, щоб далі рахувати чисто

        storage.Add(60);
        int food1 = storage.Food;
        Check(queen.PlaceOrder(warrior, AntGroup.Warrior) == AntQueen.OrderResult.Ok, "замовлення воїна 1");
        Check(queen.PlaceOrder(warrior, AntGroup.Warrior) == AntQueen.OrderResult.Ok, "замовлення воїна 2");
        Check(queen.PlaceOrder(carrier, AntGroup.Worker) == AntQueen.OrderResult.Ok, "замовлення носія");
        Check(storage.Food == food1 - 33, "їжа знята одразу: " + food1 + " → " + storage.Food + " (очікував -33)");

        // --- Яйця: перше одразу, наступне не раніше ніж через 20 с
        yield return null;
        yield return null;
        Check(queen.EggsCount == 1 && queen.OrdersCount == 2, "одразу одне яйце, два в черзі (eggs " + queen.EggsCount + ", queue " + queen.OrdersCount + ")");
        float firstEgg = Time.time;

        Time.timeScale = Speed;
        int antsBefore = colony.Ants.Count;
        yield return Wait(18f);
        Check(queen.EggsCount == 1, "до 20 с друге яйце не з'явилось (eggs " + queen.EggsCount + ", t " + (Time.time - firstEgg).ToString("0.0") + ")");
        while (queen.EggsCount < 2 && Time.time - firstEgg < 26f) yield return null;
        Check(queen.EggsCount == 2 && Time.time - firstEgg >= 19.9f, "друге яйце через " + (Time.time - firstEgg).ToString("0.0") + " с (eggs " + queen.EggsCount + ")");

        // вилуплення першого — через 30 с після відкладання
        while (Time.time - firstEgg < 29f) yield return null;
        Check(CountGroup(AntGroup.Warrior) == 0, "до 30 с воїн ще в яйці");
        while (Time.time - firstEgg < 31.5f) yield return null;
        AntAgent w1 = FindGroup(AntGroup.Warrior);
        Check(w1 != null, "через 30 с вилупився воїн");
        if (w1 != null)
            Check(w1.MaxHp == 8 && w1.Hp == 8 && Mathf.Approximately(w1.SpeedCells, 1.5f) && w1.AttackDamage == 2,
                "стати воїна в грі: HP " + w1.Hp + "/" + w1.MaxHp + ", швидкість " + w1.SpeedCells + ", шкода " + w1.AttackDamage);

        // дочекатись другого воїна й носія
        float until = Time.time + 60f;
        while (Time.time < until && (CountGroup(AntGroup.Warrior) < 2 || queen.EggsCount + queen.OrdersCount > 0)) yield return null;
        Check(CountGroup(AntGroup.Warrior) >= 2, "двоє воїнів вилупились");
        Check(deathCount == 0, "за " + Time.time.ToString("0") + " с гри жодна мураха не загинула (вільна їжа без бою)");
        Check(colony.Ants.Count >= antsBefore + 3 - Deaths(), "мурах стало більше: " + antsBefore + " → " + colony.Ants.Count);
        Snapshot("після вилуплення");

        // --- Ліміт 50
        int pop = queen.Population;
        storage.Add(1000);
        int placed = 0;
        while (queen.PlaceOrder(AntBuild.Basic, AntGroup.Builder) == AntQueen.OrderResult.Ok && placed < 100) placed++;
        Check(queen.Population == queen.MaxAnts, "ліміт: населення " + pop + " + " + placed + " замовлень = " + queen.Population + " (макс " + queen.MaxAnts + ")");
        Check(queen.PlaceOrder(AntBuild.Basic, AntGroup.Builder) == AntQueen.OrderResult.Limit, "понад ліміт → Limit");
        ClearOrders(queen, storage);

        // --- Атака на жука
        EnemyBeetle beetle = NearestBeetle();
        Check(beetle != null && EnemyBeetle.All.Count == 3, "на поверхні 3 жуки (" + EnemyBeetle.All.Count + ")");
        if (beetle == null) yield break;

        AntSelection.Instance.SelectAllWarriors();
        Check(AntSelection.Instance.Count == CountGroup(AntGroup.Warrior), "WAR вибрала всіх воїнів (" + AntSelection.Instance.Count + ")");
        int sent = 0;
        foreach (AntAgent a in AntSelection.Instance.SelectedAnts)
            if (a.AssignManual(new AttackTask(beetle), null)) sent++;
        Check(sent >= 2, "воїни отримали наказ атакувати " + beetle.name + " (" + sent + ")");

        Vector3Int beetleCell = beetle.Cell;
        GridManager surface = beetle.Grid;
        float attackStart = Time.time;
        float nextLog = 0f;
        while (beetle != null && Time.time - attackStart < 90f)
        {
            if (Time.time >= nextLog)
            {
                nextLog = Time.time + 3f;
                var sb = new StringBuilder("  бій " + (Time.time - attackStart).ToString("0.0") + "с: жук HP " + beetle.Hp + " " + beetle.CurrentState + "; воїни:");
                foreach (AntAgent a in colony.Ants)
                    if (a != null && a.Group == AntGroup.Warrior)
                        sb.Append(" [" + a.CurrentState + " HP " + a.Hp + " " + (a.Grid != null ? a.Grid.LocationName : "?") + "]");
                Line(sb.ToString());
            }
            beetleCell = beetle.Cell;
            yield return null;
        }
        Check(beetle == null, "жука переможено за " + (Time.time - attackStart).ToString("0") + " с гри");
        Check(CountGroup(AntGroup.Warrior) == 2, "двоє воїнів перемогли без втрат (живих воїнів: " + CountGroup(AntGroup.Warrior) + ")");
        yield return null;

        bool meat = false;
        foreach (FoodItem f in Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None))
            if (f.Amount == 8 && f.Grid == surface) meat = true;
        Check(meat, "лишилось м'ясо +8");
        Check(EnemyBeetle.All.Count == 2, "жуків стало 2");

        // --- Відродження через 120 с
        float died = Time.time;
        while (Time.time - died < 118f) yield return null;
        Check(EnemyBeetle.All.Count == 2, "до 120 с новий жук не з'явився");
        while (Time.time - died < 123f) yield return null;
        Check(EnemyBeetle.All.Count == 3, "через 120 с жук відродився (" + EnemyBeetle.All.Count + ")");

        Snapshot("кінець");
    }

    // ---------- Допоміжне ----------

    private static int deathCount;
    private static int Deaths() => deathCount;

    private static AntBuild Make(string body, string legs, string jaw)
    {
        AntPart B = null, L = null, J = null;
        foreach (AntPart p in AntPartCatalog.All)
        {
            if (p.name == body) B = p;
            if (p.name == legs) L = p;
            if (p.name == jaw) J = p;
        }
        return new AntBuild(B, L, J);
    }

    private static int CountGroup(AntGroup g)
    {
        int n = 0;
        foreach (AntAgent a in Colony.Instance.Ants) if (a != null && a.Group == g) n++;
        return n;
    }

    private static AntAgent FindGroup(AntGroup g)
    {
        foreach (AntAgent a in Colony.Instance.Ants) if (a != null && a.Group == g) return a;
        return null;
    }

    private static EnemyBeetle NearestBeetle()
    {
        Portal entry = null;
        foreach (Portal p in Portal.All) if (p.Grid != null && !p.Grid.HasWalls) entry = p;
        EnemyBeetle best = null;
        float bestD = float.MaxValue;
        foreach (EnemyBeetle b in EnemyBeetle.All)
        {
            float d = entry != null ? (b.Cell - entry.Cell).magnitude : 0f;
            if (d < bestD) { bestD = d; best = b; }
        }
        return best;
    }

    // Замовлення тесту на ліміт не потрібні — прибираємо через рефлексію черги.
    private static void ClearOrders(AntQueen queen, Storage storage)
    {
        var f = typeof(AntQueen).GetField("orders", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var q = f != null ? f.GetValue(queen) as System.Collections.ICollection : null;
        if (q != null) f.FieldType.GetMethod("Clear").Invoke(f.GetValue(queen), null);
    }

    private static void Snapshot(string title)
    {
        Colony c = Colony.Instance;
        Line("--- " + title + " (t=" + Time.time.ToString("0.0") + "s, їжа " + Storage.Instance.Food + ", мурах " + c.Ants.Count + ")");
        foreach (AntAgent a in c.Ants)
            if (a != null)
                Line("    " + a.name + ": " + a.Group + " HP " + a.Hp + "/" + a.MaxHp + " " + a.CurrentState + " @" +
                     (a.Grid != null ? a.Grid.LocationName : "?") + " " + a.CurrentCell);
        foreach (EnemyBeetle b in EnemyBeetle.All)
            Line("    " + b.name + ": HP " + b.Hp + " " + b.CurrentState + " @" + b.Cell + " дім " + b.HomeCell);
    }

    // Карта поверхні: # стіна/порожнеча, . підлога, c крихта, B жук, P вхід.
    private static void DumpSurface()
    {
        GridManager surface = null;
        foreach (GridManager g in GridManager.All) if (!g.HasWalls) surface = g;
        if (surface == null) { Line("поверхні немає"); return; }
        BoundsInt b = surface.CellBounds;
        Line("--- поверхня " + surface.LocationName + " bounds " + b.xMin + ".." + (b.xMax - 1) + " x " + b.yMin + ".." + (b.yMax - 1) + ", клітинка " + surface.CellWorldSize);

        var marks = new Dictionary<Vector3Int, char>();
        foreach (FoodItem f in Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None))
            if (f.Grid == surface) marks[f.Cell] = 'c';
        foreach (Portal p in Portal.All) if (p.Grid == surface) marks[p.Cell] = 'P';
        foreach (EnemyBeetle e in EnemyBeetle.All) if (e.Grid == surface) marks[e.HomeCell] = 'B';

        for (int y = b.yMax - 1; y >= b.yMin; y--)
        {
            var row = new StringBuilder("    " + y.ToString().PadLeft(4) + " ");
            for (int x = b.xMin; x < b.xMax; x++)
            {
                var c = new Vector3Int(x, y, 0);
                row.Append(marks.TryGetValue(c, out char m) ? m : (surface.IsWalkable(c) ? '.' : '#'));
            }
            Line(row.ToString());
        }
        foreach (FoodItem f in Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None))
            if (f.Grid == surface) Line("    крихта " + f.Cell);
        foreach (Portal p in Portal.All) Line("    вхід " + p.name + " " + (p.Grid != null ? p.Grid.LocationName : "?") + " " + p.Cell);
        foreach (EnemyBeetle e in EnemyBeetle.All) Line("    жук " + e.name + " дім " + e.HomeCell);
    }
}
