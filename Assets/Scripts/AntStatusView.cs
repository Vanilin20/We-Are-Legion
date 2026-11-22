using System.Collections.Generic;
using UnityEngine;

// Те, що висить над мурахою: смужка HP (коли поранена), значок голоду, вантаж.
// Окремий об'єкт, що слідує за мурахою, — щоб не крутився разом із нею.
public class AntStatusView : MonoBehaviour
{
    private const int Order = 4;

    private AntAgent ant;
    private float cell;
    private Transform hpBack, hpFill, hunger;
    private float fillWidth;
    private readonly List<SpriteRenderer> cargoDots = new List<SpriteRenderer>();
    private Sprite cargoCrumb, cargoMeat;

    public static AntStatusView Create(AntAgent ant)
    {
        var go = new GameObject(ant.name + " status");
        SceneUtil.MoveToSceneOf(go, ant.gameObject);
        var v = go.AddComponent<AntStatusView>();
        v.Init(ant);
        return v;
    }

    private void Init(AntAgent a)
    {
        ant = a;
        cell = a.Grid.CellWorldSize;

        // Рамка HP з арту (hp_back 18×4, усередині 16×2) або заготовка.
        if (GameArt.Get("hp_back") != null)
        {
            hpBack = MakeArt("HpBack", "hp_back", 0.72f * cell);
            fillWidth = 0.72f * cell * 16f / 18f;
            hpFill = MakeQuad("HpFill", false, Color.white, new Vector2(fillWidth, 0.72f * cell * 2f / 18f), Order + 1);
        }
        else
        {
            hpBack = MakeQuad("HpBack", false, new Color(0.1f, 0.1f, 0.1f, 0.9f), new Vector2(0.7f, 0.1f) * cell);
            fillWidth = 0.66f * cell;
            hpFill = MakeQuad("HpFill", false, new Color(0.3f, 0.9f, 0.3f), new Vector2(fillWidth, 0.06f * cell), Order + 1);
        }

        hunger = GameArt.Get("icon_hunger") != null
            ? MakeArt("Hunger", "icon_hunger", 0.36f * cell)
            : MakeQuad("Hunger", true, new Color(1f, 0.55f, 0.1f), Vector2.one * 0.22f * cell);

        cargoCrumb = GameArt.Get("cargo_crumb");
        cargoMeat = GameArt.Get("cargo_meat");
        for (int i = 0; i < a.CarryCapacity; i++)
        {
            Transform d = MakeQuad("Cargo" + i, true, new Color(0.95f, 0.85f, 0.45f), Vector2.one * 0.18f * cell);
            cargoDots.Add(d.GetComponent<SpriteRenderer>());
        }
    }

    private Transform MakeArt(string n, string art, float worldWidth)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        GameArt.Apply(go, art, worldWidth, Order);
        return go.transform;
    }

    private Transform MakeQuad(string n, bool round, Color c, Vector2 size, int order = Order)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = round ? PlaceholderSprites.Circle : PlaceholderSprites.Square;
        sr.color = c;
        sr.sortingOrder = order;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        return go.transform;
    }

    void LateUpdate()
    {
        if (ant == null) { Destroy(gameObject); return; }

        if (gameObject.scene != ant.gameObject.scene) SceneUtil.MoveToSceneOf(gameObject, ant.gameObject);
        transform.position = ant.transform.position;
        float top = cell * 0.55f;

        // смужка HP — лише коли поранена
        bool hurt = ant.Hp < ant.MaxHp;
        hpBack.gameObject.SetActive(hurt);
        hpFill.gameObject.SetActive(hurt);
        if (hurt)
        {
            float k = (float)ant.Hp / ant.MaxHp;
            hpBack.localPosition = new Vector3(0f, top, 0f);
            float full = fillWidth;
            hpFill.localScale = new Vector3(full * k, hpFill.localScale.y, 1f);
            hpFill.localPosition = new Vector3(-full * (1f - k) / 2f, top, 0f);
            hpFill.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.9f, 0.3f), k);
        }

        // значок голоду
        hunger.gameObject.SetActive(ant.Hungry);
        hunger.localPosition = new Vector3(0.4f * cell, top + 0.15f * cell, 0f);

        // вантаж над головою
        float y = top + (hurt ? 0.15f : 0f) * cell;
        for (int i = 0; i < cargoDots.Count; i++)
        {
            bool on = i < ant.CargoCount;
            cargoDots[i].gameObject.SetActive(on);
            if (on) SetCargoLook(cargoDots[i], ant.CargoAmountAt(i));
            cargoDots[i].transform.localPosition = new Vector3((i - (ant.CargoCount - 1) / 2f) * 0.22f * cell, y, 0f);
        }
    }

    // Предмет над головою: крихта (1 їжа) або м'ясо (більше).
    private void SetCargoLook(SpriteRenderer sr, int amount)
    {
        Sprite want = amount > 1 ? cargoMeat : cargoCrumb;
        if (want == null || sr.sprite == want) return;
        sr.sprite = want;
        sr.color = Color.white;
        sr.transform.localScale = Vector3.one * (0.22f * cell * (amount > 1 ? 1.2f : 1f) / want.bounds.size.x);
    }
}
