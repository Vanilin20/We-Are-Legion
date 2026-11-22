using UnityEngine;

// Вигляд мурахи з частин конструктора: шари ноги (з анімацією бігу) → тіло → щелепа.
// Спрайти — шари Art/ant_parts.aseprite (Resources/Art/ant_legs_*, ant_body_*, ant_jaw_*).
// Дочірній об'єкт мурахи, крутиться разом із нею. Старий спрайт мурахи ховається.
// Якщо арту немає — кольорові мітки поверх старого спрайта, як раніше.
public class AntLook : MonoBehaviour
{
    private const float RunFps = 10f; // кадрів бігу за секунду при швидкості 2 клітинки/с

    private AntAgent ant;
    private SpriteRenderer antSprite;
    private SpriteRenderer legs, body, jaw, flash;
    private AntBuild build;
    private bool useArt;
    private float runTime;

    // запасні мітки, якщо арту немає
    private SpriteRenderer markBody, markJaw, markLegL, markLegR;

    public static AntLook Attach(AntAgent ant)
    {
        var go = new GameObject("Look");
        go.transform.SetParent(ant.transform, false);
        var look = go.AddComponent<AntLook>();
        look.ant = ant;
        look.antSprite = ant.GetComponent<SpriteRenderer>();
        return look;
    }

    public void Apply(AntBuild b)
    {
        if (b == null) return;
        build = b;
        useArt = b.Body.Sprite() != null && b.Legs.Sprite(0) != null && b.Jaw.Sprite() != null;
        int order = antSprite != null ? antSprite.sortingOrder : 2;

        if (useArt)
        {
            if (legs == null)
            {
                legs = Layer("Legs", order);
                body = Layer("Body", order + 1);
                jaw = Layer("Jaw", order + 2);
                flash = Layer("Flash", order + 3);
            }
            legs.sprite = b.Legs.Sprite(0);
            body.sprite = b.Body.Sprite();
            jaw.sprite = b.Jaw.Sprite();
            flash.sprite = GameArt.Silhouette(body.sprite);
            flash.color = Color.clear;
            if (antSprite != null) antSprite.enabled = false; // старий цілий спрайт більше не потрібен
        }
        else
        {
            MakeMarks(order + 1);
        }
        SetFlash(0f);
    }

    void LateUpdate()
    {
        if (!useArt || ant == null || build == null) return;

        // біг: кадри 1..4, швидкість анімації — як швидкість мурахи; стоїть — кадр 0
        int frame = 0;
        if (ant.CurrentState == AntAgent.State.Move)
        {
            runTime += Time.deltaTime * RunFps * Mathf.Max(0.5f, ant.SpeedCells / 2f);
            frame = 1 + (int)runTime % 4;
        }
        Sprite s = build.Legs.Sprite(frame);
        if (s != null && legs.sprite != s) legs.sprite = s;
    }

    // 0 — звичайний вигляд, 1 — червоний спалах (удар).
    public void SetFlash(float k)
    {
        if (useArt)
        {
            if (flash != null) flash.color = new Color(1f, 0.15f, 0.1f, 0.85f * k);
            Color tint = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.4f), k);
            legs.color = jaw.color = tint;
            return;
        }
        if (markBody == null || build == null) return;
        markBody.color = Color.Lerp(build.Body.color, Color.red, k);
        markLegL.color = markLegR.color = Color.Lerp(build.Legs.color, Color.red, k);
        markJaw.color = Color.Lerp(build.Jaw.color, Color.red, k);
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

    // ---------- Без арту: мітки поверх старого спрайта ----------

    private void MakeMarks(int order)
    {
        if (markBody != null) return;
        float px = antSprite != null && antSprite.sprite != null ? antSprite.sprite.bounds.size.y / 32f : 0.01f;
        markBody = Mark("Body", true, new Vector2(0f, -4f) * px, new Vector2(5f, 6f) * px, order);
        markLegL = Mark("LegL", false, new Vector2(-7f, -2f) * px, new Vector2(2f, 4f) * px, order);
        markLegR = Mark("LegR", false, new Vector2(7f, -2f) * px, new Vector2(2f, 4f) * px, order);
        markJaw = Mark("Jaw", false, new Vector2(0f, 9.5f) * px, new Vector2(5f, 2f) * px, order);
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
}
