using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

public class LobbyChatNetworkTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<NetworkManager> managers = new List<NetworkManager>();
    private readonly List<List<object>> received = new List<List<object>>();
    private readonly List<Component> receivers = new List<Component>();
    private Component controller;
    private object previousController;
    private NetworkObject relay;
    private GameObject prefab;
    private GameObject trainerPrefab;
    private NetworkObject cityTrainer;
    private Type clothesType;
    private object previousClothesList;
    private Type receiverType;
    private string chatId;

    private static object Field(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static void SetField(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private GameObject Node(string name)
    {
        var node = new GameObject(name);
        objects.Add(node);
        return node;
    }

    private NetworkManager Manager(string name, ushort port)
    {
        var node = Node(name);
        var manager = node.AddComponent<NetworkManager>();
        var transport = node.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
        manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
        manager.AddNetworkPrefab(prefab);
        manager.AddNetworkPrefab(trainerPrefab);
        managers.Add(manager);
        return manager;
    }

    private static IEnumerator Until(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Chat did not reach the expected network state.");
    }

    private static object Player(string id, ulong clientId)
    {
        var player = Activator.CreateInstance(GameType("Unite.Multiplayer.GameSessionPlayer"), id, false);
        var propertyType = GameType("Unity.Services.Multiplayer.PlayerProperty");
        var visibilityType = propertyType.GetConstructors().Single().GetParameters()[1].ParameterType;
        var data = (IDictionary)player.GetType().GetProperty("Data").GetValue(player);
        data["OwnerID"] = Activator.CreateInstance(propertyType, clientId.ToString(), Enum.ToObject(visibilityType, 0));
        return player;
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        chatId = "party";
        if (NetworkManager.Singleton != null) Assert.Ignore("An external network session is active.");
        var controllerType = GameType("LobbyController");
        previousController = controllerType.GetProperty("Instance").GetValue(null);
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/Chat/ChatManagerNetworkObject.prefab");
        trainerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldTrainer.prefab");
#else
        Assert.Ignore("Requires the Editor-authored chat network prefab.");
#endif
        Assert.That(prefab, Is.Not.Null);
        receiverType = GameType("ChatMessageReciever");
        var root = Node("Chat party fixture");
        root.SetActive(false);
        controller = root.AddComponent(controllerType);
        controllerType.GetProperty("Instance").SetValue(null, controller);

        var host = Manager("Chat host", 0);
        Assert.That(host.StartHost(), Is.True);
        ushort port = host.GetComponent<UnityTransport>().GetLocalEndpoint().Port;
        for (int index = 0; index < 3; index++)
        {
            var client = Manager("Chat client " + index, port);
            Assert.That(client.StartClient(), Is.True);
        }
        yield return Until(() => managers.All(manager => manager.IsConnectedClient) && host.ConnectedClients.Count == 4);

        var party = Activator.CreateInstance(GameType("Unite.Multiplayer.GameSessionView"));
        party.GetType().GetProperty("Id").SetValue(party, "party");
        var players = (IList)party.GetType().GetProperty("Players").GetValue(party);
        for (int index = 0; index < managers.Count; index++)
        {
            var player = Player("player-" + index, managers[index].LocalClientId);
            players.Add(player);
            if (index == 0) SetField(controller, "localPlayer", player);
        }
        SetField(controller, "partyLobby", party);

        var instance = Object.Instantiate(prefab);
        objects.Add(instance);
        relay = instance.GetComponent<NetworkObject>();
        relay.Spawn();
        yield return Until(() => managers.All(manager => manager.SpawnManager.SpawnedObjects.ContainsKey(relay.NetworkObjectId)));
        foreach (var manager in managers)
        {
            var receiver = manager.SpawnManager.SpawnedObjects[relay.NetworkObjectId].GetComponent(receiverType);
            receivers.Add(receiver);
            var messages = new List<object>();
            received.Add(messages);
            var receivedEvent = receiverType.GetEvent("OnMessageRecieved");
            var parameter = Expression.Parameter(receivedEvent.EventHandlerType.GetMethod("Invoke").GetParameters()[0].ParameterType);
            var handler = Expression.Lambda(receivedEvent.EventHandlerType,
                Expression.Call(Expression.Constant(messages), typeof(List<object>).GetMethod("Add"),
                    Expression.Convert(parameter, typeof(object))), parameter).Compile();
            receivedEvent.AddEventHandler(receiver, handler);
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (cityTrainer != null && cityTrainer.IsSpawned) cityTrainer.Despawn();
        if (relay != null && relay.IsSpawned) relay.Despawn();
        foreach (var manager in managers.AsEnumerable().Reverse())
            if (manager != null) manager.Shutdown();
        yield return null;
        if (controller != null)
            GameType("LobbyController").GetProperty("Instance").SetValue(null, previousController);
        foreach (var node in objects) if (node != null) Object.Destroy(node);
        yield return null;
        if (clothesType != null) clothesType.GetField("Instance").SetValue(null, previousClothesList);
        clothesType = null;
        cityTrainer = null;
        objects.Clear();
        managers.Clear();
        receivers.Clear();
        received.Clear();
    }

    private void Send(int senderIndex, string recipientId = null, string senderId = null, string lobbyId = null,
        string text = "Hello, party! — private or global")
    {
        var messageType = GameType("ChatManager+ChatMessage");
        var message = Activator.CreateInstance(messageType);
        messageType.GetField("senderID").SetValue(message, senderId ?? "player-" + senderIndex);
        messageType.GetField("recipientID").SetValue(message, recipientId ?? string.Empty);
        messageType.GetField("lobbyID").SetValue(message, lobbyId ?? chatId);
        messageType.GetField("message").SetValue(message, text);
        receiverType.GetMethod("RecieveMessageRPC").Invoke(receivers[senderIndex], new[] { message, (object)default(RpcParams) });
    }

    private IEnumerator SpawnBubbleTrainer()
    {
        clothesType = GameType("ClothesList");
        previousClothesList = clothesType.GetField("Instance").GetValue(null);
        Node("Empty city bubble outfit fixture").AddComponent(clothesType);
        var instance = Object.Instantiate(trainerPrefab);
        objects.Add(instance);
        var avatar = instance.GetComponent(GameType("OpenWorldTrainerAvatar"));
        avatar.GetType().GetMethod("Initialize").Invoke(avatar, new object[] { "player-1", "Bubble Trainer", "" });
        cityTrainer = instance.GetComponent<NetworkObject>();
        cityTrainer.SpawnWithOwnership(managers[1].LocalClientId);
        yield return Until(() => managers.All(manager => manager.SpawnManager.SpawnedObjects.ContainsKey(cityTrainer.NetworkObjectId)));
    }

    private Component[] TrainerBubbles() => managers.Select(manager => manager.SpawnManager
        .SpawnedObjects[cityTrainer.NetworkObjectId].GetComponent(GameType("OpenWorldTrainerChatBubble"))).ToArray();

    private static GameObject BubblePanel(Component bubble) => ((Transform)Field(bubble, "bubbleRoot")).gameObject;

    [UnityTest]
    public IEnumerator CityGlobalMessagesShowOnSenderAndEveryObserverWithoutChatPanel()
    {
        UseCity();
        yield return SpawnBubbleTrainer();
        var bubbles = TrainerBubbles();
        Assert.That(bubbles.All(bubble => !BubblePanel(bubble).activeSelf), Is.True);
        Send(1);
        yield return Until(() => bubbles.All(bubble => BubblePanel(bubble).activeSelf));
        Send(0, text: "Another trainer's message");
        yield return Until(() => received.All(messages => messages.Count == 2));
        foreach (var bubble in bubbles)
        {
            var label = Field(bubble, "messageText");
            Assert.That(label.GetType().GetProperty("text").GetValue(label), Is.EqualTo("Hello, party! — private or global"));
        }
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator PrivatePartyAndInvalidMessagesNeverBecomeCityBubbles()
    {
        yield return SpawnBubbleTrainer();
        var bubbles = TrainerBubbles();
        Send(1);
        yield return Until(() => received.All(messages => messages.Count == 1));
        Assert.That(bubbles.All(bubble => !BubblePanel(bubble).activeSelf), Is.True, "Static party messages are not city bubbles.");
        UseCity();
        Send(1, "player-2");
        yield return Until(() => received[1].Count == 2 && received[2].Count == 2);
        Assert.That(bubbles.All(bubble => !BubblePanel(bubble).activeSelf), Is.True, "Private messages must never be shown overhead.");
        Send(1, lobbyId: "wrong-city");
        Send(1, senderId: "player-0");
        yield return new WaitForSecondsRealtime(0.25f);
        Assert.That(received.Select(messages => messages.Count), Is.EqualTo(new[] { 1, 2, 2, 1 }));
        Assert.That(bubbles.All(bubble => !BubblePanel(bubble).activeSelf), Is.True);
        Send(1, text: "Public message");
        yield return Until(() => bubbles.All(bubble => BubblePanel(bubble).activeSelf));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator NewCityMessageReplacesBubbleAndDespawnClearsIt()
    {
        UseCity();
        yield return SpawnBubbleTrainer();
        var bubbles = TrainerBubbles();
        Send(1, text: "First");
        yield return Until(() => bubbles.All(bubble => BubblePanel(bubble).activeSelf));
        foreach (var bubble in bubbles) SetField(bubble, "hideTime", -1f);
        yield return Until(() => bubbles.All(bubble => !BubblePanel(bubble).activeSelf));
        Send(1, text: "Second");
        yield return Until(() => bubbles.All(bubble => BubblePanel(bubble).activeSelf));
        Send(1, text: "Replacement");
        yield return Until(() => bubbles.All(bubble => {
            var label = Field(bubble, "messageText");
            return (string)label.GetType().GetProperty("text").GetValue(label) == "Replacement";
        }));
        var panels = bubbles.Select(BubblePanel).ToArray();
        cityTrainer.Despawn(false);
        yield return Until(() => panels.All(panel => panel == null || !panel.activeSelf));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator GlobalMessagesReachAllPartyMembersIncludingSender()
    {
        Send(1);
        yield return Until(() => received.All(messages => messages.Count == 1));
        foreach (var messages in received)
        {
            Assert.That(messages[0].GetType().GetField("senderID").GetValue(messages[0]), Is.EqualTo("player-1"));
            Assert.That(messages[0].GetType().GetField("message").GetValue(messages[0]),
                Is.EqualTo("Hello, party! — private or global"));
        }
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator PrivateMessagesReachOnlySenderAndRecipientNotHostOrOtherClient()
    {
        Send(1, "player-2");
        yield return Until(() => received[1].Count == 1 && received[2].Count == 1);
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.That(received[0], Is.Empty);
        Assert.That(received[3], Is.Empty);
        Assert.That(received[2][0].GetType().GetField("recipientID").GetValue(received[2][0]), Is.EqualTo("player-2"));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator HostCanSendAndReceivePrivateMessages()
    {
        Send(0, "player-1");
        yield return Until(() => received[0].Count == 1 && received[1].Count == 1);
        Send(1, "player-0");
        yield return Until(() => received[0].Count == 2 && received[1].Count == 2);
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.That(received[2], Is.Empty);
        Assert.That(received[3], Is.Empty);
        LogAssert.NoUnexpectedReceived();
    }

    private Component UseCity()
    {
        chatId = "social-city";
        var social = controller.gameObject.AddComponent(GameType("OpenWorldLobbyController"));
        var sessionType = GameType("Unity.Services.Multiplayer.ISession");
        var session = LobbyChatSessionProxy.Create(sessionType);
        var sessionValues = ((LobbyChatSessionProxy)session).Values;
        sessionValues["Id"] = chatId;
        sessionValues["Name"] = "Trainer City";
        sessionValues["Host"] = "player-0";
        sessionValues["MaxPlayers"] = 16;
        var networkType = sessionType.GetProperty("Network").PropertyType;
        var sessionNetwork = LobbyChatSessionProxy.Create(networkType);
        ((LobbyChatSessionProxy)sessionNetwork).Values["State"] = Enum.Parse(networkType.GetProperty("State").PropertyType, "Started");
        sessionValues["Network"] = sessionNetwork;
        sessionValues["Properties"] = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string),
            GameType("Unity.Services.Multiplayer.SessionProperty")));
        var playerType = sessionType.GetProperty("Players").PropertyType.GetGenericArguments()[0];
        var members = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(playerType));
        var connections = (IDictionary)Field(social, "connections");
        for (int index = 0; index < managers.Count; index++)
        {
            var member = LobbyChatSessionProxy.Create(playerType);
            var values = ((LobbyChatSessionProxy)member).Values;
            values["Id"] = "player-" + index;
            var untrustedPlayer = Player("player-" + index, ulong.MaxValue);
            values["Properties"] = untrustedPlayer.GetType().GetProperty("Data").GetValue(untrustedPlayer);
            members.Add(member);
            connections[managers[index].LocalClientId] = "player-" + index;
        }
        sessionValues["Players"] = members;
        SetField(social, "session", session);
        SetField(social, "localPlayer", Field(controller, "localPlayer"));
        SetField(social, "network", managers[0]);
        controller.GetType().GetProperty("OpenWorldLobby").SetValue(controller, social);
        return social;
    }

    [UnityTest]
    public IEnumerator CityRosterAndCounterExcludeDisconnectedSessionMembers()
    {
        var social = UseCity();
        var property = controller.GetType().GetProperty("ChatLobby");
        var view = property.GetValue(controller);
        var players = (IList)view.GetType().GetProperty("Players").GetValue(view);
        Assert.That(players.Count, Is.EqualTo(4));
        var departingId = managers[3].LocalClientId;
        managers[3].Shutdown();
        yield return Until(() => !managers[0].ConnectedClients.ContainsKey(departingId));
        // The mock service still contains all four members and the local mapping
        // still contains the departed client. Neither may keep them in the UI.
        Assert.That(property.GetValue(controller), Is.SameAs(view));
        Assert.That(players.Count, Is.EqualTo(3));
        social.GetType().GetMethod("UpdateStatus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(social, null);
        StringAssert.Contains("3/16", (string)social.GetType().GetProperty("Status").GetValue(social));
        Send(1);
        yield return Until(() => received.Take(3).All(messages => messages.Count == 1));
        Assert.That(received[3], Is.Empty);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator SocialCityReusesGlobalAndPrivateRoutingWithoutChangingParty()
    {
        var party = Field(controller, "partyLobby");
        var localPlayer = Field(controller, "localPlayer");
        var localData = (IDictionary)localPlayer.GetType().GetProperty("Data").GetValue(localPlayer);
        var originalOwner = localData["OwnerID"];
        UseCity();
        var view = controller.GetType().GetProperty("ChatLobby").GetValue(controller);
        Assert.That(view.GetType().GetProperty("Id").GetValue(view), Is.EqualTo(chatId));
        Assert.That(Field(controller, "partyLobby"), Is.SameAs(party));
        Assert.That(localData["OwnerID"], Is.SameAs(originalOwner), "Social snapshots must not overwrite the persistent party player.");
        Send(1);
        yield return Until(() => received.All(messages => messages.Count == 1));
        Send(1, "player-2");
        yield return Until(() => received[1].Count == 2 && received[2].Count == 2);
        yield return new WaitForSecondsRealtime(0.15f);
        Assert.That(received[0].Count, Is.EqualTo(1));
        Assert.That(received[3].Count, Is.EqualTo(1));
        Assert.That(controller.GetType().GetProperty("ChatLobby").GetValue(controller), Is.SameAs(view));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator SocialCityRejectsPartyMessagesAndSpoofedSenders()
    {
        UseCity();
        Send(1, lobbyId: "party");
        Send(1, senderId: "player-2");
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.That(received.All(messages => messages.Count == 0), Is.True);
        Send(1, "player-2");
        yield return Until(() => received[1].Count == 1 && received[2].Count == 1);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator SocialRoutingRefreshesOwnersAfterMigrationWithoutReplacingView()
    {
        var social = UseCity();
        var property = controller.GetType().GetProperty("ChatLobby");
        var view = property.GetValue(controller);
        var connections = (IDictionary)Field(social, "connections");
        connections[managers[1].LocalClientId] = "player-2";
        connections[managers[2].LocalClientId] = "player-1";
        Send(1, senderId: "player-1");
        yield return new WaitForSecondsRealtime(0.15f);
        Assert.That(received.All(messages => messages.Count == 0), Is.True);
        Send(1, "player-1", "player-2");
        yield return Until(() => received[1].Count == 1 && received[2].Count == 1);
        Assert.That(received[0], Is.Empty);
        Assert.That(received[3], Is.Empty);
        Assert.That(property.GetValue(controller), Is.SameAs(view));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator ChatManagerRebindsExistingAndReplacementReceiversWithoutDuplicates()
    {
        var ui = Node("Chat manager lifecycle fixture").AddComponent(GameType("ChatManager"));
        SetField(ui, "messageRecieverPrefab", prefab);
        yield return Until(() => (Object)Field(ui, "chatMessageReciever") == receivers[0]);
        ((Behaviour)ui).enabled = false;
        ((Behaviour)ui).enabled = true;
        yield return Until(() => (Object)Field(ui, "chatMessageReciever") == receivers[0]);
        Assert.That(managers[0].SpawnManager.SpawnedObjectsList.Count(node => node.GetComponent(receiverType) != null), Is.EqualTo(1));
        relay.Despawn();
        yield return Until(() => (Object)Field(ui, "chatMessageReciever") != null &&
            (bool)Field(ui, "chatMessageReciever").GetType().GetProperty("IsSpawned").GetValue(Field(ui, "chatMessageReciever")));
        Assert.That(managers[0].SpawnManager.SpawnedObjectsList.Count(node => node.GetComponent(receiverType) != null), Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
    }
}

public class LobbyChatSessionProxy : DispatchProxy
{
    public readonly Dictionary<string, object> Values = new Dictionary<string, object>();

    public static object Create(Type interfaceType) => typeof(DispatchProxy).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == "Create" && method.IsGenericMethodDefinition)
        .MakeGenericMethod(interfaceType, typeof(LobbyChatSessionProxy)).Invoke(null, null);

    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        if (method.Name.StartsWith("get_") && Values.TryGetValue(method.Name.Substring(4), out var value)) return value;
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
