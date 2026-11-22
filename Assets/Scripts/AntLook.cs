using UnityEngine;

// Кольорові мітки частин поверх спрайта мурахи (спрайт майже чорний, тонування не видно).
// Черевце — колір тіла, дві бічні мітки — колір ніг, мітка на голові — колір щелепи.
// Дочірній об'єкт мурахи: крутиться разом із нею. Коли з'явиться арт частин — заміниш тут.
public class AntLook : MonoBehaviour
{
    private SpriteRenderer bodyMark, jawMark, legLeft, legRight;
    private Color bodyColor, legsColor, jawColor;

    public static AntLook Attach(AntAgent ant)
    {
        var go = new GameObject("Look");
        go.transform.SetParent(ant.transform, false);
        var look = go.AddComponent<AntLook>();
        look.Init(ant.GetComponent<SpriteRenderer>());
        return look;
    }

    private void Init(SpriteRenderer antSprite)
    {
        int order = antSprite != null ? antSprite.sortingOrder + 1 : 3;
        // Розміри в пікселях спрайта мурахи 32×32 (голова вгорі), переведені в локальні одиниці.
        float px = antSprite != null && antSprite.sprite != null ? antSprite.sprite.bounds.size.y / 32f : 0.01f;

        bodyMark = Mark("Body", true, new Vector2(0f, -4f) * px, new Vector2(5f, 6f) * px, order);
        legLeft = Mark("LegL", false, new Vector2(-7f, -2f) * px, new Vector2(2f, 4f) * px, order);
        legRight = Mark("LegR", false, new Vector2(7f, -2f) * px, new Vector2(2f, 4f) * px, order);
        jawMark = Mark("Jaw", false, new Vector2(0f, 9.5f) * px, new Vector2(5f, 2f) * px, order);
    }

    private SpriteRenderer Mark(string n, bool round, Vector2 pos, Vector2 size, int order)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = round ? PlaceholderSprites.Circle : PlaceholderSprites.Square;
        sr.sortingOrder = order;
        return sr;
    }

    public void Apply(AntBuild build)
    {
        if (build == null) return;
        bodyColor = build.Body.color;
        legsColor = build.Legs.color;
        jawColor = build.Jaw.color;
        SetFlash(0f);
    }

    // 0 — звичайні кольори, 1 — повністю червоні (удар).
    public void SetFlash(float k)
    {
        if (bodyMark == null) return;
        bodyMark.color = Color.Lerp(bodyColor, Color.red, k);
        legLeft.color = legRight.color = Color.Lerp(legsColor, Color.red, k);
        jawMark.color = Color.Lerp(jawColor, Color.red, k);
    }
}
