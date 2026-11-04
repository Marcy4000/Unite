using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Read-only presentation: all timers, picks and phase transitions are still
// owned by the original scene's NetworkBehaviour, including on a spectator host.
public class SpectatorSelectionOverlay : MonoBehaviour
{
    [SerializeField] private RectTransform teamsRoot;
    [SerializeField] private RectTransform mapRoot;
    [SerializeField] private Image mapPreview;
    [SerializeField] private TMP_Text mapName;
    [SerializeField] private TMP_Text modeMessage;
    [SerializeField] private TMP_Text bans;
    [SerializeField] private RectTransform blueBanHolder;
    [SerializeField] private RectTransform orangeBanHolder;
    [SerializeField] private DraftTimerUI timer;
    [SerializeField] private CanvasGroup fade;
    private readonly List<SpectatorSelectionCard> cards = new();
    private CharacterSelectController blind;
    private DraftSelectController draft;
    private RacingReadyScreenManager race;
    private float nextRefresh;
    private string previousMessage;
    private int previousSecond = int.MinValue;
    private Team previousActiveTeam;
    private readonly Dictionary<Team, List<DraftBanHolder>> banIcons = new();
    private readonly Dictionary<Team, int> renderedBanCounts = new();

    public static void Show(MonoBehaviour source)
    {
        var assets = SpectatorUIAssets.Instance;
        if (!SpectatorUIAssets.IsSpectating || assets == null || assets.selectionOverlay == null) return;
        var view = Instantiate(assets.selectionOverlay);
        view.blind = source as CharacterSelectController;
        view.draft = source as DraftSelectController;
        view.race = source as RacingReadyScreenManager;
    }

    private void Start()
    {
        var lobby = LobbyController.Instance;
        var map = CharactersList.Instance.GetCurrentLobbyMap();
        blueBanHolder.gameObject.SetActive(draft != null);
        orangeBanHolder.gameObject.SetActive(draft != null);
        if (draft != null)
        {
            foreach (var team in new[] { Team.Blue, Team.Orange })
            {
                var holder = team == Team.Blue ? blueBanHolder : orangeBanHolder;
                var icons = new List<DraftBanHolder>();
                int count = Mathf.Min(draft.SpectatorMaxBans, lobby.GetTeamPlayers(team).Length);
                for (int i = 0; i < count; i++)
                {
                    var icon = Instantiate(SpectatorUIAssets.Instance.banSlot, holder);
                    SpectatorTeamPanel.Place((RectTransform)icon.transform, new Vector2((float)i / count, 0), new Vector2((float)(i + 1) / count, 1), 4);
                    icons.Add(icon);
                }
                banIcons[team] = icons;
                renderedBanCounts[team] = 0;
            }
        }
        mapPreview.sprite = map.mapIcon;
        mapName.text = map.mapName;
        var players = lobby.Combatants.ToArray();
        var layout = map.spectatorSelectionLayout;
        var groups = players.GroupBy(p => layout == SpectatorTeamLayout.BlueVersusOthers
            ? (p.Data["PlayerTeam"].Value == Team.Blue.ToString() ? Team.Blue : Team.Orange)
            : TeamMember.GetTeamFromString(p.Data["PlayerTeam"].Value)).ToArray();
        float x = 0;
        for (int i = 0; i < groups.Length; i++)
        {
            var group = groups[i];
            var panel = Instantiate(SpectatorUIAssets.Instance.teamPanel, teamsRoot);
            panel.Initialize(layout == SpectatorTeamLayout.BlueVersusOthers && group.Key == Team.Orange ? "Orange / Other teams" : group.Key + " team",
                SpectatorUIAssets.Instance.GetColor(group.Key, layout));
            if (groups.Length == 2)
            {
                SpectatorTeamPanel.Place(panel.Rect, new Vector2(i == 0 ? 0.015f : 0.715f, 0.02f),
                    new Vector2(i == 0 ? 0.285f : 0.985f, 0.98f));
            }
            else
            {
                float width = layout == SpectatorTeamLayout.EqualTeams ? 1f / groups.Length : (float)group.Count() / players.Length;
                SpectatorTeamPanel.Place(panel.Rect, new Vector2(x, 0.02f), new Vector2(x + width, 0.7f), 6);
                x += width;
            }
            var members = group.OrderBy(p => NumberEncoder.FromBase64<short>(p.Data["PlayerPos"].Value)).ToArray();
            int columns = race != null ? Mathf.Max(1, members.Length) : groups.Length == 2 && members.Length > 3 ? 2 : 1;
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)members.Length / columns));
            for (int j = 0; j < members.Length; j++)
            {
                var card = Instantiate(SpectatorUIAssets.Instance.selectionCard, panel.Content);
                card.PlaceInGrid(j, columns, rows, 7);
                card.Bind(members[j], layout);
                cards.Add(card);
            }
        }
        if (groups.Length != 2)
            SpectatorTeamPanel.Place(mapRoot, new Vector2(0.33f, 0.73f), new Vector2(0.67f, 0.98f));
        fade.alpha = 0;
        fade.DOFade(1, 0.3f);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        string message = race != null ? "Race preparation" : draft != null ? draft.SpectatorPhaseMessage :
            blind != null && blind.SpectatorIsPreparation ? "Final preparations" : "Pokémon selection";
        float remaining = draft != null ? draft.SpectatorTimeRemaining : blind != null ? blind.SpectatorTimeRemaining : 0;
        var activeTeam = draft != null ? draft.SpectatorActiveTeam : Team.Neutral;
        if (previousMessage != message || previousActiveTeam != activeTeam)
        {
            previousMessage = message;
            previousActiveTeam = activeTeam;
            timer.UpdateMessage(message);
            timer.DoFadeIn(draft != null && draft.SpectatorActiveTeam != Team.Neutral
                ? (byte)(draft.SpectatorActiveTeam == Team.Blue ? 0 : 1) : (byte)2);
            previousSecond = int.MinValue;
        }
        int second = Mathf.Max(0, Mathf.RoundToInt(remaining));
        if (race == null && previousSecond != second) { timer.UpdateTimer(second); previousSecond = second; }
        timer.SetCountdownVisible(race == null);
        modeMessage.text = race != null ? "Waiting for racers  ·  " + race.SpectatorReadyCount + "/" + cards.Count + " ready" : "SPECTATING";
        if (draft != null)
        {
            bans.text = string.Join("     ", new[] { Team.Blue, Team.Orange }.Select(team => team + " bans: " +
                string.Join(", ", draft.GetSpectatorBans(team).Select(id => id < 0 ? "None" : CharactersList.Instance.GetCharacterFromID(id).pokemonName))));
            foreach (var pair in banIcons)
            {
                var confirmed = draft.GetSpectatorBans(pair.Key).ToArray();
                for (int i = renderedBanCounts[pair.Key]; i < Mathf.Min(confirmed.Length, pair.Value.Count); i++)
                    pair.Value[i].SetBanIcon(CharactersList.Instance.GetCharacterFromID(confirmed[i]));
                renderedBanCounts[pair.Key] = confirmed.Length;
            }
        }
        else bans.text = string.Empty;
        foreach (var card in cards)
        {
            var player = LobbyController.Instance.GetPlayerByID(card.PlayerId);
            if (player == null) continue;
            short character = race != null ? race.SpectatorCharacter : draft != null ? draft.GetSpectatorCharacter(card.PlayerId) :
                NumberEncoder.FromBase64<short>(player.Data["SelectedCharacter"].Value);
            bool active = draft != null && draft.IsSpectatorActivePlayer(card.PlayerId);
            string status = race != null ? (race.IsSpectatorRacerReady(card.PlayerId) ? "Ready" : "Preparing…") :
                draft != null && draft.SpectatorIsBanning ? (active ? "Banning…" : character >= 0 ? "Banned" : "Waiting") : character < 0 ? "Choosing…" : "";
            card.Refresh(character, status, active, race == null && !(draft != null && draft.SpectatorIsBanning));
        }
    }

    private void OnDestroy() { fade.DOKill(); }
}
