using JSAM;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct UniteRoyalePlayerStats : INetworkSerializable, System.IEquatable<UniteRoyalePlayerStats>
{
    public FixedString32Bytes playerLobbyID;
    public ulong playerID;
    public int kills;
    public int deaths;

    public UniteRoyalePlayerStats(FixedString32Bytes playerLobbyID, ulong playerID)
    {
        this.playerLobbyID = playerLobbyID;
        this.playerID = playerID;
        kills = 0;
        deaths = 0;
    }

    public bool Equals(UniteRoyalePlayerStats other)
    {
        return kills == other.kills && deaths == other.deaths && playerLobbyID.Equals(other.playerLobbyID) && playerID == other.playerID;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerLobbyID);
        serializer.SerializeValue(ref playerID);
        serializer.SerializeValue(ref kills);
        serializer.SerializeValue(ref deaths);
    }
}

public class UniteRoyaleManager : NetworkBehaviour
{
    public static UniteRoyaleManager Instance { get; private set; }

    private PlayerNetworkManager[] playersInGame;

    private NetworkList<UniteRoyalePlayerStats> playerStats;

    public event System.Action OnInitializedPlayers;
    public event System.Action OnPlayerStatsChanged;
    public List<UniteRoyalePlayerStats> PlayerStats { get; private set; }
    public PlayerNetworkManager[] PlayersInGame => playersInGame;

    public int HighestKills
    {
        get
        {
            int highest = 0;
            foreach (var stats in playerStats)
            {
                if (stats.kills > highest)
                {
                    highest = stats.kills;
                }
            }
            return highest;
        }
    }

    private void Awake()
    {
        Instance = this;
        playerStats = new NetworkList<UniteRoyalePlayerStats>();
    }

    public override void OnNetworkSpawn()
    {
        GameManager.Instance.onGameStateChanged += OnGameStateChanged;
        playersInGame = FindObjectsByType<PlayerNetworkManager>(FindObjectsSortMode.None);
        if (IsServer)
        {
            foreach (var player in playersInGame)
            {
                playerStats.Add(new UniteRoyalePlayerStats(player.LocalPlayer.Id, player.NetworkObjectId));
                player.OnPlayerStatsChanged += OnPlayerStatsChangedHandler;
            }
        }

        playerStats.OnListChanged += (NetworkListEvent<UniteRoyalePlayerStats> changeEvent) =>
        {
            PlayerStats = GetSortedPlayerStats();
            OnPlayerStatsChanged?.Invoke();
        };
    }

    private void OnPlayerStatsChangedHandler(PlayerStats newStats)
    {
        if (!IsServer) return;

        for (int i = 0; i < playerStats.Count; i++)
        {
            if (playerStats[i].playerLobbyID == newStats.playerId)
            {
                UniteRoyalePlayerStats updatedStats = playerStats[i];
                updatedStats.kills = newStats.kills;
                updatedStats.deaths = newStats.deaths;
                playerStats[i] = updatedStats;
                break;
            }
        }
    }

    public override void OnDestroy()
    {
        foreach (var player in playersInGame)
        {
            if (player != null)
                player.OnPlayerStatsChanged -= OnPlayerStatsChangedHandler;
        }

        base.OnDestroy();
    }

    private void OnGameStateChanged(GameState state)
    {
        if (!IsServer) return;

        switch (state)
        {
            case GameState.Starting:
                foreach (var player in GameManager.Instance.Players)
                {
                    player.Pokemon.GainExperienceRPC(100000);
                }
                NotifyPlayersInitializedRPC();
                break;
            case GameState.Ended:
                List<PlayerStats> finalStats = new List<PlayerStats>();

                foreach (var playerNetworkManager in playersInGame)
                {
                    finalStats.Add(playerNetworkManager.PlayerStats);
                }

                UniteRoyaleGameResults gameResults = GenerateGameResults();

                EndGameRPC(gameResults);
                break;
            default:
                break;
        }
    }

    [Rpc(SendTo.Everyone)]
    void EndGameRPC(UniteRoyaleGameResults gameResults)
    {
        LobbyController.Instance.UniteRoyaleGameResults = gameResults;
        AudioManager.PlaySound(DefaultAudioSounds.AnnouncerTImeUp);

        StartCoroutine(GameManager.Instance.EndGameRoutine());
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyPlayersInitializedRPC()
    {
        OnInitializedPlayers?.Invoke();
    }

    private UniteRoyaleGameResults GenerateGameResults()
    {
        List<UniteRoyalePlayerResult> finalStats = new List<UniteRoyalePlayerResult>();
        List<UniteRoyalePlayerStats> sortedStats = GetSortedPlayerStats();

        byte position = 1;
        int previousKills = -1;

        foreach (var stats in sortedStats)
        {
            if (stats.kills != previousKills)
            {
                if (previousKills != -1)
                {
                    position++;
                }
                previousKills = stats.kills;
            }

            PlayerStats playerStats = playersInGame.Where(p => p.NetworkObjectId == stats.playerID).First().PlayerStats;
            finalStats.Add(new UniteRoyalePlayerResult(playerStats, position));
        }

        return new UniteRoyaleGameResults(finalStats.ToArray());
    }

    public List<UniteRoyalePlayerStats> GetSortedPlayerStats()
    {
        List<UniteRoyalePlayerStats> sortedStats = new List<UniteRoyalePlayerStats>();
        foreach (var stats in playerStats)
        {
            sortedStats.Add(stats);
        }
        sortedStats.Sort((a, b) => b.kills.CompareTo(a.kills));
        return sortedStats;
    }
}
