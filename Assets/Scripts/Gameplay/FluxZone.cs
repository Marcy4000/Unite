using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluxZone : MonoBehaviour
{
    [SerializeField] private bool orangeTeam;
    [SerializeField] private int laneID;
    [SerializeField] private int tier;

    public bool OrangeTeam => orangeTeam;
    public int LaneID => laneID;
    public int Tier => tier;
}
