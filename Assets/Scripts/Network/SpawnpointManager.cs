using UnityEngine;

public class SpawnpointManager : MonoBehaviour
{
    public static SpawnpointManager Instance;

    [System.Serializable]
    public class TeamSpawnpoints
    {
        public Team team;
        public Transform[] points;
    }

    [SerializeField] private TeamSpawnpoints[] teamSpawnpoints;

    private void Awake()
    {
        Instance = this;
    }

    public Transform GetSpawnpoint(Team team)
    {
        var entry = GetEntry(team);
        if (entry == null || entry.points == null || entry.points.Length == 0)
            return null;
        return entry.points[Random.Range(0, entry.points.Length)];
    }

    public Transform GetSpawnpoint(Team team, int index)
    {
        var entry = GetEntry(team);
        if (entry == null || entry.points == null || index < 0 || index >= entry.points.Length)
            return null;
        return entry.points[index];
    }

    private TeamSpawnpoints GetEntry(Team team)
    {
        foreach (var entry in teamSpawnpoints)
        {
            if (entry.team == team)
                return entry;
        }
        return null;
    }
}
