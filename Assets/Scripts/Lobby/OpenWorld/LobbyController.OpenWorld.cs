using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class LobbyController
{
    public OpenWorldLobbyController OpenWorldLobby { get; private set; }
    private bool changingHome;

    private void InitializeOpenWorldLobby()
    {
        OpenWorldLobby = gameObject.AddComponent<OpenWorldLobbyController>();
        SettingsManager.OpenWorldLobbyChanged += OnOpenWorldLobbySettingChanged;
    }

    private async void OnOpenWorldLobbySettingChanged(bool enabled)
    {
        if (!enabled) OpenWorldLobby.CancelPendingConnection();
        if (session != null || changingHome || lobbyOperationInProgress || handlingSessionLoss ||
            isSearching || !IsHomeScene(SceneManager.GetActiveScene().name)) return;
        try { await ChangeHomeAsync(false); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    public static bool IsHomeScene(string sceneName) => sceneName == "LobbyScene" || sceneName == "ChineseLobby";

    public async void OpenMatchLobbyMenu()
    {
        if (changingHome || lobbyOperationInProgress || handlingSessionLoss) return;
        try { await ChangeHomeAsync(true); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    public async void EnterOpenWorldLobby()
    {
        if (!SettingsManager.OpenWorldLobbyEnabled || session != null || changingHome ||
            lobbyOperationInProgress || handlingSessionLoss) return;
        try { await ChangeHomeAsync(false); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private async Task ChangeHomeAsync(bool staticMenu)
    {
        changingHome = true;
        LoadingScreen.Instance?.ShowGenericLoadingScreen();
        var gateTaken = false;
        try
        {
            await lifecycleGate.WaitAsync(lifetimeCancellation.Token);
            gateTaken = true;
            await LoadHomeAsync(staticMenu);
        }
        finally
        {
            if (gateTaken) lifecycleGate.Release();
            changingHome = false;
            LoadingScreen.Instance?.HideGenericLoadingScreen();
        }
    }

    private async Task LoadHomeAsync(bool staticMenu = false)
    {
        await authenticationTask;
        await OpenWorldLobby.LeaveAsync();
        var useOpenWorld = !staticMenu && session == null && SettingsManager.OpenWorldLobbyEnabled;
        var targetScene = useOpenWorld ? "ChineseLobby" : "LobbyScene";
        if (SceneManager.GetActiveScene().name != targetScene)
            await SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        if (!staticMenu && session == null && useOpenWorld != SettingsManager.OpenWorldLobbyEnabled)
        {
            await LoadHomeAsync();
            return;
        }
        if (!useOpenWorld)
        {
            ShowStaticHomeMenu();
            return;
        }
        try
        {
            var view = FindFirstObjectByType<OpenWorldLobbyView>();
            if (view == null) throw new InvalidOperationException("ChineseLobby is missing its open world lobby view.");
            await OpenWorldLobby.EnterAsync(localPlayer, view);
            if (!SettingsManager.OpenWorldLobbyEnabled) await LoadHomeAsync(true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Open world lobby unavailable: {exception.Message}");
            await OpenWorldLobby.LeaveAsync();
            await SceneManager.LoadSceneAsync("LobbyScene", LoadSceneMode.Single);
            ShowStaticHomeMenu();
            if (SettingsManager.OpenWorldLobbyEnabled)
                LastOperationError = "The open world lobby could not connect. You can retry from the main menu or use a match lobby.";
        }
    }

    private void ShowStaticHomeMenu()
    {
        lobbyUI = FindFirstObjectByType<MainMenuUI>();
        if (lobbyUI == null)
            throw new InvalidOperationException("LobbyScene is missing its authored MainMenuUI controller. Restore its menu and navigation references in the scene.");
        lobbyUI.ShowMainMenuUI();
    }
}
