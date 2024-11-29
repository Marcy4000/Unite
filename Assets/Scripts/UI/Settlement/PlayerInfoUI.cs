using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Lobbies.Models;
using System.Linq;

public enum PlayerInfoType { Normal, Stats, Moves }

public class PlayerInfoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text playerKillsText;
    [SerializeField] private TMP_Text playerAssistsText;
    [SerializeField] private TMP_Text playerBattleScoreText;

    [Space]

    [SerializeField] private StatHolderUI playerDamageHolder;
    [SerializeField] private StatHolderUI playerDamageTakenHolder;
    [SerializeField] private StatHolderUI playerHealingHolder;

    [SerializeField] private Image playerAvatarImage;
    [SerializeField] private Image playerBGImage;

    [SerializeField] private Sprite blueBG, orangeBG;

    private PlayerInfoType playerInfoType;

    public void SetPlayerInfo(Player player, PlayerInfoType type)
    {
        playerInfoType = type;

        switch (playerInfoType)
        {
            case PlayerInfoType.Normal:
                SetPlayerInfoNormal(player);
                break;
            case PlayerInfoType.Stats:
                SetPlayerInfoStats(player);
                break;
            case PlayerInfoType.Moves:
                break;
        }
    }

    public void SetPlayerInfoStats(Player player)
    {
        PlayerStats playerStats = LobbyController.Instance.GameResults.PlayerStats.FirstOrDefault(stats => stats.playerId == player.Id);

        playerNameText.text = player.Data["PlayerName"].Value;
        
        float dmgPercentage, dmgTakenPercentage, healingPercentage;

        CalculateDamagePercentage(playerStats, TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value), out dmgPercentage, out dmgTakenPercentage, out healingPercentage);

        playerDamageHolder.SetStatInfo(playerStats.damageDealt, dmgPercentage);
        playerDamageTakenHolder.SetStatInfo(playerStats.damageTaken, dmgTakenPercentage);
        playerHealingHolder.SetStatInfo(playerStats.healingDone, healingPercentage);

        playerAvatarImage.sprite = CharactersList.Instance.GetCharacterFromID(NumberEncoder.FromBase64<short>(player.Data["SelectedCharacter"].Value)).icon;

        if (player.Data["PlayerTeam"].Value == "Blue")
        {
            playerBGImage.sprite = blueBG;
        }
        else
        {
            playerBGImage.sprite = orangeBG;
        }

        if (player.Id == LobbyController.Instance.Player.Id)
        {
            playerNameText.color = Color.yellow;
        }
    }

    private void CalculateDamagePercentage(PlayerStats playerStats, Team team, out float dmgPercentage, out float dmgTakenPercentage, out float healingPercentage)
    {
        Player[] teamPlayers = LobbyController.Instance.GetTeamPlayers(team);
        PlayerStats[] teamStats = LobbyController.Instance.GameResults.PlayerStats.Where(stats => teamPlayers.Any(player => player.Id == stats.playerId)).ToArray();

        float totalDamage = teamStats.Sum(stats => stats.damageDealt);
        float totalDamageTaken = teamStats.Sum(stats => stats.damageTaken);
        float totalHealing = teamStats.Sum(stats => stats.healingDone);

        dmgPercentage = totalDamage > 0 ? playerStats.damageDealt / totalDamage : 0f;
        dmgTakenPercentage = totalDamageTaken > 0 ? playerStats.damageTaken / totalDamageTaken : 0f;
        healingPercentage = totalHealing > 0 ? playerStats.healingDone / totalHealing : 0f;
    }

    public void SetPlayerInfoNormal(Player player)
    {
        PlayerStats playerStats = LobbyController.Instance.GameResults.PlayerStats.FirstOrDefault(stats => stats.playerId == player.Id);

        playerNameText.text = player.Data["PlayerName"].Value;
        playerScoreText.text = playerStats.score.ToString();
        playerKillsText.text = playerStats.kills.ToString();
        playerAssistsText.text = playerStats.assists.ToString();
        playerBattleScoreText.text = playerStats.CalculateBattleScore().ToString();
        playerAvatarImage.sprite = CharactersList.Instance.GetCharacterFromID(NumberEncoder.FromBase64<short>(player.Data["SelectedCharacter"].Value)).icon;

        if (player.Data["PlayerTeam"].Value == "Blue")
        {
            playerBGImage.sprite = blueBG;
        }
        else
        {
            playerBGImage.sprite = orangeBG;
        }

        if (player.Id == LobbyController.Instance.Player.Id)
        {
            playerNameText.color = Color.yellow;
        }
    }
}
