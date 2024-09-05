using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Linq;

public class QuickChatSelectorUI : MonoBehaviour
{
    [SerializeField] private GameObject quickChatSelectorPanel;
    [SerializeField] private Transform buttonsHolder;
    [SerializeField] private GameObject quickChatButtonPrefab;

    [SerializeField] private Button button;
    [SerializeField] private Image cooldownImage;

    [Space]
    [SerializeField] private AvailableQuickChatMessages availableQuickChatMessages;

    private bool isPanelOpen = false;
    private bool isOnCooldown = false;

    private void Start()
    {
        button.onClick.AddListener(OnButtonClicked);
        quickChatSelectorPanel.SetActive(false);
        cooldownImage.fillAmount = 0f;
        CreateQuickChatButtons();
    }

    private void CreateQuickChatButtons()
    {
        for (int i = 0; i < availableQuickChatMessages.quickChatMessages.Length; i++)
        {
            var button = Instantiate(quickChatButtonPrefab, buttonsHolder).GetComponent<QuickChatSelectorButton>();
            button.SetMessage(availableQuickChatMessages.quickChatMessages[i]);

            button.OnButtonClicked += OnQuickChatMessageSelected;
        }
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
            orangeTeam = LobbyController.Instance.GetLocalPlayerTeam()
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
}
