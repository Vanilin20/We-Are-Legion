using System.Collections.Generic;
using UnityEngine;

// Спрайти з Assets/Resources/Art за назвою файлу. Поки арту немає — повертає null, і код малює заготовку.
// Щоб замінити заготовку своїм малюнком, поклади PNG з такою назвою в Assets/Resources/Art
// (імпорт налаштується сам: ArtImportSettings — Point, без стиснення, 100 px на одиницю):
//   storage       склад, полотно = 1 клітинка (32×32)
//   crumb_0..2    крихта (будь-яка з трьох), 1 клітинка;   meat — м'ясо жука, 1 клітинка
//   queen         королева, полотно = 1.5 клітинки (48×48)
//   larva (або egg)  личинка біля королеви, пів клітинки (16×16), біла — підфарбовується кольором тіла
//   beetle_0, beetle_1   жук (дивиться вгору), 2 кадри ходи
//   icon_hunger   значок голоду;   hp_back — рамка смужки HP (18×4, усередині 16×2)
//   cargo_crumb, cargo_meat   вантаж над головою мурахи
public static class GameArt
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<Sprite, Sprite> silhouettes = new Dictionary<Sprite, Sprite>();

    public static Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!cache.TryGetValue(name, out Sprite s))
        {
            s = Resources.Load<Sprite>("Art/" + name);
            cache[name] = s;
        }
        return s;
    }

    // Поставити спрайт і масштаб так, щоб ширина полотна спрайта дорівнювала worldWidth.
    // false — спрайта немає (лишається заготовка).
    public static bool Apply(GameObject go, string name, float worldWidth, int sortingOrder)
    {
        Sprite s = Get(name);
        if (s == null) return false;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = Color.white;
        sr.sortingOrder = sortingOrder;
        go.transform.localScale = Vector3.one * (worldWidth / s.bounds.size.x);
        return true;
    }

    // Те саме, але лише якщо спрайт ще не призначено в інспекторі. true — спрайт є (свій або з арту).
    public static bool ApplyIfEmpty(GameObject go, string name, float worldWidth, int sortingOrder)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null) return true;
        return Apply(go, name, worldWidth, sortingOrder);
    }

    // Біла копія спрайта (форма без кольору) — для червоного спалаху на темних спрайтах.
    // Потрібен Read/Write у текстури (ставить ArtImportSettings); інакше null.
    public static Sprite Silhouette(Sprite s)
    {
        if (s == null) return null;
        if (silhouettes.TryGetValue(s, out Sprite w)) return w;
        w = null;
        Texture2D src = s.texture;
        if (src != null && src.isReadable)
        {
            Rect r = s.rect;
            Color[] px = src.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, px[i].a);
            var tex = new Texture2D((int)r.width, (int)r.height) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px);
            tex.Apply();
            w = Sprite.Create(tex, new Rect(0, 0, r.width, r.height),
                new Vector2(s.pivot.x / r.width, s.pivot.y / r.height), s.pixelsPerUnit);
        }
        silhouettes[s] = w;
        return w;
    }
}
