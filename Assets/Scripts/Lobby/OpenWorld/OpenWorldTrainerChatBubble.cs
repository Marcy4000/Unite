using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class OpenWorldTrainerChatBubble : MonoBehaviour
{
    [SerializeField] private RectTransform bubbleRoot;
    [SerializeField] private TMP_Text messageText;
    [SerializeField, Min(0.1f)] private float displayDuration = 6f;
    [SerializeField, Min(1)] private int maximumCharacters = 240;
    [SerializeField] private Vector2 minimumSize = new Vector2(180f, 80f);
    [SerializeField] private Vector2 maximumSize = new Vector2(400f, 220f);
    [SerializeField] private Vector2 padding = new Vector2(48f, 52f);

    private float hideTime;

    private void Awake() => Hide();

    public void Show(string message)
    {
        if (!isActiveAndEnabled || bubbleRoot == null || messageText == null || string.IsNullOrWhiteSpace(message)) return;
        message = message.Trim();
        int length = Mathf.Min(message.Length, Mathf.Max(1, maximumCharacters));
        if (length < message.Length)
        {
            if (char.IsHighSurrogate(message[length - 1])) length--;
            message = message.Substring(0, length) + "…";
        }
        messageText.text = message;
        float width = Mathf.Clamp(messageText.GetPreferredValues(message).x + padding.x, minimumSize.x, maximumSize.x);
        float textHeight = messageText.GetPreferredValues(message, Mathf.Max(1f, width - padding.x), Mathf.Infinity).y;
        bubbleRoot.sizeDelta = new Vector2(width, Mathf.Clamp(textHeight + padding.y, minimumSize.y, maximumSize.y));
        hideTime = Time.unscaledTime + Mathf.Max(0.1f, displayDuration);
        bubbleRoot.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (bubbleRoot != null) bubbleRoot.gameObject.SetActive(false);
        if (messageText != null) messageText.text = string.Empty;
    }

    private void Update()
    {
        if (bubbleRoot != null && bubbleRoot.gameObject.activeSelf && Time.unscaledTime >= hideTime) Hide();
    }

    private void OnDisable() => Hide();
}
