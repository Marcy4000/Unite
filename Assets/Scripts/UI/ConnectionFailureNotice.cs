using Unite.Multiplayer;
using UnityEngine;

/// <summary>
/// Asset-independent fallback notice, including in builds with no error-dialog
/// prefab. A styled UI can instead subscribe to LobbyController.onConnectionFailure.
/// </summary>
public sealed class ConnectionFailureNotice : MonoBehaviour
{
    private GUIStyle messageStyle;

    private void OnGUI()
    {
        var controller = LobbyController.Instance;
        if (controller == null || controller.IsHandlingConnectionFailure ||
            controller.LastConnectionFailure == SessionFailureReason.None) return;

        messageStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
        var width = Mathf.Min(520f, Screen.width - 24f);
        var area = new Rect((Screen.width - width) / 2f, Mathf.Max(12f, (Screen.height - 210f) / 2f), width, 210f);
        GUILayout.BeginArea(area, "Connection interrupted", GUI.skin.window);
        GUILayout.Space(16f);
        GUILayout.Label(GetMessage(controller.LastConnectionFailure), messageStyle);
        GUILayout.Space(12f);
        GUILayout.Label("This interruption did not generate a match result.", messageStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("OK", GUILayout.Height(36f))) controller.DismissConnectionFailure();
        GUILayout.EndArea();
    }

    private static string GetMessage(SessionFailureReason reason)
    {
        switch (reason)
        {
            case SessionFailureReason.RemovedFromSession:
                return "You are no longer a member of this session.";
            case SessionFailureReason.SessionDeleted:
                return "The session has been closed.";
            case SessionFailureReason.HostLost:
                return "The host is no longer available. You have returned to the main menu.";
            case SessionFailureReason.ContentLoadFailed:
                return "Required match content could not be loaded. Please check your connection and try again.";
            case SessionFailureReason.LoadingTimedOut:
                return "Match loading took too long. You have returned to the main menu.";
            default:
                return "The connection to the session was lost. Please check your connection before trying again.";
        }
    }
}
