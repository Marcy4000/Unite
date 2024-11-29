using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class SurrenderManager : NetworkBehaviour
{
    [SerializeField] private GameObject surrenderPanel;
    [SerializeField] private SurrenderPanelUI surrenderPanelUI;

    private Team localTeam;
    private Player[] surrenderTeam;
    private Dictionary<string, byte> playerVotes = new Dictionary<string, byte>();

    private bool isSurrendering;
    private bool hasVoted;

    private Team currentTeamVoting;

    private NetworkVariable<float> voteTimer = new NetworkVariable<float>(10f);

    public bool IsSurrendering => isSurrendering;

    public event Action<Team, bool> onSurrenderVoteResult; // (team, result)

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        surrenderPanelUI = surrenderPanel.GetComponent<SurrenderPanelUI>();
        localTeam = LobbyController.Instance.GetLocalPlayerTeam();
        UpdateSurrenderingTeam(localTeam);

        surrenderPanel.SetActive(false);
    }

    private void UpdateSurrenderingTeam(Team orangeTeam)
    {
        surrenderTeam = LobbyController.Instance.GetTeamPlayers(orangeTeam);
        playerVotes.Clear();

        foreach (var player in surrenderTeam)
        {
            playerVotes.Add(player.Id, 0);
        }
    }

    private void Update()
    {
        surrenderPanelUI.TimeBarImage.fillAmount = voteTimer.Value / 10f;

        if (!IsServer)
        {
            return;
        }

        if (isSurrendering)
        {
            voteTimer.Value -= Time.deltaTime;

            if (voteTimer.Value <= 0f)
            {
                OnSurrenderVoteEnd();
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void StartSurrenderVoteRPC(Team orangeTeam)
    {
        if (isSurrendering)
        {
            return;
        }

        isSurrendering = true;
        UpdateSurrenderingTeam(orangeTeam);
        List<string> keys = playerVotes.Keys.ToList();
        foreach (var player in keys)
        {
            playerVotes[player] = 0;
        }

        voteTimer.Value = 10f;

        currentTeamVoting = orangeTeam;

        ShowSurrenderPanelRPC(orangeTeam);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowSurrenderPanelRPC(Team orangeTeam)
    {
        if (orangeTeam != localTeam)
        {
            return;
        }

        hasVoted = false;
        surrenderPanel.SetActive(true);
        List<string> teamPlayers = new List<string>();
        foreach (var player in this.surrenderTeam)
        {
            teamPlayers.Add(player.Id);
        }
        surrenderPanelUI.InitializeUI(teamPlayers.ToArray());
    }

    public void SendVoteToServer(bool vote)
    {
        if (hasVoted)
        {
            return;
        }

        surrenderPanelUI.ButtonsHolder.SetActive(false);
        hasVoted = true;
        SendVoteRPC(LobbyController.Instance.Player.Id, vote);
    }

    [Rpc(SendTo.Server)]
    private void SendVoteRPC(string playerId, bool vote)
    {
        if (!playerVotes.ContainsKey(playerId) || playerVotes[playerId] != 0)
        {
            return; // Already voted or invalid player ID
        }

        playerVotes[playerId] = vote ? (byte)1 : (byte)2;
        UpdateVoteIndicatorRPC(playerId, playerVotes[playerId]);
        CheckSurrenderVoteResult();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void UpdateVoteIndicatorRPC(string playerId, byte vote)
    {
        surrenderPanelUI.UpdateVoteIndicator(playerId, vote);
    }

    private void CheckSurrenderVoteResult()
    {
        int yesVotes = 0;
        int noVotes = 0;
        int totalVotes = 0;

        foreach (var vote in playerVotes.Values)
        {
            if (vote == 1)
            {
                yesVotes++;
            }
            else if (vote == 2)
            {
                noVotes++;
            }

            if (vote != 0)
            {
                totalVotes++;
            }
        }

        int requiredVotes = surrenderTeam.Length / 2 + 1;

        // If more than half voted yes, surrender
        if (yesVotes >= requiredVotes)
        {
            OnSurrenderVoteEnd(true);
        }
        // If more than half voted no, do not surrender
        else if (noVotes >= requiredVotes)
        {
            OnSurrenderVoteEnd(false);
        }
        // If not enough votes were cast when time runs out, cancel the surrender vote
        else if (voteTimer.Value <= 0f && totalVotes < requiredVotes)
        {
            OnSurrenderVoteEnd(null);
        }
    }

    private void OnSurrenderVoteEnd(bool? result = null)
    {
        isSurrendering = false;
        HideSurrenderPanelRPC();

        if (result == null)
        {
            // Cancel the vote
            onSurrenderVoteResult?.Invoke(currentTeamVoting, false);
        }
        else
        {
            bool surrenderResult = result.Value;
            onSurrenderVoteResult?.Invoke(currentTeamVoting, surrenderResult);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void HideSurrenderPanelRPC()
    {
        StartCoroutine(HideSurrenderPanel());
    }

    private IEnumerator HideSurrenderPanel()
    {
        yield return new WaitForSeconds(3f);
        surrenderPanel.SetActive(false);
    }
}
