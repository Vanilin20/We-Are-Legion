using System.Collections.Generic;
using UnityEngine;

// Тримає на поверхні count жуків. Точки охорони — дочірні об'єкти (або список guardPoints);
// став їх біля скупчень їжі. Переможений жук відроджується через respawnTime у випадковій вільній точці.
public class BeetleSpawner : MonoBehaviour
{
    [SerializeField] private int count = 3;
    [SerializeField] private float respawnTime = 120f;
    [Tooltip("Порожньо — беруться дочірні об'єкти")]
    [SerializeField] private Transform[] guardPoints;
    [Tooltip("Необов'язково: префаб жука (з арт-спрайтом). Порожньо — жук-заглушка")]
    [SerializeField] private EnemyBeetle beetlePrefab;

    private readonly List<Transform> points = new List<Transform>();
    private readonly Dictionary<EnemyBeetle, Transform> beetleAt = new Dictionary<EnemyBeetle, Transform>();
    private readonly List<float> respawnTimers = new List<float>();

    // Для HUD/налагодження: скільки живих і коли наступний.
    public int AliveCount => beetleAt.Count;

    void Start()
    {
        if (guardPoints != null)
            foreach (Transform t in guardPoints) if (t != null) points.Add(t);
        if (points.Count == 0)
            foreach (Transform t in transform) points.Add(t);
        if (points.Count == 0) points.Add(transform);

        for (int i = 0; i < count; i++) Spawn(points[i % points.Count]);
    }

    void Update()
    {
        for (int i = respawnTimers.Count - 1; i >= 0; i--)
        {
            respawnTimers[i] -= Time.deltaTime;
            if (respawnTimers[i] > 0f) continue;
            respawnTimers.RemoveAt(i);
            Spawn(RandomFreePoint());
        }
    }

    private Transform RandomFreePoint()
    {
        var free = new List<Transform>();
        foreach (Transform p in points)
            if (!beetleAt.ContainsValue(p)) free.Add(p);
        List<Transform> from = free.Count > 0 ? free : points;
        return from[Random.Range(0, from.Count)];
    }

    private void Spawn(Transform point)
    {
        EnemyBeetle b;
        if (beetlePrefab != null)
        {
            b = Instantiate(beetlePrefab, point.position, Quaternion.identity);
        }
        else
        {
            var go = new GameObject("Beetle");
            go.transform.position = point.position;
            b = go.AddComponent<EnemyBeetle>();
        }
        SceneUtil.MoveToSceneOf(b.gameObject, gameObject);
        b.name = "Beetle (" + point.name + ")";
        beetleAt[b] = point;
        b.Died += OnDied;
    }

    private void OnDied(EnemyBeetle b)
    {
        beetleAt.Remove(b);
        respawnTimers.Add(respawnTime);
    }
}
