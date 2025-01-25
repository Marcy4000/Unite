using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class DraftSwitchController : NetworkBehaviour
{
    [SerializeField] private DraftSwitchPanelUI switchPanelUI;
    [SerializeField] private DraftCharacterSelector characterSelector;

    private List<DraftPlayerIcon> draftPlayerIcons = new List<DraftPlayerIcon>();

    private string currentRequestSenderID = null;
    private short currentRequestCharacterID;

    private string currentRequestReceiverID = null;

    public event System.Action<short> OnCharacterChanged;

    private void Awake()
    {
        switchPanelUI.OnConfirm += OnConfirm;
        switchPanelUI.OnReject += OnReject;
    }

    public void Initialize(List<DraftPlayerIcon> draftPlayerIcons)
    {
        this.draftPlayerIcons = draftPlayerIcons;

        foreach (DraftPlayerIcon playerIcon in draftPlayerIcons)
        {
            playerIcon.OnRequestSwitch += OnRequestSwitch;
            playerIcon.OnConfirmSwitch += OnConfirmSwitch;
        }
    }

    public void SetPrepPhaseSwitch(bool enable)
    {
        foreach (DraftPlayerIcon playerIcon in draftPlayerIcons)
        {
            if (playerIcon.AssignedPlayer.Id == LobbyController.Instance.Player.Id)
            {
                continue;
            }

            playerIcon.SetRequestButton(enable);
        }
    }

    private void OnConfirm()
    {
        if (string.IsNullOrEmpty(currentRequestSenderID))
        {
            return;
        }

        if (ulong.TryParse(LobbyController.Instance.Lobby.Players.Find(p => p.Id == currentRequestSenderID).Data["OwnerID"].Value, out ulong ownerID))
        {
            SendSwitchResponseRPC(true, NumberEncoder.FromBase64<short>(LobbyController.Instance.Player.Data["SelectedCharacter"].Value), RpcTarget.Single(ownerID, RpcTargetUse.Temp));
        }

        ChangeCharacter(currentRequestCharacterID);

        switchPanelUI.HideSwitchPanel();

        currentRequestReceiverID = null;
        currentRequestSenderID = null;
        currentRequestCharacterID = -1;
    }

    private void OnReject()
    {
        if (!string.IsNullOrEmpty(currentRequestReceiverID))
        {
            if (ulong.TryParse(LobbyController.Instance.Lobby.Players.Find(p => p.Id == currentRequestReceiverID).Data["OwnerID"].Value, out ulong receiverID))
            {
                CancelSwitchRequestRPC(RpcTarget.Single(receiverID, RpcTargetUse.Temp));
                switchPanelUI.HideSwitchPanel();

                currentRequestReceiverID = null;
            }
        }
        else if (!string.IsNullOrEmpty(currentRequestSenderID))
        {
            if (ulong.TryParse(LobbyController.Instance.Lobby.Players.Find(p => p.Id == currentRequestSenderID).Data["OwnerID"].Value, out ulong senderID))
            {
                SendSwitchResponseRPC(false, 0, RpcTarget.Single(senderID, RpcTargetUse.Temp));
                currentRequestSenderID = null;
                currentRequestCharacterID = -1;

                switchPanelUI.HideSwitchPanel();
            }
        }
        else
        {
            switchPanelUI.HideSwitchPanel();

            currentRequestSenderID = null;
            currentRequestReceiverID = null;
            currentRequestCharacterID = -1;
        }
    }

    private void OnRequestSwitch(DraftPlayerIcon playerIcon)
    {
        if (playerIcon.AssignedPlayer.Id == LobbyController.Instance.Player.Id)
        {
            return;
        }

        currentRequestReceiverID = playerIcon.AssignedPlayer.Id;

        if (ulong.TryParse(playerIcon.AssignedPlayer.Data["OwnerID"].Value, out ulong ownerID))
        {
            SendSwitchRequestRPC(LobbyController.Instance.Player.Id, CharactersList.Instance.GetCharacterID(characterSelector.HoveredCharacter), RpcTarget.Single(ownerID, RpcTargetUse.Temp));
            switchPanelUI.ShowSwitchPanel(playerIcon.AssignedPlayer.Id, NumberEncoder.FromBase64<short>(playerIcon.AssignedPlayer.Data["SelectedCharacter"].Value), false);
        }
    }

    private void OnConfirmSwitch(DraftPlayerIcon playerIcon)
    {
        foreach (DraftPlayerIcon icon in draftPlayerIcons)
        {
            if (icon == playerIcon)
            {
                continue;
            }
            icon.SetRequestButton(true);
        }
    }

    public void HideSwitchPanel()
    {
        switchPanelUI.HideSwitchPanel();
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SendSwitchRequestRPC(string senderID, short characterID, RpcParams rpcParams = default)
    {
        if (switchPanelUI.IsSelecting)
        {
            return;
        }

        currentRequestSenderID = senderID;
        currentRequestCharacterID = characterID;

        switchPanelUI.ShowSwitchPanel(senderID, characterID, true);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SendSwitchResponseRPC(bool accept, short newCharID, RpcParams rpcParams = default)
    {
        if (accept)
        {
            ChangeCharacter(newCharID);
        }

        switchPanelUI.HideSwitchPanel();
        currentRequestReceiverID = null;
        currentRequestSenderID = null;
        currentRequestCharacterID = -1;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void CancelSwitchRequestRPC(RpcParams rpcParams = default)
    {
        currentRequestCharacterID = -1;
        currentRequestSenderID = null;

        switchPanelUI.HideSwitchPanel();
    }

    private void ChangeCharacter(short characterID)
    {
        LobbyController.Instance.ChangePlayerCharacter(characterID);
        OnCharacterChanged?.Invoke(characterID);
    }
}

