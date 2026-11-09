using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class OpenWorldLobbyUiTests
{
    private const string HudPath = "Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab";
    private const string HomePath = "Assets/Prefabs/Lobby/OpenWorldHomeButton.prefab";

    [Test]
    public void CityEnvironmentAndBakeBelongOnlyToAddressableMapScene()
    {
        const string home = "Assets/Scenes/ChineseLobby.unity";
        const string map = "Assets/Scenes/ChineseLobbyMap.unity";
        const string lighting = "Assets/Scenes/ChineseLobby/LightingData.asset";
        var homeDependencies = AssetDatabase.GetDependencies(home, true);
        var mapDependencies = AssetDatabase.GetDependencies(map, true);
        Assert.That(homeDependencies.Any(path => path.StartsWith("Assets/Scenes/ChineseLobby/") ||
            path.Contains("pref_sce_lobby_newcity_online_lv3")), Is.False);
        Assert.That(mapDependencies, Does.Contain(lighting));
        Assert.That(mapDependencies.Any(path => path.Contains("NavMesh-sce_lobby")), Is.True);
        var bake = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(lighting));
        Assert.That(AssetDatabase.GetAssetPath(bake.FindProperty("m_Scene").objectReferenceValue), Is.EqualTo(map));
        Assert.That(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == home), Is.True);
        Assert.That(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == map), Is.False);
    }

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [Test]
    public void CityHudContainsEditableLabelsButtonsMobileControlsAndInputModule()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        Assert.That(prefab, Is.Not.Null);
        var view = prefab.GetComponent(GameType("OpenWorldLobbyView"));
        var serialized = new SerializedObject(view);
        var status = (TMP_Text)serialized.FindProperty("status").objectReferenceValue;
        Assert.That(status, Is.Not.Null);
        Assert.That(status.transform.IsChildOf(prefab.transform), Is.True);
        Assert.That(status.text, Is.EqualTo("Connecting to city..."));
        Assert.That(prefab.GetComponentsInChildren<TMP_Text>(true).All(label => label.font != null), Is.True);
        var controls = (GameObject)serialized.FindProperty("mobileControls").objectReferenceValue;
        Assert.That(controls, Is.Not.Null);
        var stick = controls.GetComponentInChildren(GameType("UnityEngine.InputSystem.OnScreen.OnScreenStick"), true);
        Assert.That(stick, Is.Not.Null);
        Assert.That(stick.GetType().GetProperty("controlPath").GetValue(stick), Is.EqualTo("<Gamepad>/leftStick"));
        var events = (Component)serialized.FindProperty("lobbyEventSystem").objectReferenceValue;
        Assert.That(events, Is.Not.Null);
        var module = events.GetComponent(GameType("UnityEngine.InputSystem.UI.InputSystemUIInputModule"));
        Assert.That(module, Is.Not.Null);
        var actions = (UnityEngine.Object)module.GetType().GetProperty("actionsAsset").GetValue(module);
        Assert.That(EditorUtility.IsPersistent(actions), Is.True);
        var buttons = prefab.GetComponentsInChildren<Button>(true);
        Assert.That(buttons.Length, Is.GreaterThan(2));
        AssertClick(buttons.Single(button => button.name == "Settings"), view, "OpenSettings");
        AssertClick(buttons.Single(button => button.name == "Match menu"), view, "OpenMatchMenu");
    }

    [Test]
    public void EmoteMenuHasEmptyScrollableContentAndRuntimeButtonTemplate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        var menu = prefab.GetComponent(GameType("OpenWorldEmoteMenu"));
        Assert.That(menu, Is.Not.Null);
        var serialized = new SerializedObject(menu);
        var panel = (GameObject)serialized.FindProperty("panel").objectReferenceValue;
        Assert.That(panel, Is.Not.Null);
        Assert.That(panel.activeSelf, Is.False);
        var scroll = (ScrollRect)serialized.FindProperty("scroll").objectReferenceValue;
        Assert.That(scroll, Is.Not.Null);
        Assert.That(scroll.vertical, Is.True);
        Assert.That(scroll.horizontal, Is.False);
        Assert.That(scroll.content.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
        Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
        Assert.That(scroll.verticalScrollbar, Is.Not.Null);
        var catalog = serialized.FindProperty("catalog").objectReferenceValue;
        Assert.That(catalog, Is.Not.Null);
        Assert.That(scroll.content.childCount, Is.Zero);
        var buttonPrefab = (Button)serialized.FindProperty("buttonPrefab").objectReferenceValue;
        Assert.That(buttonPrefab, Is.Not.Null);
        Assert.That(AssetDatabase.Contains(buttonPrefab), Is.True);
        Assert.That(buttonPrefab.GetComponentInChildren<TMP_Text>(true), Is.Not.Null);
        Assert.That(buttonPrefab.onClick.GetPersistentEventCount(), Is.Zero);
        AssertClick((Button)serialized.FindProperty("openButton").objectReferenceValue, menu, "Toggle");
        AssertClick(panel.GetComponentsInChildren<Button>(true).Single(button => button.name == "Close emotes"), menu, "Close");
        var avatar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldTrainer.prefab");
        var emotes = avatar.GetComponent(GameType("OpenWorldTrainerEmotes"));
        Assert.That(new SerializedObject(emotes).FindProperty("catalog").objectReferenceValue, Is.EqualTo(catalog));
    }

    [Test]
    public void CityChatReusesAuthoredPanelAndHasEditableCallbacks()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        var manager = root.GetComponent(GameType("ChatManager"));
        Assert.That(manager, Is.Not.Null);
        var data = new SerializedObject(manager);
        foreach (string field in new[] { "messageRecieverPrefab", "chatMessagePrefabSelf", "chatMessagePrefabOther", "chatMessageContainer",
            "chatInputField", "globalChatToggle", "privateChatToggle", "privatePlayerDropdown", "unreadBadge", "unreadCountText",
            "messageNotification", "notificationSenderText", "notificationMessageText" })
            Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        var panel = (GameObject)data.FindProperty("chatContainer").objectReferenceValue;
        Assert.That(panel.name, Is.EqualTo("ChatHolder"));
        Assert.That(panel.activeSelf, Is.False);
        Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel), Is.EqualTo("Assets/Prefabs/Lobby/ChatHolder.prefab"));
        var buttons = root.GetComponentsInChildren<Button>(true);
        AssertClick(buttons.Single(button => button.name == "OpenChatButton"), manager, "OpenChat");
        AssertClick(buttons.Single(button => button.name == "CityChatNotification"), manager, "OpenChat");
        AssertClick(panel.GetComponentsInChildren<Button>(true).Single(button => button.name == "Arrow"), manager, "CloseChat");
        AssertClick(panel.GetComponentsInChildren<Button>(true).Single(button => button.name == "SendButton"), manager, "SendChatMessage");
        var input = (TMP_InputField)data.FindProperty("chatInputField").objectReferenceValue;
        Assert.That(input.onSubmit.GetPersistentTarget(0), Is.EqualTo(manager));
        Assert.That(input.onSubmit.GetPersistentMethodName(0), Is.EqualTo("SendChatMessage"));
    }

    [Test]
    public void HomeButtonHasSerializedEditableClickTarget()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomePath);
        Assert.That(prefab, Is.Not.Null);
        var home = prefab.GetComponent(GameType("OpenWorldHomeButton"));
        var button = (Button)new SerializedObject(home).FindProperty("button").objectReferenceValue;
        Assert.That(button, Is.Not.Null);
        Assert.That(button.transform.IsChildOf(prefab.transform), Is.True);
        AssertClick(button, home, "EnterCity");
    }

    [TestCase("Assets/Scenes/ChineseLobby.unity", "OpenWorldLobbyView", HudPath)]
    [TestCase("Assets/Scenes/LobbyScene.unity", "OpenWorldHomeButton", HomePath)]
    public void LobbyScenesContainAuthoredUiEitherInSceneOrPrefab(string path, string componentName, string prefabPath)
    {
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var component = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren(GameType(componentName), true)).Single();
            var source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component);
            if (!string.IsNullOrEmpty(source)) Assert.That(source, Is.EqualTo(prefabPath));
            Assert.That(component.GetComponentsInChildren<Canvas>(true), Is.Not.Empty);
            Assert.That(component.GetComponentsInChildren<Button>(true), Is.Not.Empty);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void StaticLobbyHasItsMenuControllerAndAllRequiredReferences()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
        try
        {
            var menus = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren(GameType("MainMenuUI"), true)).ToArray();
            Assert.That(menus.Length, Is.EqualTo(1), "LobbyScene requires its authored MainMenuUI controller.");
            var menu = menus[0];
            Assert.That(menu.gameObject.activeInHierarchy, Is.True);
            Assert.That(((Behaviour)menu).enabled, Is.True);
            var serialized = new SerializedObject(menu);
            foreach (var field in new[] { "startLobbyButton", "exitLobbyButton", "lobbyIdField", "sceneRoot",
                "partyScreenUI", "lobbyPlayerInfoIcons", "trainerModel", "matchmakingBarUI" })
                Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            foreach (var field in new[] { "lobbyUIs", "lobbyScenes" })
            {
                var references = serialized.FindProperty(field);
                Assert.That(references.arraySize, Is.EqualTo(field == "lobbyUIs" ? 7 : 2), field);
                for (int index = 0; index < references.arraySize; index++)
                    Assert.That(references.GetArrayElementAtIndex(index).objectReferenceValue, Is.Not.Null, field + "[" + index + "]");
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void StaticLobbyNavigationAndClothingCallbacksTargetItsActualController()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
        try
        {
            var components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).Where(component => component != null).ToArray();
            var menu = components.Single(component => component.GetType() == GameType("MainMenuUI"));
            int callbacks = 0;
            int backButtons = 0;
            foreach (var component in components)
            {
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.name != "m_TargetAssemblyTypeName" || property.stringValue != "MainMenuUI, Assembly-CSharp") continue;
                    var prefix = property.propertyPath.Substring(0, property.propertyPath.LastIndexOf('.') + 1);
                    Assert.That(serialized.FindProperty(prefix + "m_Target").objectReferenceValue, Is.EqualTo(menu), component.name);
                    var method = serialized.FindProperty(prefix + "m_MethodName").stringValue;
                    Assert.That(menu.GetType().GetMethod(method), Is.Not.Null, method);
                    callbacks++;
                    if (method == "ShowMainMenuUI") backButtons++;
                }
            }
            Assert.That(callbacks, Is.GreaterThanOrEqualTo(12));
            Assert.That(backButtons, Is.GreaterThanOrEqualTo(4));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void AssertClick(Button button, Component target, string method)
    {
        Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(button.onClick.GetPersistentTarget(0), Is.EqualTo(target));
        Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(method));
    }
}
