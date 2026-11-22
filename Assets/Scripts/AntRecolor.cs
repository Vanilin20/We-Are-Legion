using System.Collections.Generic;
using UnityEngine;

// Перефарбування основної моделі мурахи (Assets/ant: ant.png і кадри бігу) за частинами конструктора.
// Кожен піксель кадру належить тілу, ногам або щелепі:
//   тіло    — товста частина (голова, груди, черевце) + вусики;
//   ноги    — тонкі лінії збоку;
//   щелепа  — передній край голови й кліщі перед нею.
// Базова частина лишає пікселі як є (чорна мураха), інша — фарбує в колір частини (AntPart.color), зберігаючи світлотінь.
// Текстури мурахи мають бути Read/Write (ставить ArtImportSettings).
public static class AntRecolor
{
    private enum Region : byte { None, Body, Legs, Jaw }

    private static readonly Dictionary<Sprite, Region[]> regions = new Dictionary<Sprite, Region[]>();
    private static readonly Dictionary<(Sprite, AntPart, AntPart, AntPart), Sprite> cache =
        new Dictionary<(Sprite, AntPart, AntPart, AntPart), Sprite>();
    private static readonly List<Sprite> frames = new List<Sprite>();

    // Кадри основної моделі, які вже траплялись у грі (для превʼю в конструкторі).
    public static IReadOnlyList<Sprite> Frames => frames;

    public static void Register(Sprite original)
    {
        if (original != null && !frames.Contains(original)) frames.Add(original);
    }

    // Перефарбований кадр. null — текстура не читається (тоді показуємо оригінал).
    public static Sprite Get(Sprite original, AntBuild build)
    {
        if (original == null || build == null) return null;
        if (build.Body.isBasic && build.Legs.isBasic && build.Jaw.isBasic) return original;

        var key = (original, build.Body, build.Legs, build.Jaw);
        if (cache.TryGetValue(key, out Sprite done)) return done;

        Sprite result = null;
        Texture2D src = original.texture;
        if (src != null && src.isReadable)
        {
            Rect r = original.rect;
            int w = (int)r.width, h = (int)r.height;
            Color[] px = src.GetPixels((int)r.x, (int)r.y, w, h);
            Region[] map = Classify(original, px, w, h);

            for (int i = 0; i < px.Length; i++)
            {
                AntPart part = map[i] == Region.Body ? build.Body : map[i] == Region.Legs ? build.Legs : map[i] == Region.Jaw ? build.Jaw : null;
                if (part == null || part.isBasic) continue;
                Color p = px[i];
                float lum = (p.r + p.g + p.b) / 3f;
                // чорне -> темний відтінок кольору, сірі відблиски -> світлий; щелепа трохи яскравіша
                float k = Mathf.Min(1f, (map[i] == Region.Jaw ? 0.75f : 0.45f) + 2.6f * lum);
                px[i] = new Color(part.color.r * k, part.color.g * k, part.color.b * k, p.a);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px);
            tex.Apply();
            result = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(original.pivot.x / w, original.pivot.y / h), original.pixelsPerUnit);
            result.name = original.name + " (" + build.Body.name + "+" + build.Legs.name + "+" + build.Jaw.name + ")";
        }
        cache[key] = result;
        return result;
    }

    // Де тіло, де ноги, де щелепа. Тіло — те, що лишається після «розмикання» 3×3 (тонкі лінії зникають).
    private static Region[] Classify(Sprite s, Color[] px, int w, int h)
    {
        if (regions.TryGetValue(s, out Region[] cached)) return cached;

        var eroded = new bool[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool all = true;
            for (int dy = -1; dy <= 1 && all; dy++)
            for (int dx = -1; dx <= 1 && all; dx++)
                if (!Opaque(px, w, h, x + dx, y + dy)) all = false;
            eroded[y * w + x] = all;
        }

        var core = new bool[w * h];
        int top = int.MinValue, bottom = int.MaxValue;
        float sumX = 0f;
        int count = 0;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (!Opaque(px, w, h, x, y)) continue;
            bool near = false;
            for (int dy = -1; dy <= 1 && !near; dy++)
            for (int dx = -1; dx <= 1 && !near; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < w && ny < h && eroded[ny * w + nx]) near = true;
            }
            if (!near) continue;
            core[y * w + x] = true;
            top = Mathf.Max(top, y);       // у текстурі y іде вгору: голова — найбільший y
            bottom = Mathf.Min(bottom, y);
            sumX += x;
            count++;
        }

        var map = new Region[w * h];
        float cx = count > 0 ? sumX / count : w / 2f;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            if (!Opaque(px, w, h, x, y)) continue;
            if (count == 0) { map[i] = Region.Body; continue; }
            if (core[i]) map[i] = y >= top ? Region.Jaw : Region.Body;              // передній край голови — щелепа
            else if (y > top) map[i] = Mathf.Abs(x - cx) <= 2f ? Region.Jaw : Region.Body; // кліщі / вусики
            else if (y < bottom && Mathf.Abs(x - cx) <= 1.5f) map[i] = Region.Body;      // кінчик черевця
            else map[i] = Region.Legs;
        }
        regions[s] = map;
        return map;
    }

    private static bool Opaque(Color[] px, int w, int h, int x, int y)
    {
        return x >= 0 && y >= 0 && x < w && y < h && px[y * w + x].a > 0f;
    }
}
