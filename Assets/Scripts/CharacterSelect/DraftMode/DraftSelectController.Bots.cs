using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public partial class DraftSelectController
{
    private readonly Dictionary<string, short> committedDraftPicks = new();
    private readonly Dictionary<Team, Queue<short>> botBanRequests = new();
    private int botTurn = -1;
    private DraftPhase botPhase;
    private float botActAt;
    private bool botFastPicking;

    private bool HasUncommittedHumanPicks() => blueTeamPlayers.Concat(orangeTeamPlayers)
        .Any(player => !player.IsBot && !committedDraftPicks.ContainsKey(player.Id));

    private void ScheduleBotPick()
    {
        botFastPicking = !HasUncommittedHumanPicks();
        botActAt = Time.unscaledTime + Unite.Multiplayer.DraftTurnRules.BotDelaySeconds(
            false, !botFastPicking, Random.Range(-1f, 1f));
    }

    private HashSet<short> UnavailableDraftPicks()
    {
        var unavailable = new HashSet<short>(committedDraftPicks.Values);
        foreach (short ban in bannedCharacters) if (ban >= 0) unavailable.Add(ban);
        return unavailable;
    }

    private void TickDraftBots()
    {
        if (!IsServer || LobbyController.Instance.BotRoster?.HasBots != true ||
            (currentDraftPhase.Value != DraftPhase.Banning && currentDraftPhase.Value != DraftPhase.Picking)) return;
        if (botTurn != currentTurnIndex.Value || botPhase != currentDraftPhase.Value)
        {
            botTurn = currentTurnIndex.Value; botPhase = currentDraftPhase.Value;
            var team = Unite.Multiplayer.DraftTurnRules.IsBlueTurn(botTurn) ? blueTeamPlayers : orangeTeamPlayers;
            bool teamHasUncommittedHumans = team.Any(player => !player.IsBot && !committedDraftPicks.ContainsKey(player.Id));
            if (botPhase == DraftPhase.Picking) ScheduleBotPick();
            else botActAt = Time.unscaledTime + Unite.Multiplayer.DraftTurnRules.BotDelaySeconds(
                true, teamHasUncommittedHumans, Random.Range(-1f, 1f));
        }
        if (botPhase == DraftPhase.Picking && !botFastPicking && !HasUncommittedHumanPicks())
        {
            float previousDeadline = botActAt;
            ScheduleBotPick();
            botActAt = Mathf.Min(previousDeadline, botActAt);
        }
        foreach (string id in GetCurrentPlayerIDs().ToArray())
        {
            var player = LobbyController.Instance.GetPlayerByID(id);
            int index = GetPlayerIndex(id);
            if (player?.IsBot != true || index < 0 || playerStates[index] == (byte)DraftPlayerState.Confirmed ||
                playerStates[index] == (byte)DraftPlayerState.BanningConfirmed) continue;
            if (currentDraftPhase.Value == DraftPhase.Banning)
            {
                var team = TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value);
                if (botBanRequests.TryGetValue(team, out var requests))
                    while (requests.Count > 0)
                    {
                        short ban = requests.Dequeue();
                        if (UnavailableDraftPicks().Contains(ban)) continue;
                        ConfirmSelectionRPC(id, ban); return;
                    }
                if (Time.unscaledTime >= botActAt) { ConfirmSelectionRPC(id, -1); return; }
                continue;
            }
            bool humanStillPicking = GetCurrentPlayerIDs().Any(other =>
            {
                int otherIndex = GetPlayerIndex(other);
                return LobbyController.Instance.GetPlayerByID(other)?.IsBot != true &&
                    (otherIndex < 0 || playerStates[otherIndex] != (byte)DraftPlayerState.Confirmed);
            });
            if (!Unite.Multiplayer.DraftTurnRules.CanAutoPick(humanStillPicking, Time.unscaledTime, botActAt)) continue;
            short pick = LobbyController.Instance.BotRoster.ChooseDraftCharacter(UnavailableDraftPicks());
            if (pick < 0) { AbortExhaustedBotDraft(); return; }
            ConfirmSelectionRPC(id, pick);
            ScheduleBotPick();
            return;
        }
    }

    private void AbortExhaustedBotDraft()
    {
        Debug.LogError("Draft exhausted the configured bot-compatible Pokemon pool. Add more compatible loadouts or use fewer bots/bans.");
        currentDraftPhase.Value = DraftPhase.Waiting;
        LobbyController.Instance.ReportSessionFailure(Unite.Multiplayer.SessionFailureReason.ContentLoadFailed);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitBanRequestRpc(short character, RpcParams rpcParams = default)
    {
        if (currentDraftPhase.Value != DraftPhase.Banning || character < 0 || character >= CharactersList.Instance.Characters.Length ||
            CharactersList.Instance.GetCharacterFromID(character).Hidden || UnavailableDraftPicks().Contains(character)) return;
        var coordinator = LobbyController.Instance.PlayerNetworkManagers.Find(manager => manager != null && manager.OwnerClientId == rpcParams.Receive.SenderClientId);
        var human = coordinator?.LocalPlayer;
        if (human == null || human.IsBot) return;
        var team = TeamMember.GetTeamFromString(human.Data["PlayerTeam"].Value);
        if (!GetCurrentPlayerIDs().Any(id => LobbyController.Instance.GetPlayerByID(id)?.Data["PlayerTeam"].Value == team.ToString())) return;
        if (!botBanRequests.TryGetValue(team, out var queue)) botBanRequests[team] = queue = new Queue<short>();
        if (!queue.Contains(character) && queue.Count < 4) queue.Enqueue(character);
        ShowRequestRPC(human.Id, character, true);
    }
}
