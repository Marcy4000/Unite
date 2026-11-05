using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unite.Multiplayer;

public class SpectatorSelectionCard : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image portrait;
    [SerializeField] private Image battleItem;
    [SerializeField] private PlayerHeldItemsIcons heldItems;
    [SerializeField] private TMP_Text playerName;
    [SerializeField] private TMP_Text state;
    [SerializeField] private GameObject activeTurn;
    private GameSessionPlayer player;
    private string lastItems;
    private short previousCharacter = -2;
    private int gridIndex, gridColumns = 1, gridRows = 1;
    private float gridInset;
    private bool hasGridLayout;
    private Vector2 lastParentSize, lastCardSize;
    private RectTransform lastLayoutParent;
    public string PlayerId => player.Id;

    public void PlaceInGrid(int index, int columns, int rows, float inset)
    {
        gridIndex = index;
        gridColumns = Mathf.Max(1, columns);
        gridRows = Mathf.Max(1, rows);
        gridInset = Mathf.Max(0, inset);
        hasGridLayout = true;
        lastLayoutParent = null;
        UpdateGridLayout();
    }

    private void LateUpdate() { if (hasGridLayout) UpdateGridLayout(); }

    private void UpdateGridLayout()
    {
        var rect = (RectTransform)transform;
        var parent = rect.parent as RectTransform;
        if (parent == null) return;
        Vector2 available = parent.rect.size;
        Vector2 authored = rect.sizeDelta;
        if (lastLayoutParent == parent && lastParentSize == available && lastCardSize == authored) return;
        lastLayoutParent = parent;
        lastParentSize = available;
        lastCardSize = authored;
        int col = gridIndex % gridColumns, row = gridIndex / gridColumns;
        rect.anchorMin = rect.anchorMax = new Vector2((col + 0.5f) / gridColumns, 1f - (row + 0.5f) / gridRows);
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        float scale = Mathf.Clamp01(Mathf.Min((available.x / gridColumns - gridInset * 2) / Mathf.Max(1, authored.x),
            (available.y / gridRows - gridInset * 2) / Mathf.Max(1, authored.y)));
        // Keep the prefab's loading-card proportions. Fit the whole card into
        // its cell uniformly instead of changing its width/height independently.
        rect.localScale = Vector3.one * scale;
    }

    public void Bind(GameSessionPlayer participant, SpectatorTeamLayout layout)
    {
        player = participant;
        playerName.text = player.Data["PlayerName"].Value;
        Team team = TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value);
        background.color = SpectatorUIAssets.Instance.GetColor(team, layout);
    }

    public void Refresh(short character, string status, bool active, bool showItems)
    {
        // Bot participants are recreated when the network roster is composed.
        // Resolve the current snapshot so picks and their loadouts stay in sync.
        var currentPlayer = LobbyController.Instance.GetPlayerByID(player.Id);
        if (currentPlayer == null) return;
        player = currentPlayer;
        if (previousCharacter != character)
        {
            previousCharacter = character;
            var info = character >= 0 ? CharactersList.Instance.GetCharacterFromID(character) : null;
            portrait.sprite = info != null ? (info.portrait != null ? info.portrait : info.icon) : null;
            portrait.enabled = portrait.sprite != null;
            if (info != null)
            {
                portrait.transform.DOKill();
                portrait.transform.localScale = Vector3.one * 0.85f;
                portrait.transform.DOScale(1, 0.2f).SetEase(Ease.OutBack);
            }
        }
        activeTurn.SetActive(active);
        state.text = status;
        string items = player.Data["HeldItems"].Value;
        if (items != lastItems) { heldItems.SetIcons(HeldItemDatabase.DeserializeHeldItems(items)); lastItems = items; }
        heldItems.gameObject.SetActive(showItems);
        battleItem.gameObject.SetActive(showItems);
        if (showItems)
        {
            var asset = int.TryParse(player.Data["BattleItem"].Value, out int item)
                ? CharactersList.Instance.GetBattleItemByID(item) : null;
            battleItem.sprite = asset != null ? asset.icon : null;
            battleItem.enabled = battleItem.sprite != null;
        }
    }

    private void OnDestroy() => portrait.transform.DOKill();
}
