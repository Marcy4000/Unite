using Unite.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Persistent, asset-independent session error presentation.
/// </summary>
public sealed class ConnectionFailureNotice : MonoBehaviour
{
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
        if (overlay == null) BuildOverlay();
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

    private void BuildOverlay()
    {
        overlay = new GameObject("Session error overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 600);
        scaler.matchWidthOrHeight = 0.5f;
        var backdrop = Box("Backdrop", overlay.transform, Vector2.zero, Vector2.one, new Color(0.01f, 0.02f, 0.05f, 0.88f));
        var panel = Box("Dialog", backdrop.transform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.82f), new Color(0.055f, 0.08f, 0.14f, 1f));
        Box("Accent", panel.transform, new Vector2(0, 0.98f), Vector2.one, new Color(0.3f, 0.85f, 1f, 1f));
        titleText = Label("Title", panel.transform, new Vector2(0.06f, 0.75f), new Vector2(0.94f, 0.94f), 30);
        titleText.fontStyle = FontStyles.Bold;
        messageText = Label("Message", panel.transform, new Vector2(0.06f, 0.28f), new Vector2(0.94f, 0.73f), 23);
        messageText.enableAutoSizing = true;
        messageText.fontSizeMin = 16;
        messageText.fontSizeMax = 23;
        var button = Box("OK", panel.transform, new Vector2(0.65f, 0.07f), new Vector2(0.94f, 0.23f), new Color(0.3f, 0.85f, 1f, 1f));
        dismissButton = button.gameObject.AddComponent<Button>();
        dismissButton.targetGraphic = button;
        dismissButton.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = dismissButton.colors;
        colors.highlightedColor = new Color(0.75f, 0.95f, 1f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.25f, 0.6f, 0.8f, 1f);
        dismissButton.colors = colors;
        dismissButton.onClick.AddListener(() => LobbyController.Instance?.DismissConnectionFailure());
        var buttonText = Label("Label", button.transform, Vector2.zero, Vector2.one, 24);
        buttonText.text = "OK";
        buttonText.color = new Color(0.025f, 0.05f, 0.09f, 1f);
        buttonText.alignment = TextAlignmentOptions.Center;
        overlay.SetActive(false);
    }

    private static Image Box(string name, Transform parent, Vector2 minimum, Vector2 maximum, Color color)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(Image));
        node.transform.SetParent(parent, false);
        var image = node.GetComponent<Image>();
        image.rectTransform.anchorMin = minimum;
        image.rectTransform.anchorMax = maximum;
        image.rectTransform.offsetMin = Vector2.zero;
        image.rectTransform.offsetMax = Vector2.zero;
        image.color = color;
        return image;
    }

    private static TMP_Text Label(string name, Transform parent, Vector2 minimum, Vector2 maximum, float size)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        node.transform.SetParent(parent, false);
        var text = node.GetComponent<TextMeshProUGUI>();
        text.rectTransform.anchorMin = minimum;
        text.rectTransform.anchorMax = maximum;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.color = Color.white;
        text.richText = false;
        text.raycastTarget = false;
        return text;
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
