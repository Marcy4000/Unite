using DG.Tweening;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class UniteRoyalePositionUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerName;
    [SerializeField] private GameObject crownIcon;
    [SerializeField] private RectTransform holder;

    [SerializeField] private Image background, placeBG, pokemonIcon, pokemonIconFrame;

    private PlayerManager player;
    PlayerNetworkManager playerNetworkManager;
    private bool localPlayer;

    public PlayerNetworkManager PlayerNetworkManager => playerNetworkManager;

    public void AssignPlayer(PlayerNetworkManager playerNetworkManager)
    {
        this.playerNetworkManager = playerNetworkManager;
        player = playerNetworkManager.Player;
        Unity.Services.Lobbies.Models.Player lobbyPlayer = playerNetworkManager.LocalPlayer;

        playerName.text = $"0 | {lobbyPlayer.Data["PlayerName"].Value}";

        localPlayer = lobbyPlayer.Id == LobbyController.Instance.Player.Id;

        if (localPlayer)
        {
            background.color = new Color(0f, 0f, 0f, 0.97f);
            placeBG.color = new Color(184f / 255f, 105f / 255f, 255f / 255f, 255f / 255f);
            pokemonIconFrame.color = new Color(1f, 1f, 1f, 1f);
        }

        playerNetworkManager.OnPlayerStatsChanged += UpdatePlayerStats;
        player.Pokemon.OnEvolution += UpdatePokemonInfo;
        player.Pokemon.OnLevelChange += UpdatePokemonInfo;

        UpdateShownPosition();
        UpdatePokemonInfo();
        SetCrownActive(false);
    }

    public void UpdatePokemonInfo()
    {
        pokemonIcon.sprite = player.Pokemon.Portrait;
    }

    public void UpdatePlayerStats(PlayerStats playerStats)
    {
        playerName.text = $"{playerStats.kills} | {playerNetworkManager.LocalPlayer.Data["PlayerName"].Value}";
        UpdateShownPosition();
    }

    private void DoAnimation()
    {
        holder.anchoredPosition = new Vector2(-40, 0);

        holder.DOAnchorPosX(0, 0.25f).SetEase(Ease.OutBack);
    }

    private void UpdateShownPosition()
    {
        string prevText = playerName.text;

        playerName.text = $"{playerNetworkManager.PlayerStats.kills} | {playerNetworkManager.LocalPlayer.Data["PlayerName"].Value}";

        if (playerName.text != prevText && localPlayer)
        {
            DoAnimation();
        }
    }

    public void SetCrownActive(bool active)
    {
        crownIcon.SetActive(active);
    }
}
