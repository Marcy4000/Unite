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
        if (session != null || changingHome ||
            lobbyOperationInProgress || handlingSessionLoss) return;
        try { await ChangeHomeAsync(false); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private async Task ChangeHomeAsync(bool? staticMenu = null)
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

    private static bool ShouldUseOpenWorldLobby(bool? staticMenu, bool hasSession)
    {
        return !(staticMenu ?? !SettingsManager.AutoJoinOpenWorldLobby) && !hasSession;
    }

    private static bool ShouldReloadHomeScene(string activeScene, string targetScene, int loadedSceneCount)
    {
        return activeScene != targetScene || loadedSceneCount != 1;
    }

    private async Task LoadHomeAsync(bool? staticMenu = null)
    {
        await authenticationTask;
        await OpenWorldLobby.LeaveAsync();
        var useOpenWorld = ShouldUseOpenWorldLobby(staticMenu, session != null);
        var targetScene = useOpenWorld ? "ChineseLobby" : "LobbyScene";
        if (ShouldReloadHomeScene(SceneManager.GetActiveScene().name, targetScene, SceneManager.sceneCount))
            await SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
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
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Open world lobby unavailable: {exception.Message}");
            await OpenWorldLobby.LeaveAsync();
            await SceneManager.LoadSceneAsync("LobbyScene", LoadSceneMode.Single);
            ShowStaticHomeMenu();
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
