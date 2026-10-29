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
    private bool raceEnded;
    private GameManager subscribedGame;
    private NetworkManager subscribedNetwork;

    public Dictionary<ulong, RaceLapCounter> PlayerLapCounters => playerLapCounters;
    public event System.Action OnInitializedPlayers;

    public MoveAsset EmptyMove => emptyMove;

    private void Awake()
    {
        Instance = this;

    }

    public override void OnNetworkDespawn()
    {
        if (subscribedNetwork != null) subscribedNetwork.OnClientDisconnectCallback -= OnClientDisconnect;
        if (subscribedGame != null) subscribedGame.onGameStateChanged -= OnGameStateChanged;
        subscribedNetwork = null; subscribedGame = null;
        StopAllCoroutines();
        playerLapCounters.Clear();
        base.OnNetworkDespawn();
    }

    public override void OnNetworkSpawn()
    {
        subscribedGame = GameManager.Instance;
        subscribedGame.onGameStateChanged += OnGameStateChanged;
        if (IsServer)
        {
            subscribedNetwork = NetworkManager;
            subscribedNetwork.OnClientDisconnectCallback += OnClientDisconnect;
        }

        checkpointList.AddRange(Object.FindObjectsByType<RaceCheckpoint>(FindObjectsSortMode.None));

        checkpointList.Sort((a, b) => a.CheckpointIndex.CompareTo(b.CheckpointIndex));
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        var departed = new List<ulong>();
        foreach (var entry in playerLapCounters)
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(entry.Key, out var avatar) || avatar.OwnerClientId == clientId) departed.Add(entry.Key);
        foreach (ulong id in departed)
        {
            var counter = playerLapCounters[id];
            if (counter != null && counter.IsSpawned) counter.NetworkObject.Despawn();
            playerLapCounters.Remove(id);
        }
        if (departed.Count > 0)
        {
            NotifyPlayersInitializedRPC();
            TryFinishRace();
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

        if (!raceEnded && !lapCounter.RaceFinished && lapCounter.LapCount > TOTAL_LAPS)
        {
            PlayerManager player = NetworkManager.Singleton.SpawnManager.SpawnedObjects[lapCounter.AssignedPlayerID].GetComponent<PlayerManager>();

            short finishedPlace = (short)(racePlayerResults.Count + 1);
            racePlayerResults.Add(new RacePlayerResult(player.LobbyPlayer.Id, finishedPlace, GameManager.Instance.GameTime));
            lapCounter.SetCurrentPlaceRPC(finishedPlace);
            lapCounter.SetRaceFinishedRPC(true);

            PlayerRaceCompletedRPC(lapCounter.AssignedPlayerID, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
            TryFinishRace();
        }
    }

    private bool AreRemainingRacersFinished()
    {
        if (playerLapCounters.Count == 0) return false;
        foreach (var counter in playerLapCounters.Values)
            if (counter == null || !counter.RaceFinished) return false;
        return true;
    }

    private void TryFinishRace()
    {
        // Historical finish results can include disconnected players. Comparing
        // their count with the live roster can finish early or never finish.
        if (raceEnded || !AreRemainingRacersFinished()) return;
        raceEnded = true;
        EndGameRPC(GenerateRaceResults());
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void PlayerRaceCompletedRPC(ulong playerID, RpcParams rpcParams = default)
    {
        PlayerManager player = NetworkManager.Singleton.SpawnManager.SpawnedObjects[playerID].GetComponent<PlayerManager>();

        player.MovesController.LockEveryAction();
        player.PlayerMovement.CanMove = false;

        if (player.PassiveController.Passive is PsyduckRacePassive racer) { racer.CanMove = false; racer.CurrentVelocity = Vector3.zero; }
        player.SetBotMovement(Vector2.zero);
        if (player.IsLocalHuman) CameraController.Instance.ForcePan(true);

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
        if (player.PassiveController.Passive is PsyduckRacePassive racer) racer.CanMove = true;
    }

    private void UpdatePlayerPositions()
    {
        if (!IsServer)
        {
            return;
        }

        foreach (var player in playerLapCounters)
        {
            if (player.Value == null || player.Value.RaceFinished)
            {
                continue;
            }

            player.Value.SetCurrentPlaceRPC(GetPlayerPlace(player.Value));
        }
    }

    private short GetPlayerPlace(RaceLapCounter lapCounter)
    {
        if (lapCounter.RaceFinished) return lapCounter.CurrentPlace;
        // Create a sorted list of players based on laps, checkpoints, and distance to next checkpoint
        List<RaceLapCounter> sortedLapCounters = new List<RaceLapCounter>(playerLapCounters.Values);

        sortedLapCounters.RemoveAll(lap => lap == null || lap.RaceFinished);

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
        return (short)(racePlayerResults.Count + sortedLapCounters.IndexOf(lapCounter) + 1);
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
        if (!playerLapCounters.TryGetValue(playerID, out var currentPlayer) || currentPlayer == null)
        {
            return null; // Return null if the player doesn't exist
        }

        short currentPlayerPlace = currentPlayer.CurrentPlace;

        // Filter players who haven't finished the race
        var eligiblePlayers = new List<RaceLapCounter>();
        foreach (var lapCounter in playerLapCounters.Values)
        {
            if (lapCounter != null && !lapCounter.RaceFinished && lapCounter.AssignedPlayerID != playerID)
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
        for (int index = eligiblePlayers.Count - 1; index >= 0; index--)
        {
            var lapCounter = eligiblePlayers[index];
            if (lapCounter.CurrentPlace < currentPlayerPlace)
            {
                return lapCounter; // Return the first player ahead in the standings
            }
        }

        // Loop back to the first eligible player if no one is ahead
        return eligiblePlayers[0];
    }

}
