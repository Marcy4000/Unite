using System.Collections;
using System.Collections.Generic;
using Player = Unite.Multiplayer.GameSessionPlayer;
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
    private Coroutine missingTrackersWarning;
    private Coroutine selectionTransitionRoutine;
    private Coroutine selectionOpeningRoutine;
    private LoadingManagerTracker selectionCoordinator;
    private int selectionGeneration;
    private string selectionScene;
    private bool selectionTransitionActive;
    private bool selectionScreenReady;

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
        UpdateProgressSubscriptions();
    }

    private void UpdateProgressSubscriptions()
    {
        // Only update if the loading screen is active and we have players
        if (!holder.activeSelf || playerList.Count == 0)
        {
            return;
        }

        foreach (var playerIcon in playerList)
        {
            if (playerIcon.CurrentPlayer == null)
            {
                continue;
            }

            NetworkedLoadingProgressTracker progressTracker = null;
            if (playerIcon.CurrentPlayer.Data.TryGetValue("OwnerID", out var ownerId) &&
                ulong.TryParse(ownerId.Value, out var clientId))
            {
                loadingProgressManager.ProgressTrackers.TryGetValue(clientId, out progressTracker);
            }
            playerIcon.BindProgressTracker(progressTracker != null && progressTracker.IsSpawned ? progressTracker : null);
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
        UpdateProgressSubscriptions();
        missingTrackersWarning = StartCoroutine(WarnAboutMissingTrackers());
    }

    public void HideMatchLoadingScreen()
    {
        holder.SetActive(false);
        if (missingTrackersWarning != null)
        {
            StopCoroutine(missingTrackersWarning);
            missingTrackersWarning = null;
        }

        foreach (var playerIcon in playerList)
        {
            playerIcon.UnbindProgressTracker();
        }

        playerList.Clear();
    }

    private IEnumerator WarnAboutMissingTrackers()
    {
        // Allow initial network synchronization time without warning on normal late arrivals.
        yield return new WaitForSecondsRealtime(5f);
        foreach (var playerIcon in playerList)
        {
            if (!playerIcon.HasProgressTracker && playerIcon.CurrentPlayer != null && !playerIcon.CurrentPlayer.IsBot)
            {
                var player = playerIcon.CurrentPlayer;
                string ownerId = player.Data.TryGetValue("OwnerID", out var owner) ? owner.Value : "missing";
                Debug.LogWarning($"[LoadingProgress] No spawned progress tracker for player {player.Id} (OwnerID: {ownerId}); loading bar cannot update.", this);
            }
        }
        missingTrackersWarning = null;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        CancelSelectionTransition(selectionGeneration);
        if (loadingProgressManager != null)
        {
            loadingProgressManager.onTrackersUpdated -= OnTrackersUpdated;
        }
        foreach (var playerIcon in playerList)
        {
            if (playerIcon != null) playerIcon.UnbindProgressTracker();
        }
        Instance = null;
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
        gameBeginScreenUI.InitializeUI(bluePlayers, orangePlayers);
        gameBeginScreenUI.FadeIn();
    }

    public void HideGameBeginScreen()
    {
        if (selectionTransitionActive)
        {
            selectionScreenReady = true;
            return;
        }
        gameBeginScreenUI.FadeOut();
    }

    public bool BeginMatchTransition(string sceneName) => loadingProgressManager != null &&
        loadingProgressManager.BeginSelectionTransition(sceneName);

    public void BeginSelectionTransition(int generation, string sceneName, double closeAt, LoadingManagerTracker coordinator)
    {
        CancelSelectionTransition(selectionGeneration);
        selectionGeneration = generation;
        selectionScene = sceneName;
        selectionCoordinator = coordinator;
        selectionTransitionActive = true;
        selectionScreenReady = false;
        selectionTransitionRoutine = StartCoroutine(CloseSelectionTransition(generation, closeAt));
    }

    private IEnumerator CloseSelectionTransition(int generation, double closeAt)
    {
        while (selectionCoordinator != null && selectionCoordinator.NetworkManager.ServerTime.Time < closeAt)
            yield return null;
        if (!selectionTransitionActive || selectionGeneration != generation || selectionCoordinator == null) yield break;
        ShowGameBeginScreen();
        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_Matching);
        while (!gameBeginScreenUI.IsClosed) yield return null;
        if (selectionCoordinator == null || !selectionCoordinator.IsSpawned) yield break;
        selectionCoordinator.SelectionClosedRpc(generation);
        while (selectionTransitionActive && selectionGeneration == generation)
        {
            if (selectionScreenReady && SceneManager.GetActiveScene().name == selectionScene)
            {
                if (selectionCoordinator != null && selectionCoordinator.IsSpawned)
                    selectionCoordinator.SelectionScreenReadyRpc(generation);
                yield break;
            }
            yield return null;
        }
    }

    public void OpenSelectionTransition(int generation, double openAt)
    {
        if (!selectionTransitionActive || selectionGeneration != generation || selectionOpeningRoutine != null) return;
        selectionOpeningRoutine = StartCoroutine(OpenSelectionTransitionRoutine(generation, openAt));
    }

    private IEnumerator OpenSelectionTransitionRoutine(int generation, double openAt)
    {
        while (selectionCoordinator != null && selectionCoordinator.NetworkManager.ServerTime.Time < openAt)
            yield return null;
        if (!selectionTransitionActive || selectionGeneration != generation) yield break;
        selectionTransitionActive = false;
        gameBeginScreenUI.FadeOut();
        selectionOpeningRoutine = null;
    }

    public void CancelSelectionTransition(int generation)
    {
        if (generation != selectionGeneration) return;
        if (selectionTransitionRoutine != null) StopCoroutine(selectionTransitionRoutine);
        if (selectionOpeningRoutine != null) StopCoroutine(selectionOpeningRoutine);
        selectionTransitionRoutine = null;
        selectionOpeningRoutine = null;
        selectionTransitionActive = false;
        selectionScreenReady = false;
        selectionCoordinator = null;
        selectionScene = null;
        selectionGeneration = 0;
        if (gameBeginScreenUI != null) gameBeginScreenUI.HideImmediately();
    }

    private void InitializeLoadingScreen()
    {
        HideMatchLoadingScreen();
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

                playerList.Add(loadingScreenPlayer);
            }
        }
    }

    public void OnSceneEvent(SceneEvent sceneEvent)
    {
        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.Load:
                if (NetworkManager.Singleton.IsClient && sceneEvent.ClientId == NetworkManager.Singleton.LocalClientId)
                {
                    loadingProgressManager.LocalLoadOperation = sceneEvent.AsyncOperation;
                }

                if (LoadingManagerTracker.IsSelectionScene(sceneEvent.SceneName) && sceneEvent.SceneName != selectionScene)
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
