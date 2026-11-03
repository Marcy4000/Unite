using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class OpenWorldPokemonMenu : MonoBehaviour
{
    [Serializable]
    private struct PokemonButton
    {
        public CharacterInfo character;
        public Button button;
    }

    [SerializeField] private Button openButton;
    [SerializeField] private Image currentIcon;
    [SerializeField] private TMP_Text currentName;
    [SerializeField] private TMP_Text status;
    [SerializeField] private GameObject panel;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Button buttonPrefab;

    private PokemonButton[] pokemonButtons = Array.Empty<PokemonButton>();
    private CharacterInfo[] displayedRoster;

    private static OpenWorldPokemonMenu activeMenu;
    private OpenWorldPokemonCompanion companion;
    public static bool IsMenuOpen => activeMenu != null && activeMenu.panel != null && activeMenu.panel.activeInHierarchy;

    private void OnEnable()
    {
        OpenWorldTrainerAvatar.LocalAvatarChanged += OnLocalAvatarChanged;
        OnLocalAvatarChanged(OpenWorldTrainerAvatar.LocalAvatar);
    }

    private void OnDisable()
    {
        OpenWorldTrainerAvatar.LocalAvatarChanged -= OnLocalAvatarChanged;
        OnLocalAvatarChanged(null);
    }

    private void OnLocalAvatarChanged(OpenWorldTrainerAvatar avatar)
    {
        if (companion != null) companion.SelectionChanged -= RefreshSelection;
        companion = avatar != null ? avatar.GetComponent<OpenWorldPokemonCompanion>() : null;
        if (companion != null) companion.SelectionChanged += RefreshSelection;
        Close();
        RefreshSelection();
    }

    public void Toggle()
    {
        if (panel == null) return;
        if (panel.activeSelf) { Close(); return; }
        if (companion == null || !companion.CanSelect) return;
        if (activeMenu != null && activeMenu != this) activeMenu.Close();
        GetComponent<OpenWorldEmoteMenu>()?.Close();
        EnsureButtons();
        RefreshSelection();
        activeMenu = this;
        panel.SetActive(true);
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        if (activeMenu == this) activeMenu = null;
    }

    public void SelectPokemon(int index)
    {
        if (companion == null || pokemonButtons == null || index < 0 || index >= pokemonButtons.Length ||
            CharactersList.Instance == null) return;
        if (companion.TrySelect(CharactersList.Instance.GetCharacterID(pokemonButtons[index].character))) Close();
    }

    private void EnsureButtons()
    {
        var roster = CharactersList.Instance != null ? CharactersList.Instance.Characters : null;
        if (roster == null || roster == displayedRoster || buttonPrefab == null || scroll == null || scroll.content == null) return;
        foreach (var row in pokemonButtons)
        {
            row.button.gameObject.SetActive(false);
            Destroy(row.button.gameObject);
        }
        displayedRoster = roster;
        var rows = new List<PokemonButton>();
        for (int index = 0; index < roster.Length && index <= short.MaxValue; index++)
        {
            var character = roster[index];
            if (character == null || character.Hidden || character.pokemon == null || !character.pokemon.RuntimeKeyIsValid()) continue;
            int buttonIndex = rows.Count;
            var button = Instantiate(buttonPrefab, scroll.content, false);
            button.name = character.pokemonName;
            button.GetComponentInChildren<TMP_Text>(true).text = character.pokemonName;
            button.transform.Find("Portrait").GetComponent<Image>().sprite = character.CircleIcon;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => SelectPokemon(buttonIndex));
            rows.Add(new PokemonButton { character = character, button = button });
        }
        pokemonButtons = rows.ToArray();
    }

    private void RefreshSelection()
    {
        var selected = companion != null ? companion.SelectedPokemon : null;
        if (currentIcon != null)
        {
            currentIcon.sprite = selected != null ? selected.CircleIcon : null;
            currentIcon.enabled = currentIcon.sprite != null;
        }
        if (currentName != null) currentName.text = selected != null ? selected.pokemonName : "Companion";
        if (pokemonButtons == null) return;
        foreach (var row in pokemonButtons)
        {
            if (row.button == null) continue;
            row.button.gameObject.SetActive(row.character != null && !row.character.Hidden);
            if (row.button.targetGraphic != null) row.button.targetGraphic.color = row.character == selected
                ? new Color(0.38f, 0.28f, 0.62f, 1f) : Color.white;
        }
    }

    private void Update()
    {
        bool ready = companion != null && companion.CanSelect;
        if (openButton != null) openButton.interactable = ready;
        if (status != null && companion != null) status.text = companion.LoadStatus;
        if (panel == null || !panel.activeSelf) return;
        if (!ready || (SettingsManager.Instance != null && SettingsManager.Instance.IsMenuOpen) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)) Close();
    }
}
