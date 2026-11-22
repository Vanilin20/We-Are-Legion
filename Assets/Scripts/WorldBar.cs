using UnityEngine;

// Проста смужка у світі (здоров'я жука, дозрівання яйця). Заповнюється зліва направо.
public class WorldBar : MonoBehaviour
{
    private Transform fill;
    private SpriteRenderer fillRenderer;
    private float width;

    public static WorldBar Create(Transform parent, Vector3 localPos, float width, float height, Color fillColor, int order)
    {
        var go = new GameObject("Bar");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var bar = go.AddComponent<WorldBar>();
        bar.width = width;

        var back = new GameObject("Back");
        back.transform.SetParent(go.transform, false);
        var br = back.AddComponent<SpriteRenderer>();
        br.sortingOrder = order;
        Sprite frame = GameArt.Get("hp_back"); // рамка 18×4, усередині 16×2
        if (frame != null)
        {
            float backW = width * 18f / 16f;
            br.sprite = frame;
            back.transform.localScale = Vector3.one * (backW / frame.bounds.size.x);
            height = backW * 2f / 18f;
        }
        else
        {
            back.transform.localScale = new Vector3(width + height * 0.4f, height * 1.6f, 1f);
            br.sprite = PlaceholderSprites.Square;
            br.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        }

        var f = new GameObject("Fill");
        f.transform.SetParent(go.transform, false);
        f.transform.localScale = new Vector3(width, height, 1f);
        bar.fill = f.transform;
        bar.fillRenderer = f.AddComponent<SpriteRenderer>();
        bar.fillRenderer.sprite = PlaceholderSprites.Square;
        bar.fillRenderer.color = fillColor;
        bar.fillRenderer.sortingOrder = order + 1;
        return bar;
    }

    // k — від 0 до 1.
    public void Set(float k)
    {
        k = Mathf.Clamp01(k);
        fill.localScale = new Vector3(width * k, fill.localScale.y, 1f);
        fill.localPosition = new Vector3(-width * (1f - k) / 2f, 0f, 0f);
    }

    public void SetColor(Color c) => fillRenderer.color = c;

    public void SetVisible(bool on)
    {
        if (gameObject.activeSelf != on) gameObject.SetActive(on);
    }
}
