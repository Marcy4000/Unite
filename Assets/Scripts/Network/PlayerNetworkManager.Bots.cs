using System;
using System.Collections.Generic;
using System.Linq;
using Unite.Multiplayer;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

// The host's existing persistent player coordinator owns this roster. Bots are
// ordinary participants, never NGO clients, UGS members, or extra PlayerObjects.
public partial class PlayerNetworkManager
{
    private NetworkList<BotParticipant> bots = new NetworkList<BotParticipant>();
    private NetworkList<ParticipantCharacter> participantCharacters = new NetworkList<ParticipantCharacter>();
    private NetworkVariable<bool> botSelectionActive = new NetworkVariable<bool>();
    private readonly Dictionary<string, short> botPreferences = new Dictionary<string, short>();
    private readonly Dictionary<string, string> matchBotLanes = new Dictionary<string, string>();
    private readonly Dictionary<string, short> committedCharacters = new Dictionary<string, short>();
    private HashSet<string> previousHumanIds;
    private string previousMap;
    private int nextTrainerNumber = 1;
    private readonly System.Random botRandom = new System.Random();
    private bool participantViewDirty;
    private bool charactersPublished;
    private bool charactersFrozen;
    private bool composingParticipants;

    public bool IsBotRoster => IsSpawned && OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;
    public bool HasBots => IsBotRoster && bots.Count > 0;
    public bool UsesBotSelection => IsBotRoster && botSelectionActive.Value;
    public bool SupportsBots
    {
        get
        {
            var lobby = LobbyController.Instance;
            if (lobby?.Lobby == null || CharactersList.Instance == null) return false;
            var map = CharactersList.Instance.GetCurrentLobbyMap();
            return lobby.IsCustomLobby() && map != null && BotModeRules.Supports(map.sceneName, map.availableTeams.Count, map.maxTeamSize) &&
                (BotModeRules.ForScene(map.sceneName) != BotGameMode.Lanes || map.SupportsLaneSelection) &&
                (map.characterSelectType == CharacterSelectType.PsyduckRacing || SupportedBotCharacters().Length >= map.maxTeamSize);
        }
    }

    private void InitializeBotRoster()
    {
        bots.OnListChanged += OnBotsChanged;
        participantCharacters.OnListChanged += OnCharactersChanged;
        botSelectionActive.OnValueChanged += OnBotSelectionChanged;
        participantViewDirty = true;
    }

    private void UnsubscribeBotRoster()
    {
        bots.OnListChanged -= OnBotsChanged;
        participantCharacters.OnListChanged -= OnCharactersChanged;
        botSelectionActive.OnValueChanged -= OnBotSelectionChanged;
    }

    private void OnBotsChanged(NetworkListEvent<BotParticipant> change) => participantViewDirty = true;
    private void OnCharactersChanged(NetworkListEvent<ParticipantCharacter> change) => participantViewDirty = true;
    private void OnBotSelectionChanged(bool before, bool after) => participantViewDirty = true;

    private void TickBotRoster()
    {
        if (!IsBotRoster || !participantViewDirty || LobbyController.Instance?.Lobby == null) return;
        participantViewDirty = false;
        LobbyController.Instance.RefreshParticipants();
    }

    public void ComposeParticipants(GameSessionView view)
    {
        if (!IsBotRoster || composingParticipants) return;
        composingParticipants = true;
        try
        {
            var humans = view.Players.Where(player => !player.IsBot).ToArray();
            var ids = new HashSet<string>(humans.Select(player => player.Id));
            string map = view.Data.TryGetValue("SelectedMap", out var selectedMap) ? selectedMap.Value : string.Empty;
            bool inLobby = SceneManager.GetActiveScene().name == "LobbyScene";
            if (IsServer && inLobby)
            {
                if (!SupportsBots || (previousHumanIds != null && ids.Except(previousHumanIds).Any()) ||
                    (previousMap != null && previousMap != map)) ClearBots();
                // Removal does not clear bots. Update the membership basis instead.
                ulong basis = BotAllocationRules.MembershipHash(ids);
                for (int i = 0; i < bots.Count; i++)
                {
                    var bot = bots[i];
                    if (bot.Membership == basis) continue;
                    bot.Membership = basis;
                    bots[i] = bot;
                }
                if (botSelectionActive.Value)
                {
                    botSelectionActive.Value = false;
                    participantCharacters.Clear();
                }
            }
            previousHumanIds = ids;
            previousMap = map;
            ulong membership = BotAllocationRules.MembershipHash(ids);
            // Draft humans retain the existing session-owned swap flow during
            // preparation. Bot snapshots must not mask those later human swaps.
            bool overlayHumans = CharactersList.Instance.GetCurrentLobbyMap()?.characterSelectType != CharacterSelectType.Draft;
            foreach (var bot in bots)
            {
                // A joining client can see the old NGO snapshot before the host's
                // clear arrives. Never display that roster against a new human set.
                if (inLobby && bot.Membership != membership) continue;
                var player = new GameSessionPlayer(bot.Id.ToString(), true);
                player.Data["PlayerName"] = new PlayerProperty($"Trainer {bot.Number}");
                player.Data["PlayerTeam"] = new PlayerProperty(bot.Team.ToString());
                player.Data["PlayerPos"] = new PlayerProperty(NumberEncoder.ToBase64(bot.Position));
                player.Data["SelectedCharacter"] = new PlayerProperty(NumberEncoder.ToBase64(bot.Character));
                var build = BotRosterConfig.Instance != null ? BotRosterConfig.Instance.GetBuild(bot.Loadout) : null;
                bool racing = BotModeRules.ForScene(CharactersList.Instance.GetCurrentLobbyMap().sceneName) == BotGameMode.Racing;
                player.Data["BattleItem"] = new PlayerProperty(racing ? "0" : build != null ? System.Array.IndexOf(CharactersList.Instance.BattleItems, build.battleItem).ToString() : "1");
                player.Data["HeldItems"] = new PlayerProperty(HeldItemDatabase.SerializeHeldItems(build != null ? build.heldItems : new byte[] { 0, 0, 0 }));
                player.Data["ClothingInfo"] = new PlayerProperty(bot.Outfit.Length > 0 ? bot.Outfit.ToString() : "AAAAAAAAAAAASjMJcj8FAAAAAAJqASkBZwA=");
                player.Data["PlayerRank"] = new PlayerProperty(PlayerRankData.GetDefault().Serialize());
                view.Players.Add(player);
            }
            if (UsesBotSelection)
                foreach (var choice in participantCharacters)
                {
                    var player = view.Players.Find(value => value.Id == choice.Id.ToString());
                    if (player != null && (player.IsBot || overlayHumans)) player.Data["SelectedCharacter"] = new PlayerProperty(NumberEncoder.ToBase64(choice.Character));
                }
            if (!inLobby)
                foreach (var pair in committedCharacters)
                {
                    var player = view.Players.Find(value => value.Id == pair.Key);
                    if (player != null && (player.IsBot || overlayHumans)) player.Data["SelectedCharacter"] = new PlayerProperty(NumberEncoder.ToBase64(pair.Value));
                }
        }
        finally { composingParticipants = false; }
    }

    public void FillEmptySlotsWithBots()
    {
        var lobby = LobbyController.Instance;
        if (!IsServer || !IsBotRoster || !lobby.CanManageBots) return;
        lobby.RefreshParticipants();
        var map = CharactersList.Instance.GetCurrentLobbyMap();
        var humans = lobby.Lobby.Players.Where(player => !player.IsBot).ToArray();
        ulong membership = BotAllocationRules.MembershipHash(humans.Select(player => player.Id));
        foreach (var team in map.availableTeams)
            for (short position = 0; position < map.maxTeamSize; position++)
            {
                if (humans.Any(player => player.Data["PlayerTeam"].Value == team.ToString() &&
                    NumberEncoder.FromBase64<short>(player.Data["PlayerPos"].Value) == position) ||
                    BotEntries().Any(bot => bot.Team == team && bot.Position == position)) continue;
                int number = 1;
                while (BotEntries().Any(entry => entry.Number == number)) number++;
                string outfit = "AAAAAAAAAAAASjMJcj8FAAAAAAJqASkBZwA=";
                var outfits = BotRosterConfig.Instance != null ? BotRosterConfig.Instance.trainerOutfits : null;
                if (outfits != null && outfits.Length > 0)
                {
                    string candidate = outfits[botRandom.Next(outfits.Length)];
                    try { PlayerClothesInfo.Deserialize(candidate); if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= 120) outfit = candidate; }
                    catch (System.Exception) { /* Malformed optional outfits use the known-valid default. */ }
                }
                bots.Add(new BotParticipant { Id = $"bot-{nextTrainerNumber++}", Number = number, Team = team,
                    Position = position, Character = -1, Loadout = -1, Outfit = outfit, Membership = membership });
            }
        lobby.RefreshParticipants();
    }

    public bool ValidateBotRosterForStart()
    {
        if (!HasBots) return true;
        if (!SupportsBots) return false;
        var players = LobbyController.Instance.Lobby.Players;
        var map = CharactersList.Instance.GetCurrentLobbyMap();
        return players.Count <= map.maxTeamSize * map.availableTeams.Count && players.All(player =>
            map.availableTeams.Contains(TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value)) &&
            NumberEncoder.FromBase64<short>(player.Data["PlayerPos"].Value) >= 0 &&
            NumberEncoder.FromBase64<short>(player.Data["PlayerPos"].Value) < map.maxTeamSize) &&
            players.Select(player => player.Data["PlayerTeam"].Value + ":" + player.Data["PlayerPos"].Value).Distinct().Count() == players.Count;
    }

    public void RemoveBot(string id)
    {
        if (!IsServer || !IsBotRoster) return;
        for (int i = bots.Count - 1; i >= 0; i--) if (bots[i].Id.ToString() == id) bots.RemoveAt(i);
        LobbyController.Instance.RefreshParticipants();
    }

    public void ClearBots()
    {
        if (!IsServer || !IsBotRoster) return;
        bots.Clear();
        botPreferences.Clear();
        matchBotLanes.Clear();
    }

    private List<BotParticipant> BotEntries()
    {
        var result = new List<BotParticipant>();
        foreach (var bot in bots) result.Add(bot);
        return result;
    }

    public short[] SupportedBotCharacters()
    {
        if (BotRosterConfig.Instance != null) return BotRosterConfig.Instance.CharacterPool();
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Absol", "Cinderace", "Emboar", "Flareon", "Flygon" };
        return CharactersList.Instance.Characters.Where(character => character != null && !character.Hidden &&
            supported.Contains(character.pokemonName)).Select(CharactersList.Instance.GetCharacterID).ToArray();
    }

    public void BeginBotCharacterSelection()
    {
        if (!IsServer || !HasBots) return;
        ResetCommittedCharactersRpc();
        participantCharacters.Clear();
        charactersPublished = charactersFrozen = false;
        botPreferences.Clear();
        matchBotLanes.Clear();
        var pool = SupportedBotCharacters();
        foreach (var player in LobbyController.Instance.Lobby.Players)
        {
            participantCharacters.Add(new ParticipantCharacter { Id = player.Id, Character = -1 });
            if (player.IsBot)
            {
                botPreferences[player.Id] = pool[botRandom.Next(pool.Length)];
            }
        }
        botSelectionActive.Value = true;
        ResolveBotCharacters(false);
        LobbyController.Instance.RefreshParticipants();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SelectParticipantCharacterRpc(short character, RpcParams rpcParams = default)
    {
        if (!UsesBotSelection || charactersFrozen || SceneManager.GetActiveScene().name != "CharacterSelect") return;
        var manager = LobbyController.Instance.PlayerNetworkManagers.Find(value => value != null && value.IsSpawned &&
            value.OwnerClientId == rpcParams.Receive.SenderClientId);
        var player = manager?.LocalPlayer;
        if (player == null || player.IsBot || character < 0 || character >= CharactersList.Instance.Characters.Length ||
            CharactersList.Instance.GetCharacterFromID(character).Hidden) return;
        var teammates = LobbyController.Instance.GetTeamPlayers(TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value));
        if (teammates.Any(teammate => !teammate.IsBot && teammate.Id != player.Id && GetSelectedCharacter(teammate.Id) == character)) return;
        SetParticipantCharacter(player.Id, character);
        ResolveBotCharacters(charactersPublished);
    }

    private short GetSelectedCharacter(string id)
    {
        foreach (var choice in participantCharacters) if (choice.Id.ToString() == id) return choice.Character;
        return -1;
    }

    private void SetParticipantCharacter(string id, short character)
    {
        for (int i = 0; i < participantCharacters.Count; i++)
        {
            var choice = participantCharacters[i];
            if (choice.Id.ToString() != id || choice.Character == character) continue;
            choice.Character = character;
            participantCharacters[i] = choice;
            break;
        }
    }

    public void ResolveBotCharacters(bool publish)
    {
        if (!IsServer || !UsesBotSelection || charactersFrozen) return;
        charactersPublished |= publish;
        foreach (var team in CharactersList.Instance.GetCurrentLobbyMap().availableTeams)
        {
            var players = LobbyController.Instance.GetTeamPlayers(team);
            var preferences = players.Where(player => player.IsBot).ToDictionary(player => player.Id, player => botPreferences[player.Id]);
            var allocated = BotAllocationRules.AllocateCharacters(SupportedBotCharacters(),
                players.Where(player => !player.IsBot).Select(player => GetSelectedCharacter(player.Id)), preferences);
            foreach (var pair in allocated)
            {
                botPreferences[pair.Key] = pair.Value;
                if (charactersPublished) SetParticipantCharacter(pair.Key, pair.Value);
            }
        }
    }

    public void FreezeBotCharacters()
    {
        if (!IsServer || !UsesBotSelection || charactersFrozen) return;
        foreach (var team in CharactersList.Instance.GetCurrentLobbyMap().availableTeams)
        {
            var players = LobbyController.Instance.GetTeamPlayers(team);
            var used = new HashSet<short>(players.Where(player => !player.IsBot).Select(player => GetSelectedCharacter(player.Id)));
            var available = CharactersList.Instance.Characters.Where(character => !character.Hidden)
                .Select(CharactersList.Instance.GetCharacterID).Where(id => !used.Contains(id)).ToList();
            foreach (var player in players.Where(player => !player.IsBot && GetSelectedCharacter(player.Id) < 0))
            {
                if (available.Count == 0) throw new InvalidOperationException("No Pokemon available for an unselected human.");
                SetParticipantCharacter(player.Id, available[0]);
                available.RemoveAt(0);
            }
        }
        ResolveBotCharacters(true);
        charactersFrozen = true;
        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            bot.Character = GetSelectedCharacter(bot.Id.ToString());
            bot.Loadout = BotRosterConfig.Instance != null ? BotRosterConfig.Instance.ChooseBuild(bot.Character, botRandom) : (short)-1;
            bots[i] = bot;
        }
        var snapshot = new List<ParticipantCharacter>();
        foreach (var choice in participantCharacters) snapshot.Add(choice);
        CommitCharactersRpc(snapshot.ToArray());
        LobbyController.Instance.RefreshParticipants();
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void ResetCommittedCharactersRpc() => committedCharacters.Clear();

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void CommitCharactersRpc(ParticipantCharacter[] snapshot)
    {
        committedCharacters.Clear();
        foreach (var choice in snapshot) committedCharacters[choice.Id.ToString()] = choice.Character;
        LobbyController.Instance.RefreshParticipants();
        // A human may join the unlocked custom lobby while results are still
        // open, clearing bots there. Keep final match identity/loadout snapshots.
        LobbyController.Instance.CaptureMatchParticipants();
    }

    public void SpawnBotsForMatch()
    {
        if (!IsServer || !HasBots) return;
        foreach (var bot in bots)
        {
            if (FindObjectsByType<PlayerManager>(FindObjectsSortMode.None).Any(player => player.IsBot && player.LobbyPlayer?.Id == bot.Id.ToString())) continue;
            Transform spawnpoint = SpawnpointManager.Instance.GetSpawnpoint(bot.Team, bot.Position);
            GameObject avatar = Instantiate(playerPrefab, spawnpoint.position, spawnpoint.rotation);
            avatar.GetComponent<PlayerManager>().ConfigureBotBeforeSpawn(bot.Id.ToString(), bot.Team, bot.Character);
            avatar.GetComponent<NetworkObject>().Spawn(true);
        }
    }

    public void SaveBotLane(string id, string lane)
    {
        if (IsServer && IsBotRoster) matchBotLanes[id] = lane;
    }

    public string GetBotLane(string id) => matchBotLanes.TryGetValue(id, out var lane) ? lane : string.Empty;

    public BotLoadout GetBotLoadout(string id)
    {
        foreach (var bot in bots) if (bot.Id.ToString() == id) return BotRosterConfig.Instance != null ? BotRosterConfig.Instance.GetBuild(bot.Loadout) : null;
        return null;
    }

    public void BeginBotDraft()
    {
        if (!IsServer || !HasBots) return;
        charactersPublished = false; charactersFrozen = true;
        ResetCommittedCharactersRpc();
        matchBotLanes.Clear();
        participantCharacters.Clear();
        foreach (var player in LobbyController.Instance.Lobby.Players)
            participantCharacters.Add(new ParticipantCharacter { Id = player.Id, Character = -1 });
        // Draft has its own authenticated confirmation flow. Enabling the
        // blind-pick arbitrator here would swallow human session writes/swaps.
        botSelectionActive.Value = false;
    }

    public void PrepareRacingBots(CharacterInfo character)
    {
        if (!IsServer || !HasBots) return;
        ResetCommittedCharactersRpc(); participantCharacters.Clear(); matchBotLanes.Clear();
        botSelectionActive.Value = false; charactersFrozen = true;
        short id = CharactersList.Instance.GetCharacterID(character);
        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i]; bot.Character = id; bot.Loadout = -1; bots[i] = bot;
            participantCharacters.Add(new ParticipantCharacter { Id = bot.Id, Character = id });
        }
        var snapshot = new List<ParticipantCharacter>();
        foreach (var entry in participantCharacters) snapshot.Add(entry);
        CommitCharactersRpc(snapshot.ToArray());
    }

    public void CommitDraftPick(string id, short character)
    {
        if (!IsServer) return;
        SetParticipantCharacter(id, character);
        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            if (bot.Id.ToString() != id) continue;
            bot.Character = character;
            bot.Loadout = BotRosterConfig.Instance != null ? BotRosterConfig.Instance.ChooseBuild(character, botRandom) : (short)-1;
            bots[i] = bot;
        }
        var snapshot = new List<ParticipantCharacter>();
        foreach (var pick in participantCharacters) snapshot.Add(pick);
        CommitCharactersRpc(snapshot.ToArray());
    }

    public short ChooseDraftCharacter(ISet<short> unavailable)
    {
        var available = SupportedBotCharacters().Where(id => !unavailable.Contains(id)).ToArray();
        return available.Length > 0 ? available[botRandom.Next(available.Length)] : (short)-1;
    }
}

public struct BotParticipant : INetworkSerializable, IEquatable<BotParticipant>
{
    public FixedString32Bytes Id;
    public int Number;
    public Team Team;
    public short Position;
    public short Character;
    public short Loadout;
    public FixedString128Bytes Outfit;
    public ulong Membership;
    public bool Equals(BotParticipant other) => Id == other.Id && Number == other.Number && Team == other.Team &&
        Position == other.Position && Character == other.Character && Loadout == other.Loadout && Outfit == other.Outfit && Membership == other.Membership;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref Number);
        serializer.SerializeValue(ref Team);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Character);
        serializer.SerializeValue(ref Loadout);
        serializer.SerializeValue(ref Outfit);
        serializer.SerializeValue(ref Membership);
    }
}

public struct ParticipantCharacter : INetworkSerializable, IEquatable<ParticipantCharacter>
{
    public FixedString32Bytes Id;
    public short Character;
    public bool Equals(ParticipantCharacter other) => Id == other.Id && Character == other.Character;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref Character);
    }
}
