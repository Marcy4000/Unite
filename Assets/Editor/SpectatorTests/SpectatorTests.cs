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

public class SpectatorTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private GameObject root;
    private Component lobby;
    private Component characters;
    private ScriptableObject map;
    private object previousLobby;
    private object previousCharacters;
    private GameSessionView view;
    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Team(string name) => Enum.Parse(T("Team"), name);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Fields).Invoke(target, args);
    private static GameSessionPlayer Player(string id, string team, short position = 0)
    {
        var player = new GameSessionPlayer(id);
        player.Data["PlayerTeam"] = new PlayerProperty(team);
        player.Data["PlayerPos"] = new PlayerProperty(Convert.ToBase64String(BitConverter.GetBytes(position)));
        return player;
    }

    [SetUp]
    public void Setup()
    {
        previousLobby = T("LobbyController").GetProperty("Instance").GetValue(null);
        previousCharacters = T("CharactersList").GetProperty("Instance").GetValue(null);
        root = new GameObject("Spectator regression fixture"); root.SetActive(false);
        lobby = root.AddComponent(T("LobbyController"));
        characters = root.AddComponent(T("CharactersList"));
        T("LobbyController").GetProperty("Instance").SetValue(null, lobby);
        T("CharactersList").GetProperty("Instance").SetValue(null, characters);
        map = ScriptableObject.CreateInstance(T("MapInfo"));
        Set(map, "maxTeamSize", 5);
        var maps = Array.CreateInstance(T("MapInfo"), 1); maps.SetValue(map, 0); Set(characters, "maps", maps);
        view = new GameSessionView();
        view.Data["SelectedMap"] = new SessionProperty(Convert.ToBase64String(BitConverter.GetBytes((short)0)));
        Set(lobby, "partyLobby", view);
        Set(lobby, "localPlayer", Player("observer", SpectatorSlotRules.TeamValue));
    }

    [TearDown]
    public void Cleanup()
    {
        T("LobbyController").GetProperty("Instance").SetValue(null, previousLobby);
        T("CharactersList").GetProperty("Instance").SetValue(null, previousCharacters);
        Object.DestroyImmediate(root); Object.DestroyImmediate(map);
    }

    [TestCase(-1, false)] [TestCase(0, true)] [TestCase(4, true)] [TestCase(5, false)]
    public void ExactlyFiveObserverSeats(int slot, bool expected) => Assert.That(SpectatorSlotRules.IsValidSlot(slot), Is.EqualTo(expected));

    [Test]
    public void LobbySceneContainsFiveAuthoredSpectatorSlotsBoundInSeatOrder()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
        try
        {
            var party = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true))
                .Single(component => component != null && component.GetType() == T("PartyScreenUI"));
            var slots = ((IEnumerable)Get(party, "spectatorIcons")).Cast<Component>().ToArray();
            var customs = (GameObject)Get(party, "customsLobbyRoot");
            Assert.That(slots.Length, Is.EqualTo(SpectatorSlotRules.Capacity));
            Assert.That(slots.All(slot => slot != null && slot.gameObject.scene == scene), Is.True);
            Assert.That(slots.Distinct().Count(), Is.EqualTo(slots.Length));
            Assert.That(slots.All(slot => slot.transform.IsChildOf(customs.transform)), Is.True);
            var holder = slots[0].transform.parent;
            Assert.That(holder.childCount, Is.EqualTo(SpectatorSlotRules.Capacity));
            Call(party, "Awake");
            for (int i = 0; i < slots.Length; i++)
            {
                Assert.That(slots[i].transform.parent, Is.SameAs(holder));
                Assert.That(slots[i].transform.GetSiblingIndex(), Is.EqualTo(i));
                Assert.That(slots[i].GetType().GetProperty("Position").GetValue(slots[i]), Is.EqualTo((short)i));
                Assert.That(slots[i].GetType().GetProperty("Team").GetValue(slots[i]), Is.EqualTo(Team("Neutral")));
                Call(slots[i], "ResetName");
                Call(slots[i], "ResetName");
            }
            Assert.That(holder.childCount, Is.EqualTo(SpectatorSlotRules.Capacity), "Refreshing seats must reuse the authored slots.");
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase(10, true, 15)] [TestCase(6, true, 11)] [TestCase(20, true, 25)] [TestCase(5, false, 5)]
    public void ObserverCapacityDoesNotResizeTeams(int combatants, bool custom, int expected) =>
        Assert.That(SpectatorSlotRules.SessionCapacity(combatants, custom), Is.EqualTo(expected));

    [TestCase("Spectator", true)] [TestCase("Blue", false)] [TestCase("Neutral", false)] [TestCase("Green", false)]
    public void ObserverIsAnExplicitRoleNotNeutralOrBlue(string team, bool expected) => Assert.That(Player("p", team).IsSpectator, Is.EqualTo(expected));

    [Test]
    public void ObserverRoleConsumesNoAdditionalServiceProperties()
    {
        var player = Player("p", "Blue");
        int before = player.Data.Count;
        player.Data["PlayerTeam"] = new PlayerProperty(SpectatorSlotRules.TeamValue);
        Assert.That(player.IsSpectator, Is.True);
        Assert.That(player.Data.Count, Is.EqualTo(before));
    }

    [Test]
    public void CombatantEnumerationAndResultPerspectiveAreSeparateFromObserverIdentity()
    {
        view.Players.Add(Player("blue", "Blue"));
        view.Players.Add(Player("orange", "Orange"));
        view.Players.Add(Player("observer", SpectatorSlotRules.TeamValue));
        var combatants = ((IEnumerable<GameSessionPlayer>)lobby.GetType().GetProperty("Combatants").GetValue(lobby)).ToArray();
        Assert.That(combatants.Select(p => p.Id), Is.EqualTo(new[] { "blue", "orange" }));
        Assert.That(lobby.GetType().GetProperty("ResultsPerspectiveTeam").GetValue(lobby), Is.EqualTo(Team("Blue")));
        Assert.That(Call(lobby, "GetLocalPlayerTeam"), Is.EqualTo(Team("Neutral")));
        Assert.That(((Array)Call(lobby, "GetTeamPlayers", Team("Neutral"))).Length, Is.Zero);
    }

    [Test]
    public void EmptyObserverOnlyMatchCannotStartButValidBotCombatantsCan()
    {
        view.Players.Add(Player("observer", SpectatorSlotRules.TeamValue));
        Assert.That(Call(lobby, "ValidateCombatantRosterForStart"), Is.False);
        var bot = new GameSessionPlayer("bot-1", true);
        bot.Apply(Player("bot", "Blue").Data); view.Players.Add(bot);
        Assert.That(Call(lobby, "ValidateCombatantRosterForStart"), Is.True);
        Set(map, "startRestriction", Enum.Parse(T("StartRestriction"), "FullTeams"));
        Assert.That(Call(lobby, "ValidateCombatantRosterForStart"), Is.False);
    }

    [Test]
    public void SeatsRejectOutOfMapCombatantSlotsButRetainObserverSlotFour()
    {
        var coordinator = root.AddComponent(T("PlayerNetworkManager"));
        var seat = Activator.CreateInstance(T("LobbySeat"));
        Set(seat, "PlayerId", new FixedString64Bytes("p")); Set(seat, "Team", Team("Green"));
        Set(seat, "Position", (short)4); Set(seat, "Spectator", false);
        Assert.That(Call(coordinator, "ValidSeat", seat, view), Is.False);
        Set(seat, "Spectator", true);
        Assert.That(Call(coordinator, "ValidSeat", seat, view), Is.True);
        Set(map, "maxTeamSize", 1);
        Assert.That(Call(coordinator, "ValidSeat", seat, view), Is.True);
        Set(seat, "Position", (short)5);
        Assert.That(Call(coordinator, "ValidSeat", seat, view), Is.False);
    }

    [Test]
    public void ObserverSeatCollisionsAndSelfReselectionAreArbitrated()
    {
        var coordinator = root.AddComponent(T("PlayerNetworkManager"));
        object seat = Activator.CreateInstance(T("LobbySeat"));
        Set(seat, "PlayerId", new FixedString64Bytes("first")); Set(seat, "Position", (short)0); Set(seat, "Spectator", true);
        Call(Get(coordinator, "lobbySeats"), "Add", seat);
        Assert.That(Call(coordinator, "SeatFree", seat, view), Is.True);
        Set(seat, "PlayerId", new FixedString64Bytes("second"));
        Assert.That(Call(coordinator, "SeatFree", seat, view), Is.False);
        Set(seat, "Position", (short)1);
        Assert.That(Call(coordinator, "SeatFree", seat, view), Is.True);
        typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(coordinator, true);
        Call(coordinator, "FreezeLobbySeats");
        Assert.That(Get(coordinator, "seatsFrozen"), Is.True);
    }

    [Test]
    public void ObsoleteBotSlotsDoNotDisplaceCombatantsDuringMapResize()
    {
        var coordinator = root.AddComponent(T("PlayerNetworkManager"));
        var bot = Activator.CreateInstance(T("BotParticipant"));
        Set(bot, "Id", new FixedString32Bytes("bot-1")); Set(bot, "Team", Team("Blue")); Set(bot, "Position", (short)0);
        Call(Get(coordinator, "bots"), "Add", bot);
        var seat = Activator.CreateInstance(T("LobbySeat"));
        Set(seat, "PlayerId", new FixedString64Bytes("human")); Set(seat, "Team", Team("Blue")); Set(seat, "Position", (short)0);
        Set(coordinator, "previousMap", view.Data["SelectedMap"].Value);
        Assert.That(Call(coordinator, "SeatFree", seat, view), Is.False);
        Set(coordinator, "previousMap", "previous-map");
        Assert.That(Call(coordinator, "SeatFree", seat, view), Is.True);
    }

    [Test]
    public void SpectatorOwnerNeverAttemptsAvatarSpawnOnInitialising()
    {
        var player = Player("observer", SpectatorSlotRules.TeamValue); view.Players.Add(player);
        var coordinator = root.AddComponent(T("PlayerNetworkManager"));
        ((NetworkVariable<FixedString32Bytes>)Get(coordinator, "lobbyPlayerId")).Value = new FixedString32Bytes(player.Id);
        typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(coordinator, true);
        Assert.That(coordinator.GetType().GetProperty("IsSpectator").GetValue(coordinator), Is.True);
        Assert.DoesNotThrow(() => Call(coordinator, "HandleGameStateChanged", Enum.Parse(T("GameState"), "Initialising")));
        Assert.That(coordinator.GetType().GetProperty("Player").GetValue(coordinator), Is.Null);
    }

    [Test]
    public void SeatIdentityRoundTripsThroughNetcode()
    {
        var type = T("LobbySeat"); var value = Activator.CreateInstance(type);
        Set(value, "PlayerId", new FixedString64Bytes("observer-auth-id")); Set(value, "Position", (short)4); Set(value, "Spectator", true);
        using (var writer = new FastBufferWriter(256, Allocator.Temp))
        {
            writer.WriteNetworkSerializable((INetworkSerializable)value);
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                var method = typeof(FastBufferReader).GetMethods().Single(info => info.Name == "ReadNetworkSerializable" &&
                    info.GetParameters().Length == 1 && info.GetParameters()[0].ParameterType.GetElementType().IsGenericParameter);
                object[] args = { null }; method.MakeGenericMethod(type).Invoke(reader, args);
                Assert.That(args[0], Is.EqualTo(value));
            }
        }
    }

    [Test]
    public void SpectatorRenderingDoesNotChangeCombatVisibility()
    {
        var node = new GameObject("Invisible enemy"); node.transform.SetParent(root.transform);
        var vision = node.AddComponent(T("Vision"));
        Set(vision, "currentTeam", Team("Orange")); Set(vision, "isVisiblyEligible", false);
        var visual = new GameObject("Model"); visual.transform.SetParent(node.transform);
        Call(vision, "AddObject", visual);
        var manager = root.AddComponent(T("VisionManager"));
        Call(manager, "RegisterVision", vision);
        Assert.That(Call(manager, "IsVisibleToTeam", vision, Team("Blue")), Is.False);
        // Visibility requires an enabled scene object, not an active fixture.
        node.transform.SetParent(null); node.SetActive(true);
        try
        {
            Call(manager, "UpdateVisibility");
            Assert.That(vision.GetType().GetProperty("IsRendered").GetValue(vision), Is.True);
            Assert.That(Call(manager, "IsVisibleToTeam", vision, Team("Blue")), Is.False);
        }
        finally { Object.DestroyImmediate(node); }
    }

    [TestCase("RemoatStadium", true)] [TestCase("TheiaSkyRuins", true)]
    [TestCase("PsyduckRacing", false)] [TestCase("UniteRoyale", false)] [TestCase("ElectrodeVolleyball", false)]
    public void MapScoreTimerDefaultsAreExplicit(string name, bool expected)
    {
        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Maps/" + name + ".asset");
        Assert.That(Get(asset, "spectatorScoreTimerOverride"), Is.EqualTo(expected));
    }

    [Test]
    public void SelectionCardRefreshesBuildFromReplacementBotSnapshot()
    {
        var heldItem = AssetDatabase.LoadMainAssetAtPath("Assets/HeldItems/EmptyHeldItem.asset");
        var items = Array.CreateInstance(T("HeldItemInfo"), 1);
        items.SetValue(heldItem, 0);
        Set(characters, "heldItems", items);
        var battle = AssetDatabase.FindAssets("t:BattleItemAsset")
            .Select(guid => AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)))
            .First(asset => Get(asset, "icon") != null);
        var battles = Array.CreateInstance(T("BattleItemAsset"), 1);
        battles.SetValue(battle, 0);
        Set(characters, "battleItems", battles);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Spectator/SpectatorSelectionCard.prefab");
        var card = Object.Instantiate(prefab, root.transform).GetComponent(T("SpectatorSelectionCard"));
        var original = new GameSessionPlayer("bot-1", true);
        original.Data["HeldItems"] = new PlayerProperty("");
        original.Data["BattleItem"] = new PlayerProperty("-1");
        Set(card, "player", original);
        view.Players.Add(original);
        Call(card, "Refresh", (short)-1, "Choosing…", false, true);
        var image = (Component)Get(card, "battleItem");
        Assert.That(image.GetType().GetProperty("enabled").GetValue(image), Is.False);

        var updated = new GameSessionPlayer(original.Id, true);
        updated.Data["HeldItems"] = new PlayerProperty(Convert.ToBase64String(new byte[] { 0, 0, 0 }));
        updated.Data["BattleItem"] = new PlayerProperty("0");
        view.Players.Clear();
        view.Players.Add(updated);
        Call(card, "Refresh", (short)-1, "", false, true);
        Assert.That(Get(card, "lastItems"), Is.EqualTo(updated.Data["HeldItems"].Value));
        Assert.That(image.GetType().GetProperty("sprite").GetValue(image), Is.SameAs(Get(battle, "icon")));
        Assert.That(image.GetType().GetProperty("enabled").GetValue(image), Is.True);

        updated.Data["BattleItem"] = new PlayerProperty("invalid");
        Call(card, "Refresh", (short)-1, "", false, true);
        Assert.That(image.GetType().GetProperty("sprite").GetValue(image), Is.Null);
        Assert.That(image.GetType().GetProperty("enabled").GetValue(image), Is.False);
    }

    [Test]
    public void SelectionCardsKeepLoadingScreenProportionsInEveryGridSize()
    {
        var loading = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LoadingScreen/LoadingScreenPlayer.prefab");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Spectator/SpectatorSelectionCard.prefab");
        var authored = (RectTransform)prefab.transform;
        var loadingRect = (RectTransform)loading.transform;
        Assert.That(authored.sizeDelta.x / authored.sizeDelta.y, Is.EqualTo(loadingRect.sizeDelta.x / loadingRect.sizeDelta.y).Within(0.001f));
        Assert.That(authored.anchorMin, Is.EqualTo(authored.anchorMax));
        var container = new GameObject("Selection grid", typeof(RectTransform)); container.transform.SetParent(root.transform);
        var parent = (RectTransform)container.transform;
        var card = Object.Instantiate(prefab, parent).GetComponent(T("SpectatorSelectionCard"));
        var rect = (RectTransform)card.transform;
        foreach (var grid in new[] { new Vector2Int(1, 1), new Vector2Int(2, 3), new Vector2Int(1, 6) })
        {
            Call(card, "PlaceInGrid", grid.x * grid.y - 1, grid.x, grid.y, 7f);
            foreach (var size in new[] { new Vector2(500, 800), new Vector2(180, 90), new Vector2(1200, 400) })
            {
                parent.sizeDelta = size;
                Call(card, "LateUpdate");
                Assert.That(rect.rect.size, Is.EqualTo(authored.sizeDelta));
                Assert.That(rect.anchorMin, Is.EqualTo(rect.anchorMax));
                Assert.That(rect.pivot, Is.EqualTo(Vector2.one * 0.5f));
                Assert.That(rect.localScale.x, Is.EqualTo(rect.localScale.y));
                Assert.That(rect.localScale.x, Is.InRange(0, 1));
                Assert.That(rect.rect.width * rect.localScale.x, Is.LessThanOrEqualTo(size.x / grid.x - 14 + 0.01f));
                Assert.That(rect.rect.height * rect.localScale.y, Is.LessThanOrEqualTo(size.y / grid.y - 14 + 0.01f));
            }
        }
    }

    [Test]
    public void PlayerCardKeepsPrefabDimensionsWhenItsContainerResizes()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Spectator/SpectatorPlayerCard.prefab");
        var authored = (RectTransform)prefab.transform;
        Assert.That(authored.anchorMin, Is.EqualTo(Vector2.one * 0.5f));
        Assert.That(authored.anchorMax, Is.EqualTo(authored.anchorMin));
        var container = new GameObject("Card container", typeof(RectTransform)); container.transform.SetParent(root.transform);
        var parent = (RectTransform)container.transform;
        var instance = Object.Instantiate(prefab, parent);
        var card = instance.GetComponent(T("SpectatorPlayerCard"));
        var rect = (RectTransform)instance.transform;
        foreach (var size in new[] { new Vector2(240, 220), new Vector2(1600, 700) })
        {
            parent.sizeDelta = size;
            foreach (int count in new[] { 1, 5, 10 })
            {
                Call(card, "PlaceInRoster", 0, count, 12f);
                Assert.That(rect.rect.size, Is.EqualTo(authored.sizeDelta));
                Assert.That(rect.pivot, Is.EqualTo(Vector2.one * 0.5f));
                Assert.That(rect.anchorMin, Is.EqualTo(rect.anchorMax));
                Assert.That(rect.anchoredPosition.x, Is.EqualTo(-(count - 1) * 0.5f * (authored.sizeDelta.x + 12)));
            }
        }
    }

    [TestCase("BlueVersusOthers")]
    [TestCase("ProportionalTeams")]
    [TestCase("EqualTeams")]
    public void UnevenRostersScrollInsteadOfStretchingCards(string layout)
    {
        var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Spectator/SpectatorBattleHUD.prefab");
        var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Spectator/SpectatorTeamPanel.prefab");
        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Spectator/SpectatorPlayerCard.prefab");
        var hud = Object.Instantiate(hudPrefab, root.transform).GetComponent(T("SpectatorBattleHUD"));
        var panels = (IList)Get(hud, "panels");
        var groups = (IList)Get(hud, "groupedCards");
        var content = (RectTransform)Get(hud, "rosterContent");
        var viewport = (RectTransform)Get(hud, "rosterViewport");
        foreach (int count in new[] { 1, 5 })
        {
            var panel = Object.Instantiate(panelPrefab, content).GetComponent(T("SpectatorTeamPanel"));
            panels.Add(panel);
            var group = (IList)Activator.CreateInstance(groups.GetType().GetGenericArguments()[0]);
            for (int i = 0; i < count; i++)
                group.Add(Object.Instantiate(cardPrefab, (RectTransform)Get(panel, "content")).GetComponent(T("SpectatorPlayerCard")));
            groups.Add(group);
        }
        viewport.anchorMin = viewport.anchorMax = Vector2.one * 0.5f;
        foreach (float width in new[] { 320f, 1800f })
        {
            viewport.sizeDelta = new Vector2(width, 240);
            Call(hud, "LayoutRoster", Enum.Parse(T("SpectatorTeamLayout"), layout));
            Assert.That(content.rect.width, Is.GreaterThanOrEqualTo(width));
            for (int i = 0; i < groups.Count; i++)
            {
                var group = (IList)groups[i];
                var panelContent = (RectTransform)Get(panels[i], "content");
                float needed = group.Count * ((RectTransform)cardPrefab.transform).sizeDelta.x + (group.Count - 1) * 12;
                Assert.That(panelContent.rect.width, Is.GreaterThanOrEqualTo(needed));
                foreach (Component card in group)
                    Assert.That(((RectTransform)card.transform).rect.size, Is.EqualTo(((RectTransform)cardPrefab.transform).sizeDelta));
            }
        }
    }

    [TestCase("Blue", "BlueVersusOthers")]
    [TestCase("Orange", "BlueVersusOthers")]
    [TestCase("Green", "BlueVersusOthers")]
    [TestCase("Blue", "ProportionalTeams")]
    [TestCase("Orange", "ProportionalTeams")]
    [TestCase("Green", "ProportionalTeams")]
    public void MonochromeBackgroundIsReusedAcrossAllDisplayedTeams(string team, string layout)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GenericUI/SpectatorMode/t_observe_switch_player_bg_monochrome.png");
        Assert.That(sprite, Is.Not.Null);
        var settings = ScriptableObject.CreateInstance(T("SpectatorUIAssets"));
        try
        {
            Set(settings, "neutralCardBackground", sprite);
            var method = settings.GetType().GetMethod("GetBackground", new[] { T("Team"), T("SpectatorTeamLayout") });
            var mode = Enum.Parse(T("SpectatorTeamLayout"), layout);
            Assert.That(method.Invoke(settings, new[] { Team(team), mode }), Is.SameAs(sprite));
            var tint = (Color)Call(settings, "GetColor", Team(team), mode);
            Assert.That(tint, Is.Not.EqualTo(Color.white));
            if (layout == "BlueVersusOthers" && team != "Blue") Assert.That(tint, Is.EqualTo(Get(settings, "orangeColor")));
        }
        finally { Object.DestroyImmediate(settings); }
    }

    [Test]
    public void UnstartedSpectatorQuickChatCanBeDestroyedWithoutAnInputManager()
    {
        var selector = root.AddComponent(T("QuickChatSelectorUI"));
        var buttonType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.UI.Button")).First(type => type != null);
        var buttonObject = new GameObject("Quick chat button"); buttonObject.transform.SetParent(root.transform);
        Set(selector, "button", buttonObject.AddComponent(buttonType));
        Assert.DoesNotThrow(() => Call(selector, "OnDestroy"));
    }

    [Test]
    public void SpectatorsCannotSendQuickChatWithoutAPlayableAvatar()
    {
        var selector = root.AddComponent(T("QuickChatSelectorUI"));
        var panel = new GameObject("Quick chat panel"); panel.transform.SetParent(root.transform);
        Set(selector, "quickChatSelectorPanel", panel);
        Assert.DoesNotThrow(() => Call(selector, "OnQuickChatMessageSelected", "Defend"));
    }

    [Test]
    public void EditablePrefabsHaveNoMissingScriptsOrRequiredReferences()
    {
        var assets = Resources.Load("Spectator/SpectatorUIAssets");
        Assert.That(assets, Is.Not.Null);
        foreach (string field in new[] { "battleHUD", "selectionOverlay", "playerCard", "selectionCard", "teamPanel", "banSlot", "lobbySlot", "lobbySlots", "blueCardBackground", "orangeCardBackground", "neutralCardBackground" })
            Assert.That(Get(assets, field), Is.Not.Null, field);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Spectator" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero, child.name);
            foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c.GetType().Name.StartsWith("Spectator")))
            {
                var so = new SerializedObject(component); var iterator = so.GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        Assert.That(iterator.objectReferenceValue, Is.Not.Null, prefab.name + "/" + iterator.propertyPath);
            }
        }
    }
}
