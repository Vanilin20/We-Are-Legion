using System.Collections.Generic;
using UnityEngine;

// Колонія: усі мурахи й черги завдань по групах.
public class Colony : MonoBehaviour
{
    public static Colony Instance { get; private set; }

    private readonly Dictionary<AntGroup, TaskQueue> queues = new Dictionary<AntGroup, TaskQueue>();
    private readonly List<AntAgent> ants = new List<AntAgent>();

    public IReadOnlyList<AntAgent> Ants => ants;

    void Awake()
    {
        Instance = this;
        foreach (AntGroup g in System.Enum.GetValues(typeof(AntGroup)))
            queues[g] = new TaskQueue();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public TaskQueue Queue(AntGroup group) => queues[group];

    public void Register(AntAgent ant)
    {
        if (!ants.Contains(ant)) ants.Add(ant);
    }

    public void Unregister(AntAgent ant) => ants.Remove(ant);

    // Чи зайнята клітинка іншою мурахою (вона там стоїть, працює або вже туди йде).
    // Мурахи в русі можуть проходити одна крізь одну — займають лише місце, де зупиняться.
    public bool IsReservedByOther(Vector3Int cell, AntAgent me)
    {
        foreach (AntAgent a in ants)
            if (a != me && a.enabled && a.RestCell == cell) return true;
        return false;
    }

    public int CountIn(AntGroup group)
    {
        int n = 0;
        foreach (AntAgent a in ants) if (a.Group == group) n++;
        return n;
    }
}
