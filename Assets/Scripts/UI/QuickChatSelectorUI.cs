using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class QuickChatSelectorUI : MonoBehaviour
{
    [SerializeField] private GameObject quickChatSelectorPanel;
    [SerializeField] private Transform buttonsHolder;
    [SerializeField] private GameObject quickChatButtonPrefab;

    [SerializeField] private ScrollRect scrollRect;

    [SerializeField] private Button button;
    [SerializeField] private Image cooldownImage;

    [Space]
    [SerializeField] private AvailableQuickChatMessages availableQuickChatMessages;

    private List<QuickChatSelectorButton> buttons = new List<QuickChatSelectorButton>();
    private int selectedButtonIndex = 0;

    private bool isPanelOpen = false;
    private bool isOnCooldown = false;

    private float inputDelay = 0.2f; // Delay in seconds
    private float lastInputTime; // Time of the last input

    private void Start()
    {
        button.onClick.AddListener(OnButtonClicked);
        quickChatSelectorPanel.SetActive(false);
        cooldownImage.fillAmount = 0f;

        InputManager.Instance.Controls.UI.OpenQuickChat.performed += OnButtonClicked;

        CreateQuickChatButtons();
    }

    private void CreateQuickChatButtons()
    {
        buttons.Clear();

        for (int i = 0; i < availableQuickChatMessages.quickChatMessages.Length; i++)
        {
            var button = Instantiate(quickChatButtonPrefab, buttonsHolder).GetComponent<QuickChatSelectorButton>();
            button.SetMessage(availableQuickChatMessages.quickChatMessages[i]);

            button.OnButtonClicked += OnQuickChatMessageSelected;

            buttons.Add(button);
        }
    }

    private void OnButtonClicked(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        OnButtonClicked();
    }

    private void OnButtonClicked()
    {
        isPanelOpen = !isPanelOpen;
        quickChatSelectorPanel.SetActive(isPanelOpen);
    }

    private void ClosePanel()
    {
        isPanelOpen = false;
        quickChatSelectorPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isPanelOpen)
            return;

        Vector2 stickValue = InputManager.Instance.Controls.Movement.AimMove.ReadValue<Vector2>();

        // Only process input if enough time has passed since the last input
        if (Time.time - lastInputTime < inputDelay)
            return;

        if (stickValue.y > 0.5f)
        {
            if (selectedButtonIndex > 0)
            {
                buttons[selectedButtonIndex].Deselect();
                selectedButtonIndex--;
                buttons[selectedButtonIndex].Select();
                UpdateScrollPosition();
                lastInputTime = Time.time; // Update the last input time
            }
        }
        else if (stickValue.y < -0.5f)
        {
            if (selectedButtonIndex < buttons.Count - 1)
            {
                buttons[selectedButtonIndex].Deselect();
                selectedButtonIndex++;
                buttons[selectedButtonIndex].Select();
                UpdateScrollPosition();
                lastInputTime = Time.time; // Update the last input time
            }
        }
    }

    private void UpdateScrollPosition()
    {
        float totalItems = buttons.Count;
        float targetPosition = (float)selectedButtonIndex / (totalItems - 1);
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(1f - targetPosition);
    }

    private void OnQuickChatMessageSelected(string message)
    {
        ClosePanel();

        if (isOnCooldown)
            return;

        ulong senderID = GameManager.Instance.Players.Where(x => x.IsLocalPlayer).FirstOrDefault().NetworkObjectId;

        QuickChatMessage quickChatMessage = new QuickChatMessage
        {
            message = message,
            senderId = senderID,
            sendingTeam = LobbyController.Instance.GetLocalPlayerTeam()
        };
        QuickChatManager.Instance.SendQuickChatMessageRPC(quickChatMessage);

        StartCoroutine(Cooldown());
    }

    private IEnumerator Cooldown()
    {
        isOnCooldown = true;
        cooldownImage.fillAmount = 1f;

        while (cooldownImage.fillAmount > 0)
        {
            cooldownImage.fillAmount -= Time.deltaTime / 5f;
            yield return null;
        }

        isOnCooldown = false;
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(OnButtonClicked);
        InputManager.Instance.Controls.UI.OpenQuickChat.performed -= OnButtonClicked;
    }
}
