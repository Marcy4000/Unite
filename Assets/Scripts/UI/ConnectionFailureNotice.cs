using Unite.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Persistent session error presentation using an editable dialog prefab.
/// </summary>
public sealed class ConnectionFailureNotice : MonoBehaviour
{
    [SerializeField] private ConnectionFailureDialog dialogPrefab;
    private GameObject overlay;
    private TMP_Text titleText;
    private TMP_Text messageText;
    private Button dismissButton;
    private GameObject previousSelection;

    private void Update()
    {
        var controller = LobbyController.Instance;
        var visible = controller != null && !controller.IsHandlingConnectionFailure &&
            (controller.LastConnectionFailure != SessionFailureReason.None || !string.IsNullOrEmpty(controller.LastOperationError));
        if (!visible)
        {
            if (overlay != null && overlay.activeSelf)
            {
                overlay.SetActive(false);
                if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
                    EventSystem.current.SetSelectedGameObject(previousSelection);
            }
            return;
        }
        if (overlay == null && !BuildOverlay()) return;
        titleText.text = controller.LastConnectionFailure != SessionFailureReason.None ? "Connection interrupted" : "Unable to connect";
        messageText.text = controller.LastConnectionFailure != SessionFailureReason.None
            ? GetMessage(controller.LastConnectionFailure) + "\n\nThis interruption did not generate a match result."
            : controller.LastOperationError;
        if (!overlay.activeSelf)
        {
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            overlay.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(dismissButton.gameObject);
        }
        if (EventSystem.current != null && (EventSystem.current.currentSelectedGameObject == null ||
            !EventSystem.current.currentSelectedGameObject.transform.IsChildOf(overlay.transform)))
            EventSystem.current.SetSelectedGameObject(dismissButton.gameObject);
        Cursor.visible = true;
    }

    private bool BuildOverlay()
    {
        var prefab = dialogPrefab;
        if (prefab == null)
        {
            var asset = Resources.Load<GameObject>("UI/ConnectionFailureDialog");
            if (asset != null) prefab = asset.GetComponent<ConnectionFailureDialog>();
        }
        if (prefab == null)
        {
            Debug.LogError("Missing connection-error dialog prefab: Resources/UI/ConnectionFailureDialog", this);
            enabled = false;
            return false;
        }
        var dialog = Instantiate(prefab, transform);
        overlay = dialog.gameObject;
        titleText = dialog.TitleText;
        messageText = dialog.MessageText;
        dismissButton = dialog.DismissButton;
        dismissButton.onClick.AddListener(() => LobbyController.Instance?.DismissConnectionFailure());
        overlay.SetActive(false);
        return true;
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
