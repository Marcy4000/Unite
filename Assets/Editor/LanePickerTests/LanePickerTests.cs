using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class LanePickerTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string PrefabPath = "Assets/Prefabs/CharacterSelect/PathPickerPrefab.prefab";
    private GameObject instance;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Get(object target, string field) => target.GetType().GetField(field, Fields).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    private static ScriptableObject Layout(string name = "Remoat") => AssetDatabase.LoadAssetAtPath<ScriptableObject>(
        "Assets/Maps/LaneLayouts/" + name + "LaneLayout.asset");
    private static object Roster() => Activator.CreateInstance(GameType("LaneSelectionRoster"));
    private static string[] Occupants(object roster, string lane) => ((IEnumerable)roster.GetType().GetProperty("Choices").GetValue(roster))
        .Cast<object>().Where(choice => (string)Get(choice, "LaneId") == lane).Select(choice => (string)Get(choice, "PlayerId")).ToArray();

    [TearDown]
    public void TearDown()
    {
        if (instance != null) Object.DestroyImmediate(instance);
    }

    [Test]
    public void LaneArrivalOrderIsIndependentOfRosterAndPokemonSelection()
    {
        var roster = Roster();
        Call(roster, "SetLane", "third-in-roster", "top");
        Call(roster, "SetLane", "first-in-roster", "top");
        Call(roster, "SetLane", "second-in-roster", "mid");
        Assert.That(Occupants(roster, "top"), Is.EqualTo(new[] { "third-in-roster", "first-in-roster" }));
    }

    [Test]
    public void LeavingAndReturningJoinsAtEndAndDoesNotMoveOtherPlayers()
    {
        var roster = Roster();
        foreach (string id in new[] { "a", "b", "c" }) Call(roster, "SetLane", id, "top");
        Call(roster, "SetLane", "a", "mid");
        Call(roster, "SetLane", "a", "top");
        Assert.That(Occupants(roster, "top"), Is.EqualTo(new[] { "b", "c", "a" }));
        Assert.That(Occupants(roster, "mid"), Is.Empty);
        Call(roster, "SetLane", "c", "");
        Call(roster, "SetLane", "c", "top");
        Assert.That(Occupants(roster, "top"), Is.EqualTo(new[] { "b", "a", "c" }));
    }

    [Test]
    public void RepeatedAssignmentsDoNotReorderAndNoLaneIsAllowed()
    {
        var roster = Roster();
        Call(roster, "SetLane", "a", "bottom");
        Call(roster, "SetLane", "b", "bottom");
        Assert.That(Call(roster, "SetLane", "a", "bottom"), Is.False);
        Assert.That(Occupants(roster, "bottom"), Is.EqualTo(new[] { "a", "b" }));
        Call(roster, "SetLane", "a", "");
        Assert.That(Call(roster, "GetLane", "a"), Is.EqualTo(""));
        Assert.That(Occupants(roster, "bottom"), Is.EqualTo(new[] { "b" }));
    }

    [Test]
    public void FullTeamCanChooseSameLaneAndResetClearsAllChoices()
    {
        var roster = Roster();
        for (int i = 0; i < 5; i++) Call(roster, "SetLane", i.ToString(), "mid");
        Assert.That(Occupants(roster, "mid").Length, Is.EqualTo(5));
        Call(roster, "Clear");
        Assert.That(Occupants(roster, "mid"), Is.Empty);
        Assert.That(Call(roster, "GetLane", "0"), Is.EqualTo(""));
    }

    [Test]
    public void SnapshotsReplaceOldChoicesWithoutReordering()
    {
        var roster = Roster();
        Call(roster, "SetLane", "disconnected", "top");
        Call(roster, "Replace", new[] { "b", "a", "c" }, new[] { "mid", "top", "mid" });
        Assert.That(Occupants(roster, "mid"), Is.EqualTo(new[] { "b", "c" }));
        Assert.That(Call(roster, "GetLane", "disconnected"), Is.EqualTo(""));
    }

    [Test]
    public void DisconnectRemovalPreservesRemainingArrivalOrder()
    {
        var roster = Roster();
        foreach (string id in new[] { "a", "disconnected", "b" }) Call(roster, "SetLane", id, "top");
        Type choiceType = GameType("LaneSelectionRoster+Choice");
        var choice = Expression.Parameter(choiceType, "choice");
        var predicate = Expression.Lambda(typeof(Predicate<>).MakeGenericType(choiceType),
            Expression.Equal(Expression.Field(choice, "PlayerId"), Expression.Constant("disconnected")), choice).Compile();
        Assert.That(Call(roster, "RemoveWhere", predicate), Is.True);
        Assert.That(Occupants(roster, "top"), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void SnapshotSurvivesActualNetcodeSerialization()
    {
        Type type = GameType("LaneSelectionSnapshot");
        var snapshot = Activator.CreateInstance(type);
        Set(snapshot, "PlayerIds", new[] { "player-b", "player-a" });
        Set(snapshot, "LaneIds", new[] { "top", "custom-lane" });
        using (var writer = new FastBufferWriter(1024, Allocator.Temp))
        {
            writer.WriteNetworkSerializable((INetworkSerializable)snapshot);
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                var method = typeof(FastBufferReader).GetMethods().Single(info => info.Name == "ReadNetworkSerializable" &&
                    info.GetParameters().Length == 1 && info.GetParameters()[0].ParameterType.GetElementType().IsGenericParameter);
                object[] args = { null };
                method.MakeGenericMethod(type).Invoke(reader, args);
                Assert.That(Get(args[0], "PlayerIds"), Is.EqualTo(Get(snapshot, "PlayerIds")));
                Assert.That(Get(args[0], "LaneIds"), Is.EqualTo(Get(snapshot, "LaneIds")));
            }
        }
    }

    [Test]
    public void SnapshotsRequireServerPermissionAndExplicitRecipients()
    {
        var rpc = GameType("LaneSelectionNetwork").GetMethod("ReceiveSnapshotRpc", Fields).GetCustomAttribute<RpcAttribute>();
        Assert.That(rpc.InvokePermission, Is.EqualTo(RpcInvokePermission.Server));
        // The default target is private in the SDK; verify its constructor metadata instead.
        var attribute = GameType("LaneSelectionNetwork").GetMethod("ReceiveSnapshotRpc", Fields)
            .GetCustomAttributesData().Single(data => data.AttributeType == typeof(RpcAttribute));
        Assert.That((SendTo)attribute.ConstructorArguments[0].Value, Is.EqualTo(SendTo.SpecifiedInParams));
    }

    [TestCase("RemoatStadium", true)]
    [TestCase("TheiaSkyRuins", true)]
    [TestCase("TheiaSkyRuinsDraft", true)]
    [TestCase("MerStadium", false)]
    [TestCase("MerStadiumDraft", false)]
    [TestCase("ElectrodeVolleyball", false)]
    [TestCase("UniteRoyale", false)]
    [TestCase("PsyduckRacing", false)]
    [TestCase("RemoatStadium500Points", false)]
    public void OnlySupportedMapsEnableLaneSelection(string asset, bool enabled)
    {
        var map = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Maps/" + asset + ".asset");
        Assert.That(map, Is.Not.Null);
        Assert.That(map.GetType().GetProperty("SupportsLaneSelection").GetValue(map), Is.EqualTo(enabled));
    }

    [Test]
    public void CustomPropertyDisablesEvenAnOtherwiseConfiguredMap()
    {
        var map = ScriptableObject.CreateInstance(GameType("MapInfo"));
        try
        {
            Set(map, "lanePickerLayout", Layout());
            Assert.That(map.GetType().GetProperty("SupportsLaneSelection").GetValue(map), Is.True);
            var property = Activator.CreateInstance(GameType("CustomMapProperties"));
            Set(property, "key", "disableLaneSelection");
            ((IList)Get(map, "customProperties")).Add(property);
            Assert.That(map.GetType().GetProperty("SupportsLaneSelection").GetValue(map), Is.False);
        }
        finally { Object.DestroyImmediate(map); }
    }

    [TestCase("Remoat")]
    [TestCase("Theia")]
    public void LayoutHasThreeFullyConfiguredDefaultLanes(string map)
    {
        var layout = Layout(map);
        Assert.That(Get(layout, "compactMap"), Is.Not.Null);
        Assert.That(Get(layout, "expandedMap"), Is.Not.Null);
        var lanes = (Array)Get(layout, "lanes");
        Assert.That(lanes.Cast<object>().Select(lane => Get(lane, "id")), Is.EqualTo(new[] { "top", "mid", "bottom" }));
        foreach (var lane in lanes)
        {
            Assert.That(Get(lane, "icon"), Is.Not.Null);
            foreach (string size in new[] { "compact", "expanded" })
            {
                var visual = Get(lane, size);
                Assert.That(Get(visual, "highlight"), Is.Not.Null);
                Assert.That(((Vector2)Get(visual, "highlightSize")).sqrMagnitude, Is.GreaterThan(0));
                Vector2 start = (Vector2)Call(visual, "GetMarkerPosition", 0, 5);
                Vector2 center = (Vector2)Call(visual, "GetMarkerPosition", 2, 5);
                Vector2 end = (Vector2)Call(visual, "GetMarkerPosition", 4, 5);
                Assert.That(start.x, Is.LessThan(center.x));
                Assert.That(center.x, Is.LessThan(end.x));
                if ((string)Get(lane, "id") == "mid") Assert.That(center.y, Is.EqualTo(start.y));
                else Assert.That(Mathf.Abs(center.y - (start.y + end.y) / 2f), Is.GreaterThan(5));
            }
        }
    }

    private Component CreateMapView(bool expanded)
    {
        instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        var views = instance.GetComponentsInChildren(GameType("LanePickerMapView"), true);
        var view = views.First(component => (bool)Get(component, "expanded") == expanded);
        Call(view, "Initialize", Layout(), 5);
        return view;
    }

    private static Array Markers(params (string id, string lane, Sprite portrait)[] players)
    {
        var type = GameType("LanePickerMapView+PlayerMarker");
        var markers = Array.CreateInstance(type, players.Length);
        for (int i = 0; i < players.Length; i++)
        {
            object marker = Activator.CreateInstance(type);
            Set(marker, "PlayerId", players[i].id);
            Set(marker, "LaneId", players[i].lane);
            Set(marker, "Portrait", players[i].portrait);
            markers.SetValue(marker, i);
        }
        return markers;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OnlyLocalLaneHighlightsAndClearingDisablesEveryHighlight(bool expanded)
    {
        var view = CreateMapView(expanded);
        var highlights = (IDictionary)Get(view, "highlights");
        foreach (string lane in new[] { "top", "mid", "bottom", "" })
        {
            Call(view, "Refresh", lane, "local", Markers());
            foreach (DictionaryEntry pair in highlights)
                Assert.That(((Component)pair.Value).gameObject.activeSelf, Is.EqualTo((string)pair.Key == lane));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UnpickedPokemonHaveNoMapMarkerAndPickingPreservesArrivalOrder(bool expanded)
    {
        var view = CreateMapView(expanded);
        var portrait = (Sprite)Get(((Array)Get(Layout(), "lanes")).GetValue(0), "icon");
        Call(view, "Refresh", "top", "local", Markers(("first", "top", null), ("second", "top", portrait)));
        var markers = (IDictionary)Get(view, "markers");
        Assert.That(markers.Contains("first"), Is.False);
        Assert.That(((RectTransform)markers["second"]).gameObject.activeSelf, Is.True);
        Call(view, "Refresh", "top", "local", Markers(("first", "top", portrait), ("second", "top", portrait)));
        Assert.That(((RectTransform)markers["first"]).anchoredPosition.x,
            Is.LessThan(((RectTransform)markers["second"]).anchoredPosition.x));
        Assert.That(((RectTransform)markers["first"]).localRotation, Is.EqualTo(Quaternion.identity));
    }

    [TestCase(false, "top")]
    [TestCase(false, "mid")]
    [TestCase(false, "bottom")]
    [TestCase(true, "top")]
    [TestCase(true, "mid")]
    [TestCase(true, "bottom")]
    public void EnlargedMarkersStillFitFivePlayersWithoutOverlapping(bool expanded, string lane)
    {
        var view = CreateMapView(expanded);
        var character = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Pokemons/PlayableMons/PokemonSelectInfo/Absol.asset");
        var portrait = (Sprite)Get(character, "circleIcon");
        Assert.That(portrait, Is.Not.Null);
        Call(view, "Refresh", lane, "0", Markers(Enumerable.Range(0, 5)
            .Select(i => (i.ToString(), lane, portrait)).ToArray()));
        var markers = (IDictionary)Get(view, "markers");
        Assert.That(markers.Count, Is.EqualTo(5));
        float scale = (float)Get(view, "markerScale");
        Assert.That(scale, Is.GreaterThan(expanded ? 1f : 0.85f));
        for (int i = 0; i < 5; i++)
        {
            var marker = (RectTransform)markers[i.ToString()];
            Assert.That(marker.localScale, Is.EqualTo(Vector3.one * scale));
            var image = marker.Find("PokemonIcon").GetComponent(GameType("UnityEngine.UI.Image"));
            Assert.That(image.GetType().GetProperty("sprite").GetValue(image), Is.EqualTo(portrait));
            if (i > 0)
                Assert.That(Vector2.Distance(((RectTransform)markers[(i - 1).ToString()]).anchoredPosition, marker.anchoredPosition),
                    Is.GreaterThanOrEqualTo(marker.rect.width * scale));
        }
    }

    [Test]
    public void CirclePortraitIsPreferredWithoutChangingNormalCardIconAndHasFallback()
    {
        var character = ScriptableObject.CreateInstance(GameType("CharacterInfo"));
        try
        {
            var normalIcon = (Sprite)Get(((Array)Get(Layout(), "lanes")).GetValue(0), "icon");
            var absol = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Pokemons/PlayableMons/PokemonSelectInfo/Absol.asset");
            var circleIcon = (Sprite)Get(absol, "circleIcon");
            Set(character, "icon", normalIcon);
            Assert.That(character.GetType().GetProperty("CircleIcon").GetValue(character), Is.EqualTo(normalIcon));
            Set(character, "circleIcon", circleIcon);
            Assert.That(character.GetType().GetProperty("CircleIcon").GetValue(character), Is.EqualTo(circleIcon));
            Assert.That(Get(character, "icon"), Is.EqualTo(normalIcon));
        }
        finally { Object.DestroyImmediate(character); }
    }

    [Test]
    public void AllCharacterInfoAssetsReferenceHighestEvolutionCirclePortraits()
    {
        var findPortrait = GameType("CharacterInfoPortraitSetup").GetMethod("FindHighestEvolutionCirclePortrait");
        var guids = AssetDatabase.FindAssets("t:CharacterInfo", new[] { "Assets" });
        Assert.That(guids, Is.Not.Empty);
        foreach (string guid in guids)
        {
            var character = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            var circle = (Sprite)Get(character, "circleIcon");
            Assert.That(circle, Is.Not.Null, character.name);
            Assert.That(circle, Is.EqualTo(findPortrait.Invoke(null, new object[] { character })), character.name);
            Assert.That(circle.name, Does.StartWith("t_Circle_"), character.name);
        }
    }

    [TestCase("Assets/Prefabs/CharacterSelect/PlayerSelectIcon.prefab", "PlayerSelectionIcon")]
    [TestCase("Assets/Prefabs/CharacterSelect/DraftMode/DraftPlayerBlue.prefab", "DraftPlayerIcon")]
    public void AlliedCardUsesSeparateAuthoredLaneBadge(string path, string cardType)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var card = prefab.GetComponent(GameType(cardType));
        var badge = (Component)Get(card, "laneBadge");
        Assert.That(badge, Is.Not.Null);
        Assert.That(((Component)Get(badge, "icon")).name, Is.EqualTo("MapIcon"));
    }

    [Test]
    public void PreparationPlacementUsesConfiguredPositionAndScale()
    {
        instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        var ui = instance.GetComponent(GameType("LanePickerUI"));
        Call(ui, "ShowPreparationLayout");
        var compact = (RectTransform)((GameObject)Get(ui, "compactRoot")).transform;
        Assert.That(compact.anchoredPosition, Is.EqualTo((Vector2)Get(ui, "preparationPosition")));
        Assert.That(compact.localScale.x, Is.EqualTo((float)Get(ui, "preparationScale")));
        Assert.That(((GameObject)Get(ui, "expandedRoot")).activeSelf, Is.False);
    }

    [TestCase("CharacterSelect", "CharacterSelectController")]
    [TestCase("DraftSelect", "DraftSelectController")]
    public void SceneHasOneSharedPickerOutsidePhaseSpecificRoots(string sceneName, string controllerType)
    {
        string path = "Assets/Scenes/" + sceneName + ".unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool loaded = scene.IsValid() && scene.isLoaded;
        if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var pickers = components.Select(t => t.GetComponent(GameType("LanePickerUI"))).Where(c => c != null).ToArray();
            Assert.That(pickers.Length, Is.EqualTo(1));
            Assert.That(pickers[0].transform.parent.name, Is.EqualTo("Canvas"));
            var controller = components.Select(t => t.GetComponent(GameType(controllerType))).First(c => c != null);
            var network = controller.GetComponent(GameType("LaneSelectionNetwork"));
            Assert.That(network, Is.Not.Null);
            Assert.That(Get(network, "picker"), Is.EqualTo(pickers[0]));
            Assert.That(((GameObject)Get(pickers[0], "expandedRoot")).activeSelf, Is.False);
            Assert.That(Get(pickers[0], "usePreparationLayout"), Is.EqualTo(sceneName == "DraftSelect"));
        }
        finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
    }
}
