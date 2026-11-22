using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Меню We Are Legion: один раз розставляє в сценах те, що потрібно для конструктора, королеви й жуків.
// Можна запускати повторно — те, що вже є, не дублюється.
public static class WeAreLegionSetup
{
    private const string AnthillScenePath = "Assets/Scenes/SampleScene.unity";
    private const string SurfaceScenePath = "Assets/Scenes/Outdoor.unity";
    private const string PartsFolder = "Assets/Resources/" + AntPartCatalog.ResourcesFolder;

    [MenuItem("We Are Legion/Налаштувати конструктор, королеву й жуків")]
    public static void SetupAll()
    {
        var log = new StringBuilder();
        CreatePartAssets(log);

        Scene anthill = OpenScene(AnthillScenePath, OpenSceneMode.Single);
        if (!anthill.IsValid()) return;
        Scene surface = OpenScene(SurfaceScenePath, OpenSceneMode.Additive);

        SetupAnthill(anthill, log);
        if (surface.IsValid()) SetupSurface(surface, log);

        EditorSceneManager.MarkSceneDirty(anthill);
        if (surface.IsValid()) EditorSceneManager.MarkSceneDirty(surface);

        log.AppendLine();
        log.AppendLine("Перевір розташування королеви й точок жуків, потім збережи сцени (Ctrl+S).");
        log.AppendLine("Сцена Outdoor лишилась відкритою поруч — це не заважає грі.");
        EditorUtility.DisplayDialog("We Are Legion", log.ToString(), "OK");
    }

    [MenuItem("We Are Legion/Створити ассети частин мурах")]
    public static void CreatePartAssetsMenu()
    {
        var log = new StringBuilder();
        CreatePartAssets(log);
        EditorUtility.DisplayDialog("We Are Legion", log.ToString(), "OK");
    }

    // ---------- Частини ----------

    private static void CreatePartAssets(StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(PartsFolder)) AssetDatabase.CreateFolder("Assets/Resources", AntPartCatalog.ResourcesFolder);

        int made = 0;
        foreach (AntPart p in AntPartCatalog.CreateDefaults())
        {
            string path = PartsFolder + "/" + p.name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<AntPart>(path) != null) continue;
            AssetDatabase.CreateAsset(p, path);
            made++;
        }
        AssetDatabase.SaveAssets();
        log.AppendLine(made > 0
            ? "Частини: створено " + made + " ассетів у " + PartsFolder
            : "Частини: ассети вже є в " + PartsFolder);
    }

    // ---------- Мурашник ----------

    private static void SetupAnthill(Scene scene, StringBuilder log)
    {
        // Королева біля складу
        if (Find<AntQueen>(scene) == null)
        {
            Storage storage = Find<Storage>(scene);
            float cell = CellSize(scene);
            Vector3 pos = storage != null ? storage.transform.position + new Vector3(cell * 2f, 0f, 0f) : Vector3.zero;

            var go = new GameObject("Queen");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = pos;
            go.AddComponent<AntQueen>();
            Undo.RegisterCreatedObjectUndo(go, "Queen");
            log.AppendLine("Королева: додано поруч зі складом (у грі стане на найближчу розкопану клітинку).");
        }
        else log.AppendLine("Королева: вже є.");

        // Вікно конструктора на Canvas — тому, де кнопка DIG (у джойстика свій Canvas)
        DigInput dig = Find<DigInput>(scene);
        AntSelection selection = Find<AntSelection>(scene);
        Canvas canvas = dig != null && dig.GetComponentInParent<Canvas>() != null
            ? dig.GetComponentInParent<Canvas>().rootCanvas
            : Find<Canvas>(scene);
        if (canvas == null)
        {
            log.AppendLine("Canvas не знайдено — вікно конструктора й кнопки не додано.");
            return;
        }

        ConstructorWindow window = canvas.GetComponent<ConstructorWindow>();
        if (window == null)
        {
            window = Undo.AddComponent<ConstructorWindow>(canvas.gameObject);
            log.AppendLine("Конструктор: компонент додано на Canvas.");
        }
        else log.AppendLine("Конструктор: вже є на Canvas.");

        // Кнопки ANT і WAR — копії кнопки DIG, над кнопкою локацій
        if (dig == null)
        {
            log.AppendLine("Кнопку DIG не знайдено — додай кнопки ANT і WAR вручну (ConstructorWindow.Toggle, AntSelection.SelectAllWarriors).");
        }
        else
        {
            if (FindButton(canvas, "AntButton") == null)
            {
                MakeButton(dig, "AntButton", "ANT", 140f, window, nameof(ConstructorWindow.Toggle));
                log.AppendLine("Кнопка ANT: додано (відкриває конструктор).");
            }
            if (selection != null && FindButton(canvas, "WarButton") == null)
            {
                MakeButton(dig, "WarButton", "WAR", 210f, selection, nameof(AntSelection.SelectAllWarriors));
                log.AppendLine("Кнопка WAR: додано (вибирає всіх воїнів).");
            }
        }

        // HUD тепер у три рядки
        ColonyHud hud = Find<ColonyHud>(scene);
        if (hud != null)
        {
            var rt = (RectTransform)hud.transform;
            if (rt.sizeDelta.y < 110f)
            {
                Undo.RecordObject(rt, "HUD size");
                rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 700f), 110f);
            }
        }
    }

    private static void MakeButton(DigInput source, string objectName, string label, float dy, Object target, string method)
    {
        GameObject go = Object.Instantiate(source.gameObject, source.transform.parent);
        go.name = objectName;
        Undo.RegisterCreatedObjectUndo(go, objectName);

        Object.DestroyImmediate(go.GetComponent<DigInput>());

        var rt = (RectTransform)go.transform;
        var src = (RectTransform)source.transform;
        rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, dy);

        TMP_Text t = go.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.text = label;

        Button b = go.GetComponent<Button>();
        if (b != null)
        {
            b.onClick = new Button.ButtonClickedEvent();
            var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), target, method);
            UnityEventTools.AddPersistentListener(b.onClick, action);
        }
        Image img = go.GetComponent<Image>();
        if (img != null) img.color = Color.white;
    }

    private static Transform FindButton(Canvas canvas, string n)
    {
        foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }

    // ---------- Поверхня ----------

    [MenuItem("We Are Legion/Перерозставити жуків на поверхні")]
    public static void ReplaceBeetles()
    {
        Scene surface = OpenScene(SurfaceScenePath, OpenSceneMode.Additive);
        if (!surface.IsValid()) return;
        foreach (BeetleSpawner old in FindAll<BeetleSpawner>(surface))
            Undo.DestroyObjectImmediate(old.gameObject);

        var log = new StringBuilder();
        SetupSurface(surface, log);
        EditorSceneManager.MarkSceneDirty(surface);
        EditorUtility.DisplayDialog("We Are Legion", log.ToString() + "\nЗбережи сцену Outdoor (Ctrl+S).", "OK");
    }

    // Жуки стоять подалі від входу, кожен — біля власної купки крихт (вона під охороною).
    // Крихти біля входу лишаються вільними: їх робітники беруть без бою (GDD: «їжу за межами радіуса можна брати без бою»).
    private const int GuardDistMin = 14, GuardDistMax = 30; // клітинок від входу
    private const int FreeFoodClearance = 10;               // клітинок від вільних крихт

    private static void SetupSurface(Scene scene, StringBuilder log)
    {
        if (Find<BeetleSpawner>(scene) != null)
        {
            log.AppendLine("Жуки: BeetleSpawner вже є на поверхні (переставити — меню «Перерозставити жуків»).");
            return;
        }

        GridManager gm = Find<GridManager>(scene);
        Tilemap floor = gm != null ? new SerializedObject(gm).FindProperty("floorTilemap").objectReferenceValue as Tilemap : null;
        if (floor == null)
        {
            log.AppendLine("Жуки: на поверхні немає GridManager з floorTilemap — не додано.");
            return;
        }

        Portal portal = Find<Portal>(scene);
        Vector3Int entry = portal != null ? floor.WorldToCell(portal.transform.position) : Vector3Int.zero;

        var crumbs = new List<Vector3Int>();
        foreach (CrumbSpot c in FindAll<CrumbSpot>(scene))
            if (c.GetComponentInParent<BeetleSpawner>() == null) crumbs.Add(floor.WorldToCell(c.transform.position));

        List<Vector3Int> points = PickGuardCells(floor, entry, crumbs);

        var root = new GameObject("Beetles");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<BeetleSpawner>();

        // купка крихт біля кожної точки — у радіусі охорони (3 клітинки)
        Vector3Int[] food = { new Vector3Int(2, 0, 0), new Vector3Int(-1, 2, 0), new Vector3Int(-1, -2, 0) };
        for (int i = 0; i < points.Count; i++)
        {
            var p = new GameObject("GuardPoint " + (i + 1));
            p.transform.SetParent(root.transform, false);
            p.transform.position = floor.GetCellCenterWorld(points[i]);
            for (int k = 0; k < food.Length; k++)
            {
                Vector3Int fc = points[i] + food[k];
                if (!floor.HasTile(fc)) continue;
                var f = new GameObject("GuardedCrumb " + (k + 1));
                f.transform.SetParent(p.transform, false);
                f.transform.position = floor.GetCellCenterWorld(fc);
                f.AddComponent<CrumbSpot>();
            }
        }
        Undo.RegisterCreatedObjectUndo(root, "Beetles");
        log.AppendLine("Жуки: 3 точки охорони за " + GuardDistMin + "–" + GuardDistMax + " клітинок від входу " + entry +
                       ", біля кожної — 3 крихти під охороною. " + crumbs.Count + " крихт біля входу лишились вільними.");
        foreach (Vector3Int c in points) log.AppendLine("   точка " + c);
    }

    // Три напрямки від входу (через 120°); на кожному — перша клітинка з вільним місцем навколо, далеко від вільних крихт.
    private static List<Vector3Int> PickGuardCells(Tilemap floor, Vector3Int entry, List<Vector3Int> crumbs)
    {
        var result = new List<Vector3Int>();
        for (int k = 0; k < 3; k++)
        {
            bool found = false;
            for (int turn = 0; turn < 24 && !found; turn++)
            {
                // 0, +15, -15, +30, -30 ... градусів від базового напрямку
                float off = ((turn + 1) / 2) * 15f * (turn % 2 == 0 ? 1f : -1f);
                float a = (90f + k * 120f + off) * Mathf.Deg2Rad;
                for (int r = GuardDistMin + 4; r <= GuardDistMax && !found; r++)
                {
                    var c = new Vector3Int(entry.x + Mathf.RoundToInt(Mathf.Cos(a) * r), entry.y + Mathf.RoundToInt(Mathf.Sin(a) * r), 0);
                    if (!GoodGuardCell(floor, c, crumbs, result)) continue;
                    result.Add(c);
                    found = true;
                }
            }
            if (!found) result.Add(entry + new Vector3Int(GuardDistMin * (k - 1), GuardDistMin, 0)); // запасний варіант
        }
        return result;
    }

    private static bool GoodGuardCell(Tilemap floor, Vector3Int c, List<Vector3Int> crumbs, List<Vector3Int> taken)
    {
        for (int x = -3; x <= 3; x++)
        for (int y = -3; y <= 3; y++)
            if (!floor.HasTile(new Vector3Int(c.x + x, c.y + y, 0))) return false; // довкола має бути підлога
        foreach (Vector3Int f in crumbs)
            if (Dist(c, f) < FreeFoodClearance) return false;
        foreach (Vector3Int t in taken)
            if (Dist(c, t) < 12f) return false;
        return true;
    }

    private static float Dist(Vector3Int a, Vector3Int b)
    {
        float dx = a.x - b.x, dy = a.y - b.y;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // ---------- Службове ----------

    private static Scene OpenScene(string path, OpenSceneMode mode)
    {
        Scene s = SceneManager.GetSceneByPath(path);
        if (s.IsValid() && s.isLoaded) return s;

        if (mode == OpenSceneMode.Single && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return default;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            Debug.LogError("WeAreLegionSetup: немає сцени " + path);
            return default;
        }
        return EditorSceneManager.OpenScene(path, mode);
    }

    private static float CellSize(Scene scene)
    {
        Grid grid = Find<Grid>(scene);
        if (grid == null) return 1f;
        return grid.cellSize.x * grid.transform.lossyScale.x;
    }

    private static T Find<T>(Scene scene) where T : Component
    {
        List<T> all = FindAll<T>(scene);
        return all.Count > 0 ? all[0] : null;
    }

    private static List<T> FindAll<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            list.AddRange(root.GetComponentsInChildren<T>(true));
        return list;
    }
}
