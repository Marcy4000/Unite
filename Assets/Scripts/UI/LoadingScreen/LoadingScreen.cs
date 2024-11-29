using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using Unity.Multiplayer.Samples.Utilities;
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

        NetworkedLoadingProgressTracker[] trackers = FindObjectsOfType<NetworkedLoadingProgressTracker>();
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
        AudioManager.PlaySound(DefaultAudioSounds.Loading_Sfx);
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

        Player[] orangeTeamPlayers = LobbyController.Instance.GetTeamPlayers(Team.Orange);
        Player[] blueTeamPlayers = LobbyController.Instance.GetTeamPlayers(Team.Blue);

        foreach (var player in orangeTeamPlayers)
        {
            GameObject playerObj = Instantiate(playerPrefab, orangeTeamSpawn);
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

        foreach (var player in blueTeamPlayers)
        {
            GameObject playerObj = Instantiate(playerPrefab, blueTeamSpawn);
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

                if (sceneEvent.SceneName.Equals("CharacterSelect") || sceneEvent.SceneName.Equals("DraftSelect"))
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
