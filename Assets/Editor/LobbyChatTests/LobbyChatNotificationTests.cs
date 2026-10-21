using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Runtime scripts live in Assembly-CSharp, so use reflection from this test assembly.
public class LobbyChatNotificationTests
{
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
        SetField(controller, "localPlayer", Activator.CreateInstance(GameType("Unite.Multiplayer.GameSessionPlayer"), "local"));
        SetField(controller, "partyLobby", NewParty());
        Call("SyncLobby");
    }

    private static object NewParty() => Activator.CreateInstance(GameType("Unite.Multiplayer.GameSessionView"));

    private void Receive(string sender = "remote", string text = "Ready to play?")
    {
        var messageType = GameType("ChatManager+ChatMessage");
        var message = Activator.CreateInstance(messageType);
        messageType.GetField("senderID").SetValue(message, sender);
        messageType.GetField("message").SetValue(message, text);
        Call("RecieveMessageRPC", message);
    }

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
}
