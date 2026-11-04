using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class SpectatorBattleHUD : MonoBehaviour
{
    [SerializeField] private RectTransform bar;
    [SerializeField] private RectTransform rosterContent;
    [SerializeField] private RectTransform rosterViewport;
    [SerializeField] private TMP_Text cameraState;
    [SerializeField, Min(0)] private float cardSpacing = 12;
    [SerializeField, Min(0)] private float teamGap = 28;
    private readonly List<SpectatorPlayerCard> cards = new();
    private readonly List<SpectatorTeamPanel> panels = new();
    private readonly List<List<SpectatorPlayerCard>> groupedCards = new();
    private GameManager match;
    private bool cameraReady;
    private bool initialFollowAssigned;
    private float nextRefresh;
    private float lastViewportWidth;

    private void Start()
    {
        var destination = bar.anchoredPosition;
        float cardHeight = ((RectTransform)SpectatorUIAssets.Instance.playerCard.transform).sizeDelta.y;
        bar.anchoredPosition = destination - Vector2.up * (cardHeight + 32);
        bar.DOAnchorPos(destination, 0.3f).SetEase(Ease.OutCubic);
    }

    private void Update()
    {
        if (!cameraReady && CameraController.Instance != null && GameManager.Instance != null)
        {
            CameraController.Instance.InitializeSpectator(GameManager.Instance.CurrentMap.spectatorCameraStart);
            cameraReady = true;
        }
        if (match == null && GameManager.Instance != null)
        {
            match = GameManager.Instance;
            match.OnPlayerAdded += OnRosterChanged;
            match.OnPlayerRemoved += OnRosterChanged;
            Rebuild();
        }
        FollowFirstPlayerOnStartup();
        if (match != null && Mathf.Abs(rosterViewport.rect.width - lastViewportWidth) > 0.5f)
            LayoutRoster(match.CurrentMap.spectatorBattleLayout);
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        foreach (var card in cards) card.Refresh();
        bool locked = CameraController.Instance != null && CameraController.Instance.SpectatorTarget != null;
        cameraState.text = locked ? "Following player  ·  Pan to unlock" : "Free camera  ·  Select a player to follow";
    }

    private void FollowFirstPlayerOnStartup()
    {
        if (initialFollowAssigned || !cameraReady || CameraController.Instance == null) return;
        var first = cards.FirstOrDefault(card => card != null && card.Player != null);
        if (first == null) return; // Actors may register after the spectator HUD/camera.
        if (CameraController.Instance.SpectatorTarget == null)
            CameraController.Instance.FollowSpectator(first.Player.transform);
        // This is a startup default only. Roster rebuilds must not undo panning
        // or a player's manually selected follow target.
        initialFollowAssigned = true;
    }

    private void OnRosterChanged(PlayerManager _) => Rebuild();
    private void Rebuild()
    {
        foreach (var panel in panels) if (panel != null) Destroy(panel.gameObject);
        panels.Clear(); cards.Clear(); groupedCards.Clear();
        if (match == null) return;
        var assets = SpectatorUIAssets.Instance;
        var layout = match.CurrentMap.spectatorBattleLayout;
        var actors = match.Players.Where(p => p != null).OrderBy(p => p.CurrentTeam.Team)
            .ThenBy(p => p.LobbyPlayer != null ? p.LobbyPlayer.Data["PlayerPos"].Value : string.Empty).ToArray();
        var groups = actors.GroupBy(p => layout == SpectatorTeamLayout.BlueVersusOthers
            ? (p.CurrentTeam.Team == Team.Blue ? Team.Blue : Team.Orange) : p.CurrentTeam.Team).ToArray();
        foreach (var group in groups)
        {
            var panel = Instantiate(assets.teamPanel, rosterContent);
            panels.Add(panel);
            panel.Initialize(layout == SpectatorTeamLayout.BlueVersusOthers && group.Key == Team.Orange ? "Orange / Other teams" : group.Key + " team",
                assets.GetColor(group.Key, layout));
            var teamCards = new List<SpectatorPlayerCard>();
            groupedCards.Add(teamCards);
            foreach (var actor in group)
            {
                var card = Instantiate(assets.playerCard, panel.Content);
                card.Bind(actor, layout);
                cards.Add(card);
                teamCards.Add(card);
            }
        }
        LayoutRoster(layout);
    }

    private void LayoutRoster(SpectatorTeamLayout layout)
    {
        lastViewportWidth = rosterViewport.rect.width;
        float width = Mathf.Max(1, lastViewportWidth);
        int total = groupedCards.Sum(group => group.Count);
        if (total == 0) return;
        float cardWidth = ((RectTransform)SpectatorUIAssets.Instance.playerCard.transform).sizeDelta.x;
        // Grow the scrollable roster instead of stretching or squeezing cards.
        // Every group must accommodate its fixed-size cards, gaps and inset.
        foreach (var group in groupedCards)
        {
            float required = group.Count * cardWidth + Mathf.Max(0, group.Count - 1) * cardSpacing + teamGap + 6;
            float share = layout == SpectatorTeamLayout.EqualTeams ? 1f / groupedCards.Count : (float)group.Count / total;
            width = Mathf.Max(width, required / share);
        }
        rosterContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        float x = 0;
        for (int i = 0; i < groupedCards.Count; i++)
        {
            var group = groupedCards[i];
            float groupWidth = layout == SpectatorTeamLayout.EqualTeams ? width / groupedCards.Count : width * group.Count / total;
            SpectatorTeamPanel.Place(panels[i].Rect, new Vector2(x / width, 0), new Vector2((x + groupWidth) / width, 1), 3);
            for (int j = 0; j < group.Count; j++) group[j].PlaceInRoster(j, group.Count, cardSpacing);
            x += groupWidth;
        }
    }

    private void OnDestroy()
    {
        bar.DOKill();
        if (match == null) return;
        match.OnPlayerAdded -= OnRosterChanged;
        match.OnPlayerRemoved -= OnRosterChanged;
    }
}
