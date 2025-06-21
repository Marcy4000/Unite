using JSAM;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RaceManager : NetworkBehaviour
{
    public static RaceManager Instance { get; private set; }

    public static readonly int TOTAL_LAPS = 3;

    [SerializeField] private GameObject raceLapCounterPrefab;
    [SerializeField] private MoveAsset dashMove;
    [SerializeField] private MoveAsset emptyMove;

    private Dictionary<ulong, RaceLapCounter> playerLapCounters = new Dictionary<ulong, RaceLapCounter>();
    private List<RaceCheckpoint> checkpointList = new List<RaceCheckpoint>();

    private List<RacePlayerResult> racePlayerResults = new List<RacePlayerResult>();

    public Dictionary<ulong, RaceLapCounter> PlayerLapCounters => playerLapCounters;
    public event System.Action OnInitializedPlayers;

    public MoveAsset EmptyMove => emptyMove;

    private void Awake()
    {
        Instance = this;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        }
    }

    private void OnDisable()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }
    }

    public override void OnNetworkSpawn()
    {
        GameManager.Instance.onGameStateChanged += OnGameStateChanged;

        checkpointList.AddRange(Object.FindObjectsByType<RaceCheckpoint>(FindObjectsSortMode.None));

        checkpointList.Sort((a, b) => a.CheckpointIndex.CompareTo(b.CheckpointIndex));
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        if (playerLapCounters.ContainsKey(clientId))
        {
            playerLapCounters.Remove(clientId);

            NotifyPlayersInitializedRPC();
        }
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

                NotifyPlayersInitializedRPC();
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

    [Rpc(SendTo.Everyone)]
    private void NotifyPlayersInitializedRPC()
    {
        if (!IsServer)
        {
            StartCoroutine(WaitForLapCounters());
        }
        else
        {
            OnInitializedPlayers?.Invoke();
        }
    }

    private IEnumerator WaitForLapCounters()
    {
        while (!AreAllLapCountersInitialized())
        {
            yield return null; // Wait for the next frame
        }

        OnInitializedPlayers?.Invoke();
    }

    private bool AreAllLapCountersInitialized()
    {
        RaceLapCounter[] raceLapCounters = Object.FindObjectsByType<RaceLapCounter>(FindObjectsSortMode.None);

        if (raceLapCounters.Length != GameManager.Instance.Players.Count)
        {
            return false;
        }

        foreach (var lapCounter in raceLapCounters)
        {
            if (lapCounter.AssignedPlayerID == 0)
            {
                return false;
            }
        }

        foreach (var lapCounter in raceLapCounters)
        {
            if (!playerLapCounters.ContainsKey(lapCounter.AssignedPlayerID))
            {
                playerLapCounters.Add(lapCounter.AssignedPlayerID, lapCounter);
            }
        }

        return true;
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
            lapCounter.SetRaceFinishedRPC(true);

            if (racePlayerResults.Count == playerLapCounters.Count)
            {
                EndGameRPC(GenerateRaceResults());
            }
            else
            {
                PlayerRaceCompletedRPC(lapCounter.AssignedPlayerID, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
            }
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void PlayerRaceCompletedRPC(ulong playerID, RpcParams rpcParams = default)
    {
        PlayerManager player = NetworkManager.Singleton.SpawnManager.SpawnedObjects[playerID].GetComponent<PlayerManager>();

        player.MovesController.LockEveryAction();
        player.PlayerMovement.CanMove = false;

        CameraController.Instance.ForcePan(true);

        player.UpdatePosAndRotRPC(new Vector3(-100f, -50f, 0f), Quaternion.identity);
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

        StartCoroutine(UpdateLocalPlayerDelayed(player));
    }

    private IEnumerator UpdateLocalPlayerDelayed(PlayerManager player)
    {
        yield return null;

        player.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
        player.MovesController.BattleItemStatus.AddStatus(ActionStatusType.Disabled);
        player.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
        player.ScoreStatus.AddStatus(ActionStatusType.Busy);

        player.MovesController.LearnMove(dashMove);

        player.PlayerMovement.CanMove = false;
    }

    private void UpdatePlayerPositions()
    {
        if (!IsServer)
        {
            return;
        }

        foreach (var player in playerLapCounters)
        {
            if (player.Value == null)
            {
                continue;
            }

            player.Value.SetCurrentPlaceRPC(GetPlayerPlace(player.Value));
        }
    }

    private short GetPlayerPlace(RaceLapCounter lapCounter)
    {
        // Create a sorted list of players based on laps, checkpoints, and distance to next checkpoint
        List<RaceLapCounter> sortedLapCounters = new List<RaceLapCounter>(playerLapCounters.Values);

        sortedLapCounters.RemoveAll(lap => lap == null); // Remove null references

        sortedLapCounters.Sort((a, b) =>
        {
            // If both players have finished, compare their confirmed positions
            if (a.RaceFinished && b.RaceFinished)
            {
                return a.CurrentPlace.CompareTo(b.CurrentPlace);
            }

            // If one has finished, it should be ranked higher
            if (a.RaceFinished) return -1;
            if (b.RaceFinished) return 1;

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

    public RaceLapCounter GetNextPlayer(ulong playerID)
    {
        if (!playerLapCounters.ContainsKey(playerID))
        {
            return null; // Return null if the player doesn't exist
        }

        short currentPlayerPlace = playerLapCounters[playerID].CurrentPlace;

        // Filter players who haven't finished the race
        var eligiblePlayers = new List<RaceLapCounter>();
        foreach (var lapCounter in playerLapCounters.Values)
        {
            if (!lapCounter.RaceFinished && lapCounter.AssignedPlayerID != playerID)
            {
                eligiblePlayers.Add(lapCounter);
            }
        }

        if (eligiblePlayers.Count == 0)
        {
            return null; // No eligible players left
        }

        // Sort eligible players by their place in ascending order
        eligiblePlayers.Sort((a, b) => a.CurrentPlace.CompareTo(b.CurrentPlace));

        // Find the next player in the standings
        foreach (var lapCounter in eligiblePlayers)
        {
            if (lapCounter.CurrentPlace > currentPlayerPlace)
            {
                return lapCounter; // Return the first player ahead in the standings
            }
        }

        // Loop back to the first eligible player if no one is ahead
        return eligiblePlayers[0];
    }

}