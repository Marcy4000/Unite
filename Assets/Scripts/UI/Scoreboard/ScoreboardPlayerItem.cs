using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreboardPlayerItem : MonoBehaviour
{
    [SerializeField] private TMP_Text playerName, pokemonName, playerLevel, kills, assists, deathTimer;
    [SerializeField] private Image pokemonIcon, background, battleItem;
    [SerializeField] private GameObject deathTimerBG;

    [SerializeField] private Sprite blueBG, orangeBG;

    private PlayerManager player;

    public void SetPlayerInfo(PlayerManager player)
    {
        this.player = player;

        if (player.LobbyPlayer.Id == LobbyController.Instance.Player.Id)
        {
            pokemonName.color = Color.yellow;
        }

        playerName.text = player.LobbyPlayer.Data["PlayerName"].Value;
        pokemonName.text = player.Pokemon.BaseStats.PokemonName;
        playerLevel.text = $"{player.Pokemon.CurrentLevel+1}";
        kills.text = player.PlayerStats.kills.ToString();
        assists.text = player.PlayerStats.assists.ToString();
        pokemonIcon.sprite = player.Pokemon.Portrait;
        background.sprite = player.OrangeTeam ? orangeBG : blueBG;

        deathTimerBG.SetActive(false);

        BattleItemAsset selectedBattleItem = CharactersList.Instance.GetBattleItemByID(int.Parse(player.LobbyPlayer.Data["BattleItem"].Value));
        battleItem.sprite = selectedBattleItem.icon;

        player.Pokemon.OnEvolution += UpdatePokemonInfo;
        player.Pokemon.OnLevelChange += UpdatePokemonInfo;
        player.Pokemon.OnDeath += OnDeath;
        player.OnRespawn += OnRespawn;
        if (GameManager.Instance.TryGetPlayerNetworkManager(player.OwnerClientId, out PlayerNetworkManager playerNetworkManager))
        {
            playerNetworkManager.OnDeathTimerChanged += UpdateDeathTimer;
        }

        if (GameManager.Instance.TryGetPlayerNetworkManager(player.OwnerClientId, out PlayerNetworkManager networkManager))
        {
            networkManager.OnPlayerStatsChanged += UpdatePlayerStats;
        }
    }

    public void UpdatePlayerStats(PlayerStats playerStats)
    {
        kills.text = playerStats.kills.ToString();
        assists.text = playerStats.assists.ToString();
    }

    public void UpdatePokemonInfo()
    {
        pokemonName.text = player.Pokemon.BaseStats.PokemonName;
        pokemonIcon.sprite = player.Pokemon.Portrait;
        playerLevel.text = $"{player.Pokemon.CurrentLevel + 1}";
    }

    private void UpdateDeathTimer(float time)
    {
        deathTimer.text = Mathf.RoundToInt(time).ToString();
    }

    private void OnDeath(DamageInfo info)
    {
        deathTimerBG.SetActive(true);
    }

    private void OnRespawn()
    {
        deathTimerBG.SetActive(false);
    }
}
