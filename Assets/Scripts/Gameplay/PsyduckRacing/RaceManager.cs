using JSAM;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class RaceManager : NetworkBehaviour
{
    public static RaceManager Instance { get; private set; }

    public static readonly int TOTAL_LAPS = 1;

    [SerializeField] private GameObject raceLapCounterPrefab;

    private Dictionary<ulong, RaceLapCounter> playerLapCounters = new Dictionary<ulong, RaceLapCounter>();
    private List<RaceCheckpoint> checkpointList = new List<RaceCheckpoint>();

    private List<RacePlayerResult> racePlayerResults = new List<RacePlayerResult>();

    public Dictionary<ulong, RaceLapCounter> PlayerLapCounters => playerLapCounters;
    public event System.Action onInitializedPlayers;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        GameManager.Instance.onGameStateChanged += OnGameStateChanged;

        checkpointList.AddRange(FindObjectsOfType<RaceCheckpoint>());

        checkpointList.Sort((a, b) => a.CheckpointIndex.CompareTo(b.CheckpointIndex));
    }

    private void OnGameStateChanged(GameState state)
    {
        if (!IsServer)
        {
            return;
        }

        switch (state)
        {
            case GameState.Initialising:
                break;
            case GameState.Starting:
                foreach (var player in GameManager.Instance.Players)
                {
                    player.Pokemon.GainExperienceRPC(100000);
                    GameObject spawnedObject = Instantiate(raceLapCounterPrefab, Vector3.zero, Quaternion.identity);
                    spawnedObject.GetComponent<NetworkObject>().SpawnWithOwnership(player.OwnerClientId, true);

                    RaceLapCounter lapCounter = spawnedObject.GetComponent<RaceLapCounter>();
                    lapCounter.InitializeRPC(player.NetworkObjectId);

                    lapCounter.OnLapChanged += (lap) => OnPlayerLapCompleted(lapCounter);

                    playerLapCounters.Add(player.NetworkObjectId, lapCounter);
                }

                StartCoroutine(WaitBeforeNotify());
                break;
            case GameState.Playing:
                foreach (var player in GameManager.Instance.Players)
                {
                    UpdateLocalPlayerRPC(player.NetworkObjectId, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
                }

                StartCoroutine(UpdatePlayerPositionsCoroutine());
                break;
            case GameState.Ended:
                break;
            default:
                break;
        }
    }

    private IEnumerator WaitBeforeNotify()
    {
        yield return null;
        NotifyPlayersInitializedRPC();
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyPlayersInitializedRPC()
    {
        if (!IsServer)
        {
            RaceLapCounter[] raceLapCounters = FindObjectsOfType<RaceLapCounter>();

            foreach (var lapCounter in raceLapCounters)
            {
                playerLapCounters.Add(lapCounter.AssignedPlayerID, lapCounter);
            }
        }

        onInitializedPlayers?.Invoke();
    }

    private IEnumerator UpdatePlayerPositionsCoroutine()
    {
        while (true)
        {
            UpdatePlayerPositions();
            yield return new WaitForSeconds(0.05f);
        }
    }

    private void OnPlayerLapCompleted(RaceLapCounter lapCounter)
    {
        if (!IsServer)
        {
            return;
        }

        if (lapCounter.LapCount > TOTAL_LAPS)
        {
            PlayerManager player = NetworkManager.Singleton.SpawnManager.SpawnedObjects[lapCounter.AssignedPlayerID].GetComponent<PlayerManager>();

            racePlayerResults.Add(new RacePlayerResult(player.LobbyPlayer.Id, lapCounter.CurrentPlace, GameManager.Instance.GameTime));

            CheckIfRaceEnded();
        }
    }

    private void CheckIfRaceEnded()
    {
        if (!IsServer)
        {
            return;
        }

        if (racePlayerResults.Count == playerLapCounters.Count)
        {
            EndGameRPC(GenerateRaceResults());
        }
    }

    [Rpc(SendTo.Everyone)]
    void EndGameRPC(RaceGameResults gameResults)
    {
        LobbyController.Instance.RaceGameResults = gameResults;
        AudioManager.PlaySound(DefaultAudioSounds.AnnouncerTImeUp);

        StartCoroutine(GameManager.Instance.EndGameRoutine());
    }

    private RaceGameResults GenerateRaceResults()
    {
        return new RaceGameResults(racePlayerResults.ToArray(), GameManager.Instance.GameTime);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void UpdateLocalPlayerRPC(ulong playerID, RpcParams rpcParams = default)
    {
        PlayerManager player = NetworkManager.Singleton.SpawnManager.SpawnedObjects[playerID].GetComponent<PlayerManager>();

        player.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
        player.MovesController.BattleItemStatus.AddStatus(ActionStatusType.Disabled);
        player.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
        player.ScoreStatus.AddStatus(ActionStatusType.Busy);
    }

    private void UpdatePlayerPositions()
    {
        if (!IsServer)
        {
            return;
        }

        foreach (var player in playerLapCounters)
        {
            player.Value.SetCurrentPlaceRPC(GetPlayerPlace(player.Value));
        }
    }

    private short GetPlayerPlace(RaceLapCounter lapCounter)
    {
        // Create a sorted list of players based on laps, checkpoints, and distance to next checkpoint
        List<RaceLapCounter> sortedLapCounters = new List<RaceLapCounter>(playerLapCounters.Values);

        sortedLapCounters.Sort((a, b) =>
        {
            // Compare Lap Count
            int lapComparison = b.LapCount.CompareTo(a.LapCount);
            if (lapComparison != 0) return lapComparison;

            // Compare Checkpoint Count
            int checkpointComparison = b.CheckpointCount.CompareTo(a.CheckpointCount);
            if (checkpointComparison != 0) return checkpointComparison;

            // Compare Distance to Next Checkpoint
            Transform aNextCheckpoint = GetNextCheckpoint(a);
            Transform bNextCheckpoint = GetNextCheckpoint(b);

            float aDistance = Vector3.Distance(a.transform.position, aNextCheckpoint.position);
            float bDistance = Vector3.Distance(b.transform.position, bNextCheckpoint.position);

            return aDistance.CompareTo(bDistance);
        });

        // Return the 1-based position of the given lapCounter
        return (short)(sortedLapCounters.IndexOf(lapCounter) + 1);
    }

    private Transform GetNextCheckpoint(RaceLapCounter lapCounter)
    {
        int nextCheckpointIndex = lapCounter.CheckpointCount + 1;
        if (nextCheckpointIndex >= checkpointList.Count)
        {
            nextCheckpointIndex = 0;
        }
        return checkpointList[nextCheckpointIndex].transform;
    }
}