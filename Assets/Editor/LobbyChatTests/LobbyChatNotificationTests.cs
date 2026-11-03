using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Runtime scripts live in Assembly-CSharp, so use reflection from this test assembly.
public abstract class LobbyChatNotificationTestsBase
{
    protected virtual bool UseCityUi => false;
    private GameObject cityRoot;
    private Scene scene;
    private bool openedScene;
    private Component manager;
    private Component controller;
    private object previousController;
    private PropertyInfo instanceProperty;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Field(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void SetField(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private object Call(string name, params object[] args) => manager.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(manager, args);

    private int UnreadCount => (int)manager.GetType().GetProperty("UnreadMessageCount").GetValue(manager);
    private GameObject Badge => (GameObject)Field(manager, "unreadBadge");
    private GameObject Popup => (GameObject)Field(manager, "messageNotification");

    private string Text(string field) => (string)Field(manager, field).GetType().GetProperty("text")
        .GetValue(Field(manager, field));

    [OneTimeSetUp]
    public void LoadLobbyScene()
    {
        if (UseCityUi)
        {
            cityRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab");
            manager = cityRoot.GetComponent(GameType("ChatManager"));
            Assert.That(manager, Is.Not.Null);
            return;
        }
        scene = SceneManager.GetSceneByPath("Assets/Scenes/LobbyScene.unity");
        openedScene = !scene.isLoaded;
        if (openedScene)
            scene = EditorSceneManager.OpenScene("Assets/Scenes/LobbyScene.unity", OpenSceneMode.Additive);
        manager = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(GameType("ChatManager"), true)).Single();
    }

    [SetUp]
    public void SetUpParty()
    {
        var controllerType = GameType("LobbyController");
        instanceProperty = controllerType.GetProperty("Instance");
        previousController = instanceProperty.GetValue(null);
        var root = new GameObject("Lobby chat test controller");
        root.SetActive(false); // Never initialize authentication or networking.
        controller = root.AddComponent(controllerType);
        instanceProperty.SetValue(null, controller);
        SetField(controller, "localPlayer", Activator.CreateInstance(GameType("Unite.Multiplayer.GameSessionPlayer"), "local", false));
        SetField(controller, "partyLobby", NewParty());
        Call("SyncLobby");
    }

    private static object NewParty()
    {
        var party = Activator.CreateInstance(GameType("Unite.Multiplayer.GameSessionView"));
        party.GetType().GetProperty("Id").SetValue(party, "party");
        return party;
    }

    private static object Message(string sender = "remote", string text = "Ready to play?", string recipient = null,
        string lobby = "party")
    {
        var messageType = GameType("ChatManager+ChatMessage");
        var message = Activator.CreateInstance(messageType);
        messageType.GetField("senderID").SetValue(message, sender);
        messageType.GetField("recipientID").SetValue(message, recipient);
        messageType.GetField("lobbyID").SetValue(message, lobby);
        messageType.GetField("message").SetValue(message, text);
        return message;
    }

    private void Receive(string sender = "remote", string text = "Ready to play?", string recipient = null,
        string lobby = "party") => Call("RecieveMessageRPC", Message(sender, text, recipient, lobby));

    private IList PartyPlayers => (IList)Field(controller, "partyLobby").GetType().GetProperty("Players")
        .GetValue(Field(controller, "partyLobby"));

    private object AddPlayer(string id, ulong clientId, string name = "Party member", bool isBot = false)
    {
        var player = Activator.CreateInstance(GameType("Unite.Multiplayer.GameSessionPlayer"), id, isBot);
        SetPlayerProperty(player, "PlayerName", name);
        SetPlayerProperty(player, "OwnerID", clientId.ToString());
        SetPlayerProperty(player, "ClothingInfo", Convert.ToBase64String(new byte[16]));
        PartyPlayers.Add(player);
        return player;
    }

    private static void SetPlayerProperty(object player, string key, string value)
    {
        var propertyType = GameType("Unity.Services.Multiplayer.PlayerProperty");
        var visibilityType = propertyType.GetConstructors().Single().GetParameters()[1].ParameterType;
        var data = (IDictionary)player.GetType().GetProperty("Data").GetValue(player);
        data[key] = Activator.CreateInstance(propertyType, value, Enum.ToObject(visibilityType, 0));
    }

    private bool Route(object message, ulong senderClientId, out IList targets)
    {
        var arguments = new[] { Field(controller, "partyLobby"), message, senderClientId, null };
        var result = (bool)GameType("ChatMessageReciever").GetMethod("TryGetMessageTargets",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
        targets = (IList)arguments[3];
        return result;
    }

    private string SelectedPlayer => (string)manager.GetType().GetProperty("SelectedPrivatePlayerId").GetValue(manager);
    private string Channel => manager.GetType().GetProperty("CurrentChannel").GetValue(manager).ToString();
    private bool DropdownEnabled => ((Component)Field(manager, "privatePlayerDropdown")).gameObject.activeSelf &&
        (bool)Field(manager, "privatePlayerDropdown").GetType()
        .GetProperty("interactable").GetValue(Field(manager, "privatePlayerDropdown"));
    private bool[] MessageVisibility => ((IList)Field(manager, "chatMessages")).Cast<Component>()
        .Select(message => message.gameObject.activeSelf).ToArray();

    private void RemoveMessageObjects()
    {
        // Edit-mode tests must destroy scene objects immediately, not through the
        // runtime ClearChat's deferred destruction at the end of a player frame.
        var messages = (IList)Field(manager, "chatMessages");
        foreach (Component message in messages) Object.DestroyImmediate(message.gameObject);
        messages.Clear();
    }

    [TearDown]
    public void TearDownParty()
    {
        RemoveMessageObjects();
        Call("ClearChat");
        Call("CloseChat");
        instanceProperty.SetValue(null, previousController);
        Object.DestroyImmediate(controller.gameObject);
    }

    [OneTimeTearDown]
    public void CloseLobbyScene()
    {
        if (cityRoot != null) PrefabUtility.UnloadPrefabContents(cityRoot);
        if (openedScene) EditorSceneManager.CloseScene(scene, true);
    }

    [Test]
    public void ClosedChatShowsPreviewAndAccumulatesUnreadMessages()
    {
        Receive();
        Receive(text: "Let's go!");
        Assert.That(UnreadCount, Is.EqualTo(2));
        Assert.That(Badge.activeSelf, Is.True);
        Assert.That(Text("unreadCountText"), Is.EqualTo("2"));
        Assert.That(Popup.activeSelf, Is.True);
        Assert.That(Text("notificationMessageText"), Is.EqualTo("Let's go!"));
        Assert.That(Text("notificationSenderText"), Is.EqualTo("Party member"));
    }

    [Test]
    public void OpeningChatMarksMessagesReadAndHidesNotification()
    {
        Receive();
        var button = Popup.GetComponent(GameType("UnityEngine.UI.Button"));
        var onClick = (UnityEngine.Events.UnityEventBase)button.GetType().GetProperty("onClick").GetValue(button);
        Assert.That(onClick.GetPersistentTarget(0), Is.EqualTo(manager));
        Assert.That(onClick.GetPersistentMethodName(0), Is.EqualTo("OpenChat"));
        Call("OpenChat"); // Scene's persistent callback is intentionally runtime-only.
        Assert.That(UnreadCount, Is.Zero);
        Assert.That(Badge.activeSelf, Is.False);
        Assert.That(Popup.activeSelf, Is.False);
        Receive();
        Assert.That(UnreadCount, Is.Zero, "Messages visible in open chat are already read.");
    }

    [Test]
    public void OwnAndEmptyMessagesDoNotNotify()
    {
        Receive(sender: "local");
        Receive(text: "   ");
        Assert.That(UnreadCount, Is.Zero);
        Assert.That(Badge.activeSelf, Is.False);
        Assert.That(Popup.activeSelf, Is.False);
    }

    [Test]
    public void PreviewExpiresWithoutClearingUnreadCount()
    {
        Receive();
        SetField(manager, "notificationHideTime", -1f);
        Call("Update");
        Assert.That(Popup.activeSelf, Is.False);
        Assert.That(Badge.activeSelf, Is.True);
        Assert.That(UnreadCount, Is.EqualTo(1));
    }

    [Test]
    public void ClosingAndReenablingPartyKeepsUnreadCountButHidesPreview()
    {
        Receive();
        Call("OnDisable");
        Call("OnEnable");
        Assert.That(UnreadCount, Is.EqualTo(1));
        Assert.That(Badge.activeSelf, Is.True);
        Assert.That(Popup.activeSelf, Is.False);
    }

    [Test]
    public void ChangingOrLeavingPartyResetsUnreadState()
    {
        Receive();
        RemoveMessageObjects();
        SetField(controller, "partyLobby", NewParty());
        Call("SyncLobby");
        Assert.That(UnreadCount, Is.Zero);
        Assert.That(Badge.activeSelf, Is.False);
        Receive();
        RemoveMessageObjects();
        SetField(controller, "partyLobby", null);
        Call("SyncLobby");
        Receive();
        Assert.That(UnreadCount, Is.Zero);
        Assert.That(Popup.activeSelf, Is.False);
    }

    [Test]
    public void ClearingChatAlsoClearsUnreadState()
    {
        Receive();
        RemoveMessageObjects();
        Call("ClearChat");
        Assert.That(UnreadCount, Is.Zero);
        Assert.That(Badge.activeSelf, Is.False);
        Assert.That(Popup.activeSelf, Is.False);
    }

    [Test]
    public void BadgeCapsDisplayedCountAt99Plus()
    {
        for (int i = 0; i < 100; i++) Receive();
        Assert.That(UnreadCount, Is.EqualTo(100));
        Assert.That(Text("unreadCountText"), Is.EqualTo("99+"));
    }

    [Test]
    public void SceneWiresBothTogglesAndPlayerDropdown()
    {
        Assert.That(((Component)Field(manager, "globalChatToggle")).name, Is.EqualTo("GlobalChat"));
        Assert.That(((Component)Field(manager, "privateChatToggle")).name, Is.EqualTo("PrivateChat"));
        Assert.That(((Component)Field(manager, "privatePlayerDropdown")).name, Is.EqualTo("PrivatePlayerDropdown"));
    }

    [Test]
    public void GlobalChatDisablesDropdownAndHidesPrivateMessages()
    {
        AddPlayer("remote", 1);
        Receive();
        Receive(recipient: "local");
        Receive(sender: "local", recipient: "remote");
        Call("OpenGlobalChat");
        Assert.That(Channel, Is.EqualTo("Global"));
        Assert.That(DropdownEnabled, Is.False);
        Assert.That(MessageVisibility, Is.EqualTo(new[] { true, false, false }));
        Assert.That(UnreadCount, Is.EqualTo(1));
    }

    [Test]
    public void PrivateChatShowsOnlySelectedPlayersConversationInBothDirections()
    {
        AddPlayer("remote", 1);
        AddPlayer("other", 2);
        Receive();
        Receive(recipient: "local");
        Receive(sender: "local", recipient: "remote");
        Receive(sender: "other", recipient: "local");
        Call("OpenPrivateChat", "remote");
        Assert.That(Channel, Is.EqualTo("Private"));
        Assert.That(SelectedPlayer, Is.EqualTo("remote"));
        Assert.That(DropdownEnabled, Is.True);
        Assert.That(MessageVisibility, Is.EqualTo(new[] { false, true, true, false }));
        Assert.That(UnreadCount, Is.EqualTo(2));
        Call("SelectPrivatePlayer", 1);
        Assert.That(SelectedPlayer, Is.EqualTo("other"));
        Assert.That(MessageVisibility, Is.EqualTo(new[] { false, false, false, true }));
        Assert.That(UnreadCount, Is.EqualTo(1));
    }

    [Test]
    public void MessagesForAnotherConversationRemainUnreadWhileChatIsOpen()
    {
        AddPlayer("remote", 1);
        AddPlayer("other", 2);
        Call("OpenPrivateChat", "remote");
        Receive(recipient: "local");
        Receive(sender: "other", recipient: "local");
        Receive();
        Assert.That(UnreadCount, Is.EqualTo(2));
        Assert.That(MessageVisibility, Is.EqualTo(new[] { true, false, false }));
        Call("OpenGlobalChat");
        Assert.That(UnreadCount, Is.EqualTo(1));
    }

    [Test]
    public void NotificationOpensTheCorrespondingPrivateConversation()
    {
        AddPlayer("remote", 1);
        Receive(recipient: "local");
        Call("OpenChat");
        Assert.That(Channel, Is.EqualTo("Private"));
        Assert.That(SelectedPlayer, Is.EqualTo("remote"));
        Assert.That(UnreadCount, Is.Zero);
        Assert.That(Popup.activeSelf, Is.False);
    }

    [Test]
    public void GlobalNotificationReturnsToGlobalConversation()
    {
        AddPlayer("remote", 1);
        Call("OpenPrivateChat", "remote");
        Call("CloseChat");
        Receive();
        Call("OpenChat");
        Assert.That(Channel, Is.EqualTo("Global"));
        Assert.That(UnreadCount, Is.Zero);
    }

    [Test]
    public void UiEventsSwitchChannelsAndSelectPartyPlayers()
    {
        AddPlayer("local", 0);
        AddPlayer("remote", 1, "First player");
        AddPlayer("other", 2, "Second player");
        AddPlayer("bot", 0, isBot: true);
        Call("OnDisable");
        Call("OnEnable");
        Call("OpenGlobalChat");
        var toggle = Field(manager, "privateChatToggle");
        toggle.GetType().GetProperty("isOn").SetValue(toggle, true);
        Assert.That(Channel, Is.EqualTo("Private"));
        Assert.That(DropdownEnabled, Is.True);
        var dropdown = Field(manager, "privatePlayerDropdown");
        Assert.That(((IList)dropdown.GetType().GetProperty("options").GetValue(dropdown)).Count, Is.EqualTo(2));
        dropdown.GetType().GetProperty("value").SetValue(dropdown, 1);
        Assert.That(SelectedPlayer, Is.EqualTo("other"));
        toggle = Field(manager, "globalChatToggle");
        toggle.GetType().GetProperty("isOn").SetValue(toggle, true);
        Assert.That(Channel, Is.EqualTo("Global"));
        Assert.That(DropdownEnabled, Is.False);
    }

    [Test]
    public void RosterChangesPreserveSelectionByIdAndHandleDeparture()
    {
        var first = AddPlayer("remote", 1, "Duplicate name");
        var second = AddPlayer("other", 2, "Duplicate name");
        Call("OpenPrivateChat", "other");
        PartyPlayers.Remove(first);
        PartyPlayers.Add(first);
        SetPlayerProperty(second, "PlayerName", "New name");
        Call("SyncLobby");
        Assert.That(SelectedPlayer, Is.EqualTo("other"));
        var dropdown = Field(manager, "privatePlayerDropdown");
        var options = (IList)dropdown.GetType().GetProperty("options").GetValue(dropdown);
        Assert.That(options[0].GetType().GetProperty("text").GetValue(options[0]), Is.EqualTo("New name"));
        PartyPlayers.Remove(second);
        Call("SyncLobby");
        Assert.That(SelectedPlayer, Is.EqualTo("remote"));
        PartyPlayers.Clear();
        Call("SyncLobby");
        Assert.That(SelectedPlayer, Is.Null);
        Assert.That(DropdownEnabled, Is.False);
    }

    [TestCase("local")]
    [TestCase("missing")]
    [TestCase("bot")]
    public void InvalidPrivateChatTargetDoesNotOpenAnotherConversation(string playerId)
    {
        AddPlayer("remote", 1);
        AddPlayer("bot", 0, isBot: true);
        Call("OpenPrivateChat", playerId);
        Assert.That(Channel, Is.EqualTo("Global"));
        Assert.That(((GameObject)Field(manager, "chatContainer")).activeSelf, Is.False);
    }

    [Test]
    public void UnrelatedPrivateMessagesAndOldPartyMessagesAreDiscarded()
    {
        Receive(sender: "remote", recipient: "other");
        Receive(lobby: "previous-party");
        Assert.That(MessageVisibility, Is.Empty);
        Assert.That(UnreadCount, Is.Zero);
    }

    [Test]
    public void ChangingPartyWithTheSamePlayersResetsAndRebuildsSelection()
    {
        var player = AddPlayer("remote", 1);
        Call("OpenPrivateChat", "remote");
        SetField(controller, "partyLobby", NewParty());
        PartyPlayers.Add(player);
        Call("SyncLobby");
        Assert.That(Channel, Is.EqualTo("Global"));
        Assert.That(SelectedPlayer, Is.EqualTo("remote"));
        Assert.That(DropdownEnabled, Is.False);
    }

    [Test]
    public void ServerRoutesGlobalMessagesToHumanPartyMembersOnly()
    {
        AddPlayer("local", 0);
        AddPlayer("remote", 1);
        AddPlayer("other", 2);
        AddPlayer("bot", 0, isBot: true);
        Assert.That(Route(Message(), 1, out var targets), Is.True);
        Assert.That(targets, Is.EquivalentTo(new ulong[] { 0, 1, 2 }));
    }

    [TestCase("remote", "other", 1ul, 2ul)]
    [TestCase("local", "remote", 0ul, 1ul)]
    [TestCase("remote", "local", 1ul, 0ul)]
    public void ServerRoutesPrivateMessagesOnlyToSenderAndRecipient(string sender, string recipient,
        ulong senderClientId, ulong recipientClientId)
    {
        AddPlayer("local", 0);
        AddPlayer("remote", 1);
        AddPlayer("other", 2);
        Assert.That(Route(Message(sender: sender, recipient: recipient), senderClientId, out var targets), Is.True);
        Assert.That(targets, Is.EquivalentTo(new[] { senderClientId, recipientClientId }));
    }

    [TestCase("local", "remote", 1ul, "party", "Hello")]
    [TestCase("missing", "remote", 1ul, "party", "Hello")]
    [TestCase("remote", "missing", 1ul, "party", "Hello")]
    [TestCase("remote", "remote", 1ul, "party", "Hello")]
    [TestCase("remote", "bot", 1ul, "party", "Hello")]
    [TestCase("remote", "local", 1ul, "previous-party", "Hello")]
    [TestCase("remote", "local", 1ul, "party", "   ")]
    public void ServerRejectsSpoofedOrInvalidMessages(string sender, string recipient, ulong senderClientId,
        string lobby, string text)
    {
        AddPlayer("local", 0);
        AddPlayer("remote", 1);
        AddPlayer("bot", 0, isBot: true);
        Assert.That(Route(Message(sender, text, recipient, lobby), senderClientId, out _), Is.False);
    }
}

public sealed class LobbyChatNotificationTests : LobbyChatNotificationTestsBase
{
}

public sealed class OpenWorldChatNotificationTests : LobbyChatNotificationTestsBase
{
    protected override bool UseCityUi => true;
}
