using UnityEngine;

public class SpawnpointManager : MonoBehaviour
{
    public static SpawnpointManager Instance;

    [SerializeField] private Transform[] blueTeamPoints;
    [SerializeField] private Transform[] orangeTeamPoints;

    private void Awake()
    {
        Instance = this;
    }

    public Transform GetBlueTeamSpawnpoint()
    {
        return blueTeamPoints[Random.Range(0, blueTeamPoints.Length)];
    }

    public Transform GetOrangeTeamSpawnpoint()
    {
        return orangeTeamPoints[Random.Range(0, orangeTeamPoints.Length)];
    }

    public Transform GetBlueTeamSpawnpoint(int index)
    {
        if (index < 0 || index >= blueTeamPoints.Length)
        {
            return null;
        }

        return blueTeamPoints[index];
    }

    public Transform GetOrangeTeamSpawnpoint(int index)
    {
        if (index < 0 || index >= orangeTeamPoints.Length)
        {
            return null;
        }

        return orangeTeamPoints[index];
    }
}
