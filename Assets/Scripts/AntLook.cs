using UnityEngine;

// Вигляд мурахи: основна модель (ant.png і кадри бігу з Animator), перефарбована за частинами конструктора
// (див. AntRecolor). Animator і далі міняє кадри на оригінальному SpriteRenderer — ми його ховаємо
// й показуємо перефарбовану копію поточного кадру в дочірньому об'єкті.
// Якщо перефарбувати не вдається (текстура без Read/Write) — видно оригінал.
public class AntLook : MonoBehaviour
{
    private AntAgent ant;
    private SpriteRenderer antSprite, look, flash;
    private AntBuild build;
    private float flashK;

    public static AntLook Attach(AntAgent ant)
    {
        var go = new GameObject("Look");
        go.transform.SetParent(ant.transform, false);
        var l = go.AddComponent<AntLook>();
        l.ant = ant;
        l.antSprite = ant.GetComponent<SpriteRenderer>();
        int order = l.antSprite != null ? l.antSprite.sortingOrder : 2;
        l.look = l.Layer("Recolored", order);
        l.flash = l.Layer("Flash", order + 1);
        l.flash.enabled = false;
        return l;
    }

    public void Apply(AntBuild b)
    {
        build = b;
        Refresh();
    }

    void LateUpdate() => Refresh(); // Animator уже поставив кадр цього кадру

    private void Refresh()
    {
        if (antSprite == null || build == null) return;
        Sprite src = antSprite.sprite;
        if (src == null) return;
        AntRecolor.Register(src);

        Sprite recolored = AntRecolor.Get(src, build);
        bool ok = recolored != null;
        antSprite.enabled = !ok;
        look.enabled = ok;
        if (!ok) return;

        look.sprite = recolored;
        look.flipX = antSprite.flipX;
        look.flipY = antSprite.flipY;
        if (flashK > 0f) flash.sprite = GameArt.Silhouette(recolored);
    }

    // 0 — звичайний вигляд, 1 — червоний спалах (удар). Чорну мураху не підфарбуєш множенням, тому — білий силует поверх.
    public void SetFlash(float k)
    {
        flashK = k;
        if (flash == null) return;
        flash.enabled = k > 0f;
        if (k <= 0f) return;
        flash.sprite = GameArt.Silhouette(look.enabled ? look.sprite : antSprite != null ? antSprite.sprite : null);
        flash.color = new Color(1f, 0.15f, 0.1f, 0.85f * k);
    }

    private SpriteRenderer Layer(string n, int order)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        if (antSprite != null) sr.sortingLayerID = antSprite.sortingLayerID;
        return sr;
    }
}
