using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>The city trainer profile, hosted by OpenWorldLobbyUI.</summary>
public sealed class OpenWorldTrainerMenu : MonoBehaviour
{
    public static OpenWorldTrainerMenu Instance { get; private set; }
    public static bool IsMenuOpen => Instance != null && Instance.panel != null && Instance.panel.activeInHierarchy;

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text playerName;
    [SerializeField] private TrainerCardUI card;
    [SerializeField] private Button closeButton;
    private OpenWorldTrainerAvatar target;
    [SerializeField] private ChatManager chat;
    [SerializeField] private Button privateChat;

    private void OnEnable()
    {
        Instance = this;
        closeButton.onClick.AddListener(Close);
        privateChat.onClick.AddListener(OpenPrivateChat);
    }

    public void Open(OpenWorldTrainerAvatar player)
    {
        if (panel == null || player == null || !player.IsSpawned || player == OpenWorldTrainerAvatar.LocalAvatar) return;
        Close();
        target = player;
        target.IdentityChanged += Refresh;
        Refresh(player);
        panel.transform.SetAsLastSibling();
        panel.SetActive(true);
        privateChat.interactable = chat != null;
    }

    private void Refresh(OpenWorldTrainerAvatar player)
    {
        playerName.text = player.PlayerName;
        card.Initialize(PlayerClothesInfo.Deserialize(player.Clothing));
    }

    public void Close()
    {
        if (target != null) target.IdentityChanged -= Refresh;
        target = null;
        if (panel != null) panel.SetActive(false);
    }

    private void OpenPrivateChat()
    {
        if (target == null || !target.IsSpawned || chat == null) { Close(); return; }
        string id = target.PlayerId;
        Close();
        chat.OpenPrivateChat(id);
    }

    private void Update()
    {
        if (!IsMenuOpen) return;
        if (target == null || !target.IsSpawned || OpenWorldTrainerAvatar.LocalAvatar == null ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)) Close();
    }

    private void OnDisable()
    {
        Close();
        closeButton.onClick.RemoveListener(Close);
        privateChat.onClick.RemoveListener(OpenPrivateChat);
        if (Instance == this) Instance = null;
    }
    private void OnDestroy()
    {
        Close();
        if (Instance == this) Instance = null;
    }

}
