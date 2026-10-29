using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unite.Multiplayer;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class DraftUIStateTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> assets = new();
    private readonly Dictionary<string, object> previousInstances = new();
    private GameObject root;
    private Component draft;
    private Component selector;
    private GameSessionPlayer localPlayer;
    private GameSessionPlayer otherPlayer;
    private NetworkList<FixedString32Bytes> playerIDs;
    private NetworkList<byte> playerStates;
    private NetworkList<short> bans;
    private object confirmedCharacter;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);

    private Component Add(string name, string type)
    {
        var node = new GameObject(name);
        node.SetActive(false);
        node.transform.SetParent(root.transform);
        return node.AddComponent(GameType(type));
    }

    private Component Singleton(string type)
    {
        var property = GameType(type).GetProperty("Instance");
        previousInstances.Add(type, property.GetValue(null));
        var component = Add(type, type);
        property.SetValue(null, component);
        return component;
    }

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Draft UI state tests");
        root.SetActive(false);
        var lobby = Singleton("LobbyController");
        localPlayer = new GameSessionPlayer("local");
        localPlayer.Data["PlayerTeam"] = new PlayerProperty("Blue");
        localPlayer.Data["SelectedCharacter"] = new PlayerProperty("//8=");
        otherPlayer = new GameSessionPlayer("other");
        otherPlayer.Data["SelectedCharacter"] = new PlayerProperty("//8=");
        var session = new GameSessionView();
        session.Players.AddRange(new[] { localPlayer, otherPlayer });
        Set(lobby, "localPlayer", localPlayer);
        Set(lobby, "partyLobby", session);

        var characters = Singleton("CharactersList");
        var infos = Array.CreateInstance(GameType("CharacterInfo"), 2);
        for (int index = 0; index < infos.Length; index++)
        {
            var info = ScriptableObject.CreateInstance(GameType("CharacterInfo"));
            assets.Add(info);
            infos.SetValue(info, index);
        }
        Set(characters, "characters", infos);

        draft = Singleton("DraftSelectController");
        playerIDs = new NetworkList<FixedString32Bytes>();
        playerStates = new NetworkList<byte>();
        bans = new NetworkList<short>();
        Set(draft, "playerIDs", playerIDs);
        Set(draft, "playerStates", playerStates);
        Set(draft, "bannedCharacters", bans);
        selector = Add("Selector", "DraftCharacterSelector");
        Set(draft, "draftCharacterSelector", selector);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var previous in previousInstances)
            GameType(previous.Key).GetProperty("Instance").SetValue(null, previous.Value);
        previousInstances.Clear();
        Object.DestroyImmediate(root);
        playerIDs?.Dispose();
        playerStates?.Dispose();
        bans?.Dispose();
        foreach (var asset in assets) Object.DestroyImmediate(asset);
        assets.Clear();
        confirmedCharacter = null;
    }

    private IList InitializeSelector()
    {
        Set(selector, "holder", selector.transform);
        Set(selector, "iconPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/CharacterSelect/DraftMode/DraftSelectCharacterIcon.prefab"));
        var group = Add("Toggle group", "UnityEngine.UI.ToggleGroup");
        SetProperty(group, "allowSwitchOff", true);
        Set(selector, "toggleGroup", group);
        Set(selector, "categoryDropdown", Add("Category", "TMPro.TMP_Dropdown"));
        Call(selector, "InitializeUI");
        var selectedEvent = selector.GetType().GetEvent("OnCharacterSelected");
        var capture = GetType().GetMethod(nameof(CaptureSelection), Fields).MakeGenericMethod(GameType("CharacterInfo"));
        selectedEvent.AddEventHandler(selector, Delegate.CreateDelegate(selectedEvent.EventHandlerType, this, capture));
        return (IList)Get(selector, "icons");
    }

    private void CaptureSelection<T>(T character) => confirmedCharacter = character;

    [TestCase(false)]
    [TestCase(true)]
    public void UnrelatedAvailabilityRefreshPreservesTentativeToggleAndConfirmation(bool categoryFiltered)
    {
        var icons = InitializeSelector();
        var selected = icons[0];
        var toggle = Property(selected, "Toggle");
        SetProperty(toggle, "isOn", true);
        int notifications = 0;
        selector.GetType().GetEvent("OnSelectedToggleChanged").AddEventHandler(selector, (Action)(() => notifications++));
        if (categoryFiltered) Call(selector, "OnCategoryChange", 2);
        otherPlayer.Data["SelectedCharacter"] = new PlayerProperty("AQA=");

        for (int refresh = 0; refresh < 3; refresh++) Call(selector, "UpdateUnavailablePokemons");

        Assert.That(Property(toggle, "isOn"), Is.True);
        Assert.That(Property(selector, "HoveredCharacter"), Is.SameAs(Property(selected, "Info")));
        Assert.That(Property(Property(icons[1], "Toggle"), "interactable"), Is.False);
        Assert.That(notifications, Is.Zero, "Refreshing availability must not resubmit the tentative choice.");
        Call(selector, "OnConfirmButtonPressed");
        Assert.That(confirmedCharacter, Is.SameAs(Property(selected, "Info")));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BannedOrPickedTentativeChoiceClearsToggleAndHoverWithoutResurrecting(bool banned)
    {
        var icons = InitializeSelector();
        var toggle = Property(icons[0], "Toggle");
        SetProperty(toggle, "isOn", true);
        if (banned) bans.Add(0);
        else otherPlayer.Data["SelectedCharacter"] = new PlayerProperty("AAA=");

        Call(selector, "UpdateUnavailablePokemons");

        Assert.That(Property(toggle, "isOn"), Is.False);
        Assert.That(Property(toggle, "interactable"), Is.False);
        Assert.That(Property(selector, "HoveredCharacter"), Is.Null);
        Call(selector, "OnConfirmButtonPressed");
        Assert.That(confirmedCharacter, Is.Null);
        bans.Clear();
        otherPlayer.Data["SelectedCharacter"] = new PlayerProperty("//8=");
        Call(selector, "UpdateUnavailablePokemons");
        Assert.That(Property(toggle, "interactable"), Is.True);
        Assert.That(Property(toggle, "isOn"), Is.False);
        SetProperty(Property(icons[1], "Toggle"), "isOn", true);
        Call(selector, "OnConfirmButtonPressed");
        Assert.That(confirmedCharacter, Is.SameAs(Property(icons[1], "Info")));
    }

    [TestCase("Picking", "Idle", 0, false, false, true, false)]
    [TestCase("Picking", "Idle", 1, false, false, false, false)]
    [TestCase("Picking", "Picking", 0, true, true, false, false)]
    [TestCase("Picking", "Confirmed", 0, true, false, false, true)]
    [TestCase("Picking", "Confirmed", 0, false, false, false, true)]
    [TestCase("Picking", "Confirmed", 1, false, false, false, true)]
    [TestCase("Banning", "Banning", 0, true, true, false, false)]
    [TestCase("Banning", "BanningIdle", 0, false, false, false, false)]
    [TestCase("Banning", "BanningConfirmed", 0, false, false, false, false)]
    [TestCase("Banning", "Idle", 1, false, false, false, false)]
    [TestCase("Preparation", "Confirmed", 0, false, false, false, true)]
    [TestCase("Preparation", "Idle", 0, false, false, false, true)]
    [TestCase("Starting", "Confirmed", 0, false, false, false, false)]
    [TestCase("Waiting", "Idle", 0, false, false, false, false)]
    public void DraftActionsAreMutuallyExclusiveAcrossTurnsAndPhases(string phase, string state, int turn,
        bool localTurn, bool confirm, bool requestPick, bool prepare)
    {
        var blue = new List<GameSessionPlayer> { otherPlayer, localPlayer };
        if (localTurn) blue.Reverse();
        Set(draft, "blueTeamPlayers", blue);
        Set(draft, "orangeTeamPlayers", new List<GameSessionPlayer>
        {
            new GameSessionPlayer("orange-first"), new GameSessionPlayer("orange-second")
        });
        playerIDs.Add(localPlayer.Id);
        playerStates.Add((byte)Enum.Parse(GameType("DraftPlayerState"), state));
        SetProperty(Get(draft, "currentDraftPhase"), "Value", Enum.Parse(GameType("DraftPhase"), phase));
        ((NetworkVariable<int>)Get(draft, "currentTurnIndex")).Value = turn;
        Set(draft, "draftPlayerHolderBlue", Add("Blue", "DraftPlayerHolder"));
        Set(draft, "draftPlayerHolderOrange", Add("Orange", "DraftPlayerHolder"));
        foreach (string button in new[] { "confirmButton", "switchButton", "banRequestButton", "battlePrepButton" })
            Set(draft, button, Add(button, "UnityEngine.UI.Button"));
        Set(draft, "pkmnListSideInfo", Add("Side info", "PkmnListSideInfo"));

        for (int refresh = 0; refresh < 3; refresh++) Call(draft, "UpdateUIForCurrentTurn");

        Assert.That(((Component)Get(draft, "confirmButton")).gameObject.activeSelf, Is.EqualTo(confirm));
        Assert.That(((Component)Get(draft, "switchButton")).gameObject.activeSelf, Is.EqualTo(requestPick));
        Assert.That(((Component)Get(draft, "battlePrepButton")).gameObject.activeSelf, Is.EqualTo(prepare));
        Assert.That(((Component)Get(draft, "pkmnListSideInfo")).gameObject.activeSelf, Is.EqualTo(prepare));
        Assert.That(selector.gameObject.activeSelf, Is.EqualTo(!prepare && (phase == "Banning" || phase == "Picking")));
        Assert.That(((Component)Get(draft, "banRequestButton")).gameObject.activeSelf,
            Is.EqualTo(phase == "Banning" && !localTurn && turn == 0));

        if (phase == "Picking" && state == "Confirmed")
        {
            for (int nextTurn = 1; nextTurn <= 2; nextTurn++)
            {
                ((NetworkVariable<int>)Get(draft, "currentTurnIndex")).Value = nextTurn;
                Call(draft, "OnPlayerStatesChanged", new NetworkListEvent<byte>
                {
                    Type = NetworkListEvent<byte>.EventType.Value
                });
                Assert.That(((Component)Get(draft, "switchButton")).gameObject.activeSelf, Is.False);
                Assert.That(((Component)Get(draft, "confirmButton")).gameObject.activeSelf, Is.False);
                Assert.That(((Component)Get(draft, "battlePrepButton")).gameObject.activeSelf, Is.True);
                Assert.That(selector.gameObject.activeSelf, Is.False);
            }
        }
    }
}
