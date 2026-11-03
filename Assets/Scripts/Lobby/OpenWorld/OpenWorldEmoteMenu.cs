using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class OpenWorldEmoteMenu : MonoBehaviour
{
    [Serializable]
    private struct AnimationButton
    {
        [SerializeField] public int animationIndex;
        [SerializeField] public Button button;
        [SerializeField] public TMP_Text label;
    }

    [SerializeField] private OpenWorldEmoteCatalog catalog;
    [SerializeField] private Button openButton;
    [SerializeField] private GameObject panel;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Button buttonPrefab;

    private AnimationButton[] animationButtons = Array.Empty<AnimationButton>();

    private static OpenWorldEmoteMenu activeMenu;
    private Animator displayedAnimator;
    public static bool IsMenuOpen => activeMenu != null && activeMenu.panel != null && activeMenu.panel.activeInHierarchy;

    private void OnEnable()
    {
        OpenWorldTrainerAvatar.LocalAvatarChanged += OnLocalAvatarChanged;
        Close();
    }

    private void OnDisable()
    {
        OpenWorldTrainerAvatar.LocalAvatarChanged -= OnLocalAvatarChanged;
        Close();
    }

    public void Toggle()
    {
        if (panel == null) return;
        if (panel.activeSelf)
        {
            Close();
            return;
        }
        var emotes = LocalEmotes();
        if (emotes == null || !emotes.CanPlay) return;
        if (activeMenu != null && activeMenu != this) activeMenu.Close();
        RefreshButtons(emotes);
        activeMenu = this;
        panel.SetActive(true);
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        if (activeMenu == this) activeMenu = null;
    }

    public void PlayEmote(int index)
    {
        Close();
        LocalEmotes()?.TryPlay(index);
    }

    private static OpenWorldTrainerEmotes LocalEmotes() => OpenWorldTrainerAvatar.LocalAvatar != null
        ? OpenWorldTrainerAvatar.LocalAvatar.GetComponent<OpenWorldTrainerEmotes>() : null;

    private void OnLocalAvatarChanged(OpenWorldTrainerAvatar avatar)
    {
        displayedAnimator = null;
        Close();
    }

    private void Update()
    {
        var emotes = LocalEmotes();
        if (openButton != null) openButton.interactable = emotes != null && emotes.IsReady;
        if (panel == null || !panel.activeSelf) return;
        if (emotes == null || !emotes.IsReady ||
            (SettingsManager.Instance != null && SettingsManager.Instance.IsMenuOpen) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            Close();
            return;
        }
        if (displayedAnimator != emotes.ActiveAnimator) RefreshButtons(emotes);
    }

    private void RefreshButtons(OpenWorldTrainerEmotes emotes)
    {
        displayedAnimator = emotes.ActiveAnimator;
        EnsureButtons();
        foreach (var row in animationButtons)
        {
            if (row.button == null) continue;
            bool available = catalog != null && catalog == emotes.Catalog &&
                catalog.TryGet(row.animationIndex, displayedAnimator, out _);
            row.button.gameObject.SetActive(available);
            if (available && row.label != null) row.label.text = catalog.Entries[row.animationIndex].DisplayName;
        }
    }

    private void EnsureButtons()
    {
        if (catalog == null || buttonPrefab == null || scroll == null || scroll.content == null ||
            animationButtons.Length == catalog.Entries.Count) return;
        foreach (var row in animationButtons)
        {
            row.button.gameObject.SetActive(false);
            Destroy(row.button.gameObject);
        }
        animationButtons = new AnimationButton[catalog.Entries.Count];
        for (int index = 0; index < animationButtons.Length; index++)
        {
            int animationIndex = index;
            var button = Instantiate(buttonPrefab, scroll.content, false);
            button.name = catalog.Entries[index]?.DisplayName ?? "Emote";
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => PlayEmote(animationIndex));
            animationButtons[index] = new AnimationButton
            {
                animationIndex = index,
                button = button,
                label = button.GetComponentInChildren<TMP_Text>(true)
            };
        }
    }
}
