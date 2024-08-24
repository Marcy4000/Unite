using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class PreviewScreenUI : MonoBehaviour
{
    [SerializeField] private DraftPlayerHolder draftPlayerHolderBlue;
    [SerializeField] private DraftPlayerHolder draftPlayerHolderOrange;
    [SerializeField] private DraftTimerUI draftTimerUI;

    public void InitializeUI()
    {
        bool localPlayerTeam = LobbyController.Instance.GetLocalPlayerTeam();

        List<Player> blueTeamPlayers = LobbyController.Instance.GetTeamPlayers(localPlayerTeam).ToList();
        List<Player> orangeTeamPlayers = LobbyController.Instance.GetTeamPlayers(!localPlayerTeam).ToList();

        draftPlayerHolderBlue.Initialize(blueTeamPlayers);
        draftPlayerHolderOrange.Initialize(orangeTeamPlayers);
        draftTimerUI.DoFadeIn(2);

        foreach (var playerIcon in draftPlayerHolderBlue.PlayerIcons)
        {
            playerIcon.UpdateIconState(DraftPlayerState.Confirmed);
            playerIcon.UpdateSelectedCharacter(CharactersList.Instance.GetCharacterFromString(playerIcon.AssignedPlayer.Data["SelectedCharacter"].Value));
            playerIcon.UpdateBattleItem(CharactersList.Instance.GetBattleItemByID(int.Parse(playerIcon.AssignedPlayer.Data["BattleItem"].Value)));
        }

        foreach (var playerIcon in draftPlayerHolderOrange.PlayerIcons)
        {
            playerIcon.UpdateIconState(DraftPlayerState.Confirmed);
            playerIcon.UpdateSelectedCharacter(CharactersList.Instance.GetCharacterFromString(playerIcon.AssignedPlayer.Data["SelectedCharacter"].Value));
            playerIcon.UpdateBattleItem(CharactersList.Instance.GetBattleItemByID(int.Parse(playerIcon.AssignedPlayer.Data["BattleItem"].Value)));
        }
    }

    public void UpdateTimerValue(float time)
    {
        draftTimerUI.UpdateTimer(time);
    }
}
