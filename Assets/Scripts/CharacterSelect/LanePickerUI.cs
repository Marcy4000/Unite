using System.Collections.Generic;
using Unite.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

public class LanePickerUI : MonoBehaviour
{
    [SerializeField] private GameObject compactRoot;
    [SerializeField] private GameObject expandedRoot;
    [SerializeField] private Button openButton;
    [Tooltip("Additional buttons that open this same picker, including buttons on other selection/preparation panels.")]
    [SerializeField] private List<Button> additionalOpenButtons = new List<Button>();
    [SerializeField] private Button closeButton;
    [SerializeField] private Transform optionsHolder;
    [SerializeField] private LanePickerOption optionTemplate;
    [SerializeField] private LanePickerMapView compactMap;
    [SerializeField] private LanePickerMapView expandedMap;
    [SerializeField] private bool usePreparationLayout;
    [SerializeField] private Vector2 preparationPosition = new Vector2(280, -430);
    [SerializeField] private float preparationScale = 0.45f;

    private readonly List<LanePickerOption> options = new List<LanePickerOption>();
    private readonly HashSet<Button> subscribedOpenButtons = new HashSet<Button>();
    private LaneSelectionNetwork selection;
    private LobbyController lobby;

    public void Initialize(LaneSelectionNetwork state, int teamCapacity)
    {
        Unsubscribe();
        selection = state;
        lobby = LobbyController.Instance;
        Close();
        if (usePreparationLayout) ShowPreparationLayout();
        optionTemplate.gameObject.SetActive(false);
        foreach (var option in options) Destroy(option.gameObject);
        options.Clear();

        if (state.Layout != null)
        {
            compactMap.Initialize(state.Layout, teamCapacity);
            expandedMap.Initialize(state.Layout, teamCapacity);
            foreach (var lane in state.Layout.lanes)
            {
                var option = Instantiate(optionTemplate, optionsHolder);
                option.name = lane.displayName;
                option.Initialize(lane, state);
                option.gameObject.SetActive(true);
                options.Add(option);
            }
        }

        SubscribeOpenButtons();
        closeButton.onClick.RemoveListener(Close);
        closeButton.onClick.AddListener(Close);
        Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        SubscribeOpenButtons();
        Subscribe();
        if (selection != null) Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeOpenButtons();
        Unsubscribe();
        Close();
    }

    private void OnDestroy()
    {
        UnsubscribeOpenButtons();
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    public void RegisterOpener(Button button)
    {
        if (button == null) return;
        if (button != openButton && !additionalOpenButtons.Contains(button)) additionalOpenButtons.Add(button);
        if (isActiveAndEnabled) SubscribeOpener(button);
    }

    public void UnregisterOpener(Button button)
    {
        if (button == null) return;
        if (openButton == button) openButton = null;
        additionalOpenButtons.RemoveAll(opener => opener == button);
        if (subscribedOpenButtons.Remove(button)) button.onClick.RemoveListener(Open);
    }

    private void SubscribeOpenButtons()
    {
        UnsubscribeOpenButtons();
        if (!isActiveAndEnabled) return;
        SubscribeOpener(openButton);
        foreach (var button in additionalOpenButtons) SubscribeOpener(button);
    }

    private void SubscribeOpener(Button button)
    {
        if (button == null || !subscribedOpenButtons.Add(button)) return;
        button.onClick.AddListener(Open);
        button.interactable = selection != null && selection.CanSelect;
    }

    private void UnsubscribeOpenButtons()
    {
        foreach (var button in subscribedOpenButtons)
        {
            if (button == null) continue;
            button.onClick.RemoveListener(Open);
            button.interactable = false;
        }
        subscribedOpenButtons.Clear();
    }

    private void Subscribe()
    {
        if (selection == null || lobby == null || !isActiveAndEnabled) return;
        selection.Changed -= Refresh;
        selection.Changed += Refresh;
        lobby.onLobbyUpdate -= OnLobbyUpdated;
        lobby.onLobbyUpdate += OnLobbyUpdated;
    }

    private void Unsubscribe()
    {
        if (selection != null) selection.Changed -= Refresh;
        if (lobby != null) lobby.onLobbyUpdate -= OnLobbyUpdated;
    }

    private void OnLobbyUpdated(GameSessionView session) => Refresh();

    private void Refresh()
    {
        bool available = selection.IsAvailable;
        compactRoot.SetActive(available);
        if (!available || !selection.CanSelect) Close();
        foreach (var button in subscribedOpenButtons)
            if (button != null) button.interactable = selection.CanSelect;
        if (!available) return;

        string lane = selection.GetLane(lobby.Player.Id);
        foreach (var option in options) option.Refresh(lane, selection.CanSelect);
        var teammates = lobby.GetTeamPlayers(lobby.GetLocalPlayerTeam());
        var markers = new List<LanePickerMapView.PlayerMarker>();
        var choices = new List<LaneSelectionRoster.Choice>(selection.Choices);
        // Predict only our own choice while waiting for the server's ordered snapshot.
        // Teammates' arrival order always comes from the server.
        var localChoice = choices.Find(choice => choice.PlayerId == lobby.Player.Id);
        if ((localChoice.LaneId ?? string.Empty) != lane)
        {
            choices.RemoveAll(choice => choice.PlayerId == lobby.Player.Id);
            if (lane.Length > 0) choices.Add(new LaneSelectionRoster.Choice(lobby.Player.Id, lane));
        }
        foreach (var choice in choices)
        {
            var player = System.Array.Find(teammates, teammate => teammate.Id == choice.PlayerId);
            if (player == null || !player.Data.TryGetValue("SelectedCharacter", out var property)) continue;
            short characterId = NumberEncoder.FromBase64<short>(property.Value);
            var character = characterId < 0 ? null : CharactersList.Instance.GetCharacterFromID(characterId);
            markers.Add(new LanePickerMapView.PlayerMarker
            {
                PlayerId = player.Id,
                LaneId = choice.LaneId,
                Portrait = character != null ? character.CircleIcon : null
            });
        }
        compactMap.Refresh(lane, lobby.Player.Id, markers);
        expandedMap.Refresh(lane, lobby.Player.Id, markers);
    }

    public void Open()
    {
        if (!isActiveAndEnabled || selection == null || !selection.CanSelect) return;
        expandedRoot.SetActive(true);
        Refresh();
    }

    public void Close() => expandedRoot.SetActive(false);

    public void ShowPreparationLayout()
    {
        var rect = (RectTransform)compactRoot.transform;
        rect.anchoredPosition = preparationPosition;
        rect.localScale = Vector3.one * preparationScale;
    }
}
