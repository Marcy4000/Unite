using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class OpenWorldStageMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text status;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private OpenWorldStageTrackButton buttonPrefab;
    private readonly List<OpenWorldStageTrackButton> buttons = new List<OpenWorldStageTrackButton>();
    private OpenWorldStageJukebox source;
    private OpenWorldStageJukebox displayedSource;
    private OpenWorldStageJukebox.Track[] displayedTracks = Array.Empty<OpenWorldStageJukebox.Track>();
    private bool[] displayedPlayable = Array.Empty<bool>();
    private GameObject previousSelection;
    private GameObject lastScrolledSelection;
    public static OpenWorldStageMenu Instance { get; private set; }
    public static bool IsMenuOpen => Instance != null && Instance.panel != null && Instance.panel.activeInHierarchy;

    private void OnEnable()
    {
        Instance = this;
        Close();
        closeButton.onClick.AddListener(Close);
    }

    public void Open(OpenWorldStageJukebox jukebox)
    {
        if (!isActiveAndEnabled || jukebox == null || !jukebox.CanUseConsole(OpenWorldTrainerAvatar.LocalAvatar)) return;
        if (panel == null || scroll == null || scroll.content == null || buttonPrefab == null) return;
        if (IsMenuOpen) Close();
        source = jukebox;
        lastScrolledSelection = null;
        GetComponent<OpenWorldEmoteMenu>()?.Close();
        GetComponent<OpenWorldPokemonMenu>()?.Close();
        GetComponent<OpenWorldTrainerMenu>()?.Close();
        panel.transform.SetAsLastSibling();
        if (EventSystem.current != null) previousSelection = EventSystem.current.currentSelectedGameObject;
        panel.SetActive(true);
        Refresh();
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
    }

    public void Close()
    {
        source = null;
        if (panel == null || !panel.activeSelf) return;
        panel.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    private void Update()
    {
        if (!IsMenuOpen) return;
        if (source == null || !source.CanUseConsole(OpenWorldTrainerAvatar.LocalAvatar) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)) { Close(); return; }
        Refresh();
        ScrollSelectionIntoView();
    }

    private void EnsureButtons()
    {
        var tracks = source.Tracks;
        bool unchanged = displayedSource == source && displayedTracks.Length == tracks.Length;
        for (int index = 0; unchanged && index < tracks.Length; index++)
            unchanged = displayedTracks[index].title == tracks[index].title && displayedTracks[index].music == tracks[index].music &&
                displayedPlayable[index] == tracks[index].IsPlayable;
        if (unchanged) return;
        ClearButtons();
        displayedSource = source;
        displayedTracks = (OpenWorldStageJukebox.Track[])tracks.Clone();
        displayedPlayable = new bool[tracks.Length];
        for (int index = 0; index < tracks.Length; index++)
        {
            displayedPlayable[index] = tracks[index].IsPlayable;
            if (!source.IsValidTrack(index)) continue;
            int trackIndex = index;
            var jukebox = source;
            var button = Instantiate(buttonPrefab, scroll.content, false);
            button.name = tracks[index].DisplayName;
            button.Initialize(index, () => { if (jukebox != null) jukebox.TrySelectTrack(trackIndex); });
            button.gameObject.SetActive(true);
            buttons.Add(button);
        }
    }

    private void Refresh()
    {
        EnsureButtons();
        float remaining = source.CooldownRemaining;
        foreach (var button in buttons)
            button.Refresh(source.Tracks[button.TrackIndex].DisplayName, button.TrackIndex == source.SelectedTrack, remaining <= 0f);
        status.text = buttons.Count == 0 ? "No playable tracks configured on this jukebox."
            : remaining > 0f ? $"Next request in {Mathf.CeilToInt(remaining)}s - Shared with everyone on stage"
            : "Choose a track - Everyone on stage hears your selection";
    }

    private void ScrollSelectionIntoView()
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == lastScrolledSelection) return;
        lastScrolledSelection = selected;
        if (selected == null || scroll.viewport == null || !selected.transform.IsChildOf(scroll.content)) return;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selected.transform);
        var viewport = scroll.viewport.rect;
        float offset = bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y
            : bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y : 0f;
        if (Mathf.Approximately(offset, 0f)) return;
        scroll.StopMovement();
        scroll.content.anchoredPosition += Vector2.up * offset;
    }

    private void ClearButtons()
    {
        foreach (var button in buttons)
        {
            if (button == null) continue;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(button.transform))
                EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            button.Clear();
            button.gameObject.SetActive(false);
            Destroy(button.gameObject);
        }
        buttons.Clear();
    }

    private void OnDisable()
    {
        Close();
        closeButton.onClick.RemoveListener(Close);
        if (Instance == this) Instance = null;
    }
}
