using System.Collections.Generic;
using UnityEngine;

// Слот конструктора: тіло, ноги, щелепа.
public enum AntPartSlot { Body, Legs, Jaw }

// Одна частина мурахи (мутація). Числа — з таблиці «Властивості гравця» в GDD.
// Ассети лежать у Assets/Resources/AntParts (створює меню We Are Legion → Створити ассети частин).
// Кожна частина заповнює лише свої поля: тіло — здоров'я й модифікатор швидкості,
// ноги — швидкість і вантаж, щелепа — копання й шкоду.
[CreateAssetMenu(menuName = "We Are Legion/Ant Part", fileName = "AntPart")]
public class AntPart : ScriptableObject
{
    [Header("Що це")]
    public AntPartSlot slot;
    [Tooltip("Напис у конструкторі (латиницею, бо в шрифті поки немає кирилиці)")]
    public string displayName = "BASIC";
    [Tooltip("Базова частина безкоштовна; кожна неосновна додає 3 їжі (AntBuild.ExtraPartCost)")]
    public bool isBasic;
    [Tooltip("Порядок кнопки в рядку конструктора")]
    public int order;
    [Tooltip("Колір кнопки й колір, яким перефарбовується ця частина мурахи (базова частина лишається чорною)")]
    public Color color = Color.white;

    [Header("Тіло")]
    public int hp = 4;
    [Tooltip("Додається до швидкості ніг, клітинок/с")]
    public float speedBonus;

    [Header("Ноги")]
    [Tooltip("Клітинок за секунду")]
    public float speed = 2f;
    [Tooltip("Скільки предметів несе за раз")]
    public int carry = 2;

    [Header("Щелепа")]
    [Tooltip("Секунд на одну клітинку")]
    public float digTime = 1f;
    [Tooltip("Шкода за удар (удар раз на 1 с)")]
    public int damage = 1;

    public int Cost => isBasic ? 0 : AntBuild.ExtraPartCost;
}

// Зібрана мураха: по одній частині в кожному слоті. Стати рахуються з частин.
public sealed class AntBuild
{
    public const int BaseCost = 5;      // базова мураха
    public const int ExtraPartCost = 3; // кожна неосновна частина

    public AntPart Body { get; }
    public AntPart Legs { get; }
    public AntPart Jaw { get; }

    public AntBuild(AntPart body, AntPart legs, AntPart jaw)
    {
        Body = body;
        Legs = legs;
        Jaw = jaw;
    }

    public int Hp => Mathf.Max(1, Body.hp);
    // Швидкість мурахи = швидкість ніг + модифікатор тіла.
    public float Speed => Mathf.Max(0.25f, Legs.speed + Body.speedBonus);
    public int Carry => Mathf.Max(1, Legs.carry);
    public float DigTime => Mathf.Max(0.05f, Jaw.digTime);
    public int Damage => Mathf.Max(0, Jaw.damage);
    public int Cost => BaseCost + Body.Cost + Legs.Cost + Jaw.Cost;

    public AntPart Get(AntPartSlot slot)
    {
        switch (slot)
        {
            case AntPartSlot.Body: return Body;
            case AntPartSlot.Legs: return Legs;
            default: return Jaw;
        }
    }

    public AntBuild With(AntPart part)
    {
        switch (part.slot)
        {
            case AntPartSlot.Body: return new AntBuild(part, Legs, Jaw);
            case AntPartSlot.Legs: return new AntBuild(Body, part, Jaw);
            default: return new AntBuild(Body, Legs, part);
        }
    }

    // Базова мураха (AntBasic): просте тіло, базові ноги, базова щелепа.
    public static AntBuild Basic => new AntBuild(
        AntPartCatalog.Basic(AntPartSlot.Body),
        AntPartCatalog.Basic(AntPartSlot.Legs),
        AntPartCatalog.Basic(AntPartSlot.Jaw));
}

// Усі частини гри. Беруться з Resources/AntParts; якщо ассетів ще немає — зі стартових чисел нижче.
public static class AntPartCatalog
{
    public const string ResourcesFolder = "AntParts";

    private static List<AntPart> parts;

    public static IReadOnlyList<AntPart> All
    {
        get { Load(); return parts; }
    }

    public static List<AntPart> Of(AntPartSlot slot)
    {
        Load();
        var list = new List<AntPart>();
        foreach (AntPart p in parts) if (p.slot == slot) list.Add(p);
        list.Sort((a, b) => a.order.CompareTo(b.order));
        return list;
    }

    public static AntPart Basic(AntPartSlot slot)
    {
        List<AntPart> list = Of(slot);
        foreach (AntPart p in list) if (p.isBasic) return p;
        return list.Count > 0 ? list[0] : null;
    }

    private static void Load()
    {
        if (parts != null) return;
        parts = new List<AntPart>(Resources.LoadAll<AntPart>(ResourcesFolder));

        // Немає ассетів (або бракує цілого слоту) — беремо стартові числа з GDD.
        bool complete = parts.Count > 0;
        foreach (AntPartSlot s in System.Enum.GetValues(typeof(AntPartSlot)))
            if (!parts.Exists(p => p.slot == s)) complete = false;
        if (!complete)
        {
            if (parts.Count > 0) Debug.LogWarning("AntPartCatalog: у Resources/" + ResourcesFolder + " бракує частин, беру стартові");
            parts = CreateDefaults();
        }
    }

    // Стартові значення для першого playtest (GDD, 06.11.2026).
    // Назви ассетів (name) — для меню, яке їх створює.
    public static List<AntPart> CreateDefaults()
    {
        var list = new List<AntPart>
        {
            Make("Body_Simple",  AntPartSlot.Body, "SIMPLE",  true,  0, new Color(0.72f, 0.56f, 0.40f), hp: 4, speedBonus: 0f),
            Make("Body_Armored", AntPartSlot.Body, "ARMORED", false, 1, new Color(0.45f, 0.58f, 0.78f), hp: 8, speedBonus: -0.5f),
            Make("Body_Light",   AntPartSlot.Body, "LIGHT",   false, 2, new Color(0.55f, 0.86f, 0.45f), hp: 2, speedBonus: 0.5f),

            Make("Legs_Basic",   AntPartSlot.Legs, "BASIC",   true,  0, new Color(0.78f, 0.78f, 0.74f), speed: 2f,   carry: 2),
            Make("Legs_Cargo",   AntPartSlot.Legs, "CARGO",   false, 1, new Color(0.92f, 0.68f, 0.20f), speed: 1.5f, carry: 4),
            Make("Legs_Fast",    AntPartSlot.Legs, "FAST",    false, 2, new Color(0.35f, 0.82f, 0.92f), speed: 3f,   carry: 1),

            Make("Jaw_Basic",    AntPartSlot.Jaw,  "BASIC",   true,  0, new Color(0.96f, 0.93f, 0.82f), digTime: 1f,   damage: 1),
            Make("Jaw_Digger",   AntPartSlot.Jaw,  "DIGGER",  false, 1, new Color(0.78f, 0.58f, 0.95f), digTime: 0.5f, damage: 1),
            Make("Jaw_Fighter",  AntPartSlot.Jaw,  "FIGHTER", false, 2, new Color(0.95f, 0.30f, 0.28f), digTime: 1.5f, damage: 2),
        };
        return list;
    }

    private static AntPart Make(string assetName, AntPartSlot slot, string display, bool basic, int order, Color color,
        int hp = 4, float speedBonus = 0f, float speed = 2f, int carry = 2, float digTime = 1f, int damage = 1)
    {
        var p = ScriptableObject.CreateInstance<AntPart>();
        p.name = assetName;
        p.slot = slot;
        p.displayName = display;
        p.isBasic = basic;
        p.order = order;
        p.color = color;
        p.hp = hp;
        p.speedBonus = speedBonus;
        p.speed = speed;
        p.carry = carry;
        p.digTime = digTime;
        p.damage = damage;
        return p;
    }
}
