using UnityEngine;

// Заглушки замість арту: білі фігури розміром 1 юніт, колір задає SpriteRenderer.color.
// Коли з'явиться арт — просто признач спрайт в інспекторі, заглушка не створиться.
public static class PlaceholderSprites
{
    private static Sprite square, circle;

    public static Sprite Square => square != null ? square : (square = Make(false));
    public static Sprite Circle => circle != null ? circle : (circle = Make(true));

    private static Sprite Make(bool round)
    {
        const int size = 32;
        var tex = new Texture2D(size, size) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool on = !round || Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) <= r;
            tex.SetPixel(x, y, on ? Color.white : Color.clear);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // SpriteRenderer із заглушкою, якщо спрайта ще немає.
    public static SpriteRenderer Ensure(GameObject go, bool round, Color color, float sizeWorld, int sortingOrder)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        if (sr.sprite == null)
        {
            sr.sprite = round ? Circle : Square;
            sr.color = color;
            go.transform.localScale = Vector3.one * sizeWorld;
        }
        sr.sortingOrder = sortingOrder;
        return sr;
    }
}
