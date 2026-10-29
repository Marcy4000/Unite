using System;
using UnityEngine;

public class BotRaceRoute : MonoBehaviour
{
    [Serializable] public class Segment
    {
        public RaceCheckpoint checkpoint;
        public Vector3[] guides = new Vector3[0];
    }
    public Segment[] segments = new Segment[0];
    public static BotRaceRoute Instance { get; private set; }
    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public Segment Next(int checkpoint)
    {
        for (int i = 0; i < segments.Length; i++)
            if (segments[i].checkpoint != null && segments[i].checkpoint.CheckpointIndex == checkpoint) return segments[i];
        return null;
    }
}
