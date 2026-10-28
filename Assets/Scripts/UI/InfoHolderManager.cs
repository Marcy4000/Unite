using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InfoHolderManager : MonoBehaviour
{
    [SerializeField] private GameObject infoPrefab;

    [SerializeField] private Transform infoHolder;
    [SerializeField] private bool enemyTeam;

    private GameManager gameManager;
    private LobbyController lobby;
    private readonly HashSet<PlayerManager> watchedPlayers = new();
    private readonly Dictionary<PlayerManager, GameObject> rows = new();

    private void OnEnable()
    {
        GameManager.OnInstanceAvailable += Bind;
        Bind(GameManager.Instance);
    }

    private void Bind(GameManager manager)
    {
        if (manager == null || gameManager == manager) return;
        Unbind();
        gameManager = manager;
        gameManager.OnPlayerAdded += WatchPlayer;
        gameManager.OnPlayerRemoved += RemovePlayer;
        lobby = LobbyController.Instance;
        if (lobby != null) lobby.onPlayerDataChanged += OnPlayerDataChanged;
        foreach (var player in gameManager.Players) WatchPlayer(player);
    }

    private void WatchPlayer(PlayerManager player)
    {
        if (player == null || !watchedPlayers.Add(player)) return;
        player.Pokemon.OnPokemonInitialized += ReconcilePlayers;
        player.Pokemon.OnTeamChange += OnTeamChanged;
        ReconcilePlayers();
    }

    private void OnTeamChanged(TeamMember team) => ReconcilePlayers();
    private void OnPlayerDataChanged(Unite.Multiplayer.GameSessionPlayer player) => ReconcilePlayers();

    private void ReconcilePlayers()
    {
        Team localTeam = Team.Neutral;
        if (lobby != null && lobby.Player?.Data != null && lobby.Player.Data.TryGetValue("PlayerTeam", out var teamData))
            localTeam = TeamMember.GetTeamFromString(teamData.Value);
        foreach (var player in watchedPlayers.ToArray())
        {
            bool eligible = player != null && localTeam != Team.Neutral && player.CurrentTeam.Team != Team.Neutral &&
                (enemyTeam ? !player.CurrentTeam.IsOnSameTeam(localTeam) : !player.IsLocalHuman && player.CurrentTeam.IsOnSameTeam(localTeam));
            if (!eligible)
            {
                RemoveRow(player);
                continue;
            }
            if (enemyTeam) CreateEnemyInfo(player);
            else CreateAllyInfo(player);
        }
    }

    public void CreateAllyInfo(PlayerManager player)
    {
        if (player == null || rows.ContainsKey(player)) return;
        GameObject allyInfo = Instantiate(infoPrefab, infoHolder);
        rows.Add(player, allyInfo);
        allyInfo.GetComponent<AllyInfoUI>().InitializeUI(player);
    }

    public void CreateEnemyInfo(PlayerManager player)
    {
        if (player == null || rows.ContainsKey(player)) return;
        GameObject allyInfo = Instantiate(infoPrefab, infoHolder);
        rows.Add(player, allyInfo);
        allyInfo.GetComponent<EnemyDeathInfoUI>().InitializeUI(player);
    }

    private void RemoveRow(PlayerManager player)
    {
        if (!rows.TryGetValue(player, out var row)) return;
        rows.Remove(player);
        if (row != null) { row.SetActive(false); Destroy(row); }
    }

    private void RemovePlayer(PlayerManager player)
    {
        if (!watchedPlayers.Remove(player)) return;
        if (player != null && player.Pokemon != null)
        {
            player.Pokemon.OnPokemonInitialized -= ReconcilePlayers;
            player.Pokemon.OnTeamChange -= OnTeamChanged;
        }
        RemoveRow(player);
    }

    private void Unbind()
    {
        if (gameManager != null)
        {
            gameManager.OnPlayerAdded -= WatchPlayer;
            gameManager.OnPlayerRemoved -= RemovePlayer;
        }
        if (lobby != null) lobby.onPlayerDataChanged -= OnPlayerDataChanged;
        foreach (var player in watchedPlayers.ToArray()) RemovePlayer(player);
        gameManager = null;
        lobby = null;
    }

    private void OnDisable()
    {
        GameManager.OnInstanceAvailable -= Bind;
        Unbind();
    }
}
