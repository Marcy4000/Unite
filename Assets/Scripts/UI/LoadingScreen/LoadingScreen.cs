using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using JSAM;

public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform blueTeamSpawn, orangeTeamSpawn;
    [SerializeField] private GameObject holder;
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private LoadingManagerTracker loadingProgressManager;
    [SerializeField] private GameBeginScreenUI gameBeginScreenUI;

    private List<LoadingScreenPlayer> playerList = new List<LoadingScreenPlayer>();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        HideGenericLoadingScreen();
        HideMatchLoadingScreen();
        loadingProgressManager.onTrackersUpdated += OnTrackersUpdated;
    }

    private void OnTrackersUpdated()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        NetworkedLoadingProgressTracker[] trackers = FindObjectsByType<NetworkedLoadingProgressTracker>(FindObjectsSortMode.None);
        List<ulong> playerIds = new List<ulong>();
        foreach (var tracker in trackers)
        {
            if (playerIds.Contains(tracker.OwnerClientId))
            {
                tracker.NetworkObject.Despawn(true);
                continue;
            }
            playerIds.Add(tracker.OwnerClientId);
        }
    }

    public void ShowMatchLoadingScreen()
    {
        InitializeLoadingScreen();
        if (gameBeginScreenUI.gameObject.activeSelf)
        {
            HideGameBeginScreen();
        }
        holder.SetActive(true);
    }

    public void HideMatchLoadingScreen()
    {
        holder.SetActive(false);

        foreach (var playerIcon in playerList)
        {
            ulong clientId = ulong.Parse(playerIcon.CurrentPlayer.Data["OwnerID"].Value);
            loadingProgressManager.ProgressTrackers.TryGetValue(clientId, out var progressTracker);
            if (progressTracker != null)
            {
                progressTracker.Progress.OnValueChanged -= playerIcon.UpdateProgressBar;
            }
        }

        playerList.Clear();
    }

    public void ShowGenericLoadingScreen()
    {
        try
        {
            AudioManager.PlaySound(DefaultAudioSounds.Loading_Sfx);
        }
        catch (System.Exception)
        {
        }
        loadingScreen.SetActive(true);
    }

    public void HideGenericLoadingScreen()
    {
        loadingScreen.SetActive(false);
    }

    public void ShowGameBeginScreen()
    {
        int bluePlayers = LobbyController.Instance.GetTeamPlayers(Team.Blue).Length;
        int orangePlayers = LobbyController.Instance.GetTeamPlayers(Team.Orange).Length;
        gameBeginScreenUI.gameObject.SetActive(true);
        gameBeginScreenUI.InitializeUI(bluePlayers, orangePlayers);
        gameBeginScreenUI.FadeIn();
    }

    public void HideGameBeginScreen()
    {
        gameBeginScreenUI.FadeOut();
    }

    private void InitializeLoadingScreen()
    {
        foreach (Transform child in blueTeamSpawn)
        {
            Destroy(child.gameObject);
        }
        foreach (Transform child in orangeTeamSpawn)
        {
            Destroy(child.gameObject);
        }

        playerList.Clear();

        var map = CharactersList.Instance.GetCurrentLobbyMap();
        var teams = map.availableTeams;
        var allPlayers = LobbyController.Instance.Lobby.Players;
        Team localTeam = LobbyController.Instance.GetLocalPlayerTeam();

        // For 2 teams: local team on top (blueTeamSpawn), other on bottom (orangeTeamSpawn)
        // For 3+ teams: alternate spawn top/bottom (blueTeamSpawn/orangeTeamSpawn)
        if (teams.Count == 2)
        {
            foreach (var player in allPlayers)
            {
                Team t = TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value);
                bool isLocalTeam = t == localTeam;
                Transform parent = isLocalTeam ? blueTeamSpawn : orangeTeamSpawn;

                GameObject playerObj = Instantiate(playerPrefab, parent);
                var loadingScreenPlayer = playerObj.GetComponent<LoadingScreenPlayer>();
                loadingScreenPlayer.SetPlayerData(player);

                ulong clientId = ulong.Parse(player.Data["OwnerID"].Value);
                loadingProgressManager.ProgressTrackers.TryGetValue(clientId, out var progressTracker);
                if (progressTracker != null)
                {
                    progressTracker.Progress.OnValueChanged += loadingScreenPlayer.UpdateProgressBar;
                }

                playerList.Add(loadingScreenPlayer);
            }
        }
        else
        {
            // Sort all players by team, then by position
            List<Player> sortedPlayers = new List<Player>(allPlayers);
            sortedPlayers.Sort((a, b) =>
            {
                Team ta = TeamMember.GetTeamFromString(a.Data["PlayerTeam"].Value);
                Team tb = TeamMember.GetTeamFromString(b.Data["PlayerTeam"].Value);
                int cmp = ta.CompareTo(tb);
                if (cmp != 0) return cmp;
                short pa = NumberEncoder.FromBase64<short>(a.Data["PlayerPos"].Value);
                short pb = NumberEncoder.FromBase64<short>(b.Data["PlayerPos"].Value);
                return pa.CompareTo(pb);
            });

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var player = sortedPlayers[i];
                Transform parent = (i % 2 == 0) ? blueTeamSpawn : orangeTeamSpawn;

                GameObject playerObj = Instantiate(playerPrefab, parent);
                var loadingScreenPlayer = playerObj.GetComponent<LoadingScreenPlayer>();
                loadingScreenPlayer.SetPlayerData(player);

                ulong clientId = ulong.Parse(player.Data["OwnerID"].Value);
                loadingProgressManager.ProgressTrackers.TryGetValue(clientId, out var progressTracker);
                if (progressTracker != null)
                {
                    progressTracker.Progress.OnValueChanged += loadingScreenPlayer.UpdateProgressBar;
                }

                playerList.Add(loadingScreenPlayer);
            }
        }
    }

    public void OnSceneEvent(SceneEvent sceneEvent)
    {
        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.Load:
                if (NetworkManager.Singleton.IsClient)
                {
                    if (sceneEvent.LoadSceneMode == LoadSceneMode.Single)
                    {
                        loadingProgressManager.LocalLoadOperation = sceneEvent.AsyncOperation;
                    }
                    else
                    {
                        loadingProgressManager.LocalLoadOperation = sceneEvent.AsyncOperation;
                    }
                }

                if (sceneEvent.SceneName.Equals("CharacterSelect") || sceneEvent.SceneName.Equals("DraftSelect") || sceneEvent.SceneName.Equals("RacingReadyScreen"))
                {
                    ShowGameBeginScreen();
                    AudioManager.PlaySound(DefaultAudioSounds.Play_UI_Matching);
                }
                break;
        }
    }

    public void SetLoadingManagerOperation(AsyncOperationHandle progress)
    {
        if (NetworkManager.Singleton.IsClient)
        {
            loadingProgressManager.LocalAddressableOperation = progress;
        }
    }
}
