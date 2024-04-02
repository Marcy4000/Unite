using System.Collections;
using System.Collections.Generic;
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
}
