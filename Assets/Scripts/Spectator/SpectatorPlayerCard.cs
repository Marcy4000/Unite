using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpectatorPlayerCard : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private Image background;
    [SerializeField] private Image hpBar;
    [SerializeField] private GameObject uniteReady;
    [SerializeField] private GameObject deathOverlay;
    [SerializeField] private TMP_Text deathTimer;
    [SerializeField] private TMP_Text level;
    [SerializeField] private TMP_Text energy;
    [SerializeField] private TMP_Text playerName;
    [SerializeField] private GameObject selected;
    [SerializeField] private Button button;
    private PlayerManager player;
    private bool wasSelected;
    public PlayerManager Player => player;

    public void PlaceInRoster(int index, int count, float spacing)
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        // The prefab owns width/height. Neither team size nor viewport width
        // may change its proportions; only the centered row position changes.
        rect.anchoredPosition = new Vector2((index - (count - 1) * 0.5f) * (rect.sizeDelta.x + spacing), 0);
    }

    public void Bind(PlayerManager actor, SpectatorTeamLayout layout)
    {
        player = actor;
        var assets = SpectatorUIAssets.Instance;
        background.sprite = assets.GetBackground(actor.CurrentTeam.Team, layout);
        background.color = assets.GetColor(actor.CurrentTeam.Team, layout);
        playerName.text = actor.LobbyPlayer != null ? actor.LobbyPlayer.Data["PlayerName"].Value : actor.name;
        button.onClick.AddListener(Follow);
        if (actor.MovesController != null && actor.MovesController.IsSpawned) actor.MovesController.RequestUniteReadyRpc();
        Refresh();
    }

    private void Follow()
    {
        if (player != null && CameraController.Instance != null) CameraController.Instance.FollowSpectator(player.transform);
    }

    public void Refresh()
    {
        if (player == null || player.Pokemon == null) return;
        var pokemon = player.Pokemon;
        portrait.sprite = pokemon.Portrait;
        portrait.enabled = portrait.sprite != null;
        int maxHP = pokemon.BaseStats != null ? pokemon.GetMaxHp() : 0;
        hpBar.fillAmount = maxHP > 0 ? Mathf.Clamp01((float)pokemon.CurrentHp / maxHP) : 0;
        bool dead = player.PlayerState == PlayerState.Dead;
        deathOverlay.SetActive(dead);
        deathTimer.text = dead ? Mathf.Max(0, Mathf.CeilToInt(player.DeathTimer)).ToString() : string.Empty;
        uniteReady.SetActive(!dead && player.MovesController != null && player.MovesController.UniteIsReady);
        level.text = "Lv." + (pokemon.CurrentLevel + 1);
        energy.text = player.CurrentEnergy.ToString();
        bool locked = CameraController.Instance != null && CameraController.Instance.SpectatorTarget == player.transform;
        selected.SetActive(locked);
        if (locked != wasSelected)
        {
            transform.DOKill();
            transform.DOScale(locked ? 1.06f : 1f, 0.15f).SetEase(Ease.OutQuad);
            wasSelected = locked;
        }
    }

    private void OnDestroy() { transform.DOKill(); button.onClick.RemoveListener(Follow); }
}
