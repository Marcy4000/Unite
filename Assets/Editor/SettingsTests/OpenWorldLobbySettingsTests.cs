using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class OpenWorldLobbySettingsTests
{
    private const string PreferenceKey = "OpenWorldLobbyEnabled";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Type settingsType;
    private GameObject root;
    private Component manager;
    private bool hadPreference;
    private int previousPreference;
    private int previousVSync;
    private int previousTargetFrameRate;
    private readonly List<bool> changes = new List<bool>();
    private Action<bool> changed;

    private bool Enabled => (bool)settingsType.GetProperty("AutoJoinOpenWorldLobby").GetValue(null);
    private Toggle LobbyToggle => (Toggle)settingsType.GetField("openWorldLobbyToggle", Private).GetValue(manager);

    [SetUp]
    public void SetUp()
    {
        settingsType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("SettingsManager")).First(type => type != null);
        hadPreference = PlayerPrefs.HasKey(PreferenceKey);
        previousPreference = PlayerPrefs.GetInt(PreferenceKey);
        previousVSync = QualitySettings.vSyncCount;
        previousTargetFrameRate = Application.targetFrameRate;
        PlayerPrefs.DeleteKey(PreferenceKey);
        changes.Clear();
        changed = enabled =>
        {
            Assert.That(Enabled, Is.EqualTo(enabled));
            Assert.That(PlayerPrefs.GetInt(PreferenceKey), Is.EqualTo(enabled ? 1 : 0));
            changes.Add(enabled);
        };
        settingsType.GetEvent("OpenWorldLobbyChanged").AddEventHandler(null, changed);
        root = new GameObject("Settings tests", typeof(RectTransform));
    }

    [TearDown]
    public void TearDown()
    {
        settingsType.GetEvent("OpenWorldLobbyChanged").RemoveEventHandler(null, changed);
        Object.DestroyImmediate(root);
        if (hadPreference)
            PlayerPrefs.SetInt(PreferenceKey, previousPreference);
        else
            PlayerPrefs.DeleteKey(PreferenceKey);
        PlayerPrefs.Save();
        QualitySettings.vSyncCount = previousVSync;
        Application.targetFrameRate = previousTargetFrameRate;
    }

    [TestCase(0, null, false, false)]
    [TestCase(1, null, false, true)]
    [TestCase(0, false, false, true)]
    [TestCase(1, true, false, false)]
    [TestCase(1, null, true, false)]
    [TestCase(0, false, true, false)]
    public void HomeNavigationRespectsPreferenceUnlessExplicitlyOverridden(
        int preference, bool? staticMenu, bool hasSession, bool expected)
    {
        PlayerPrefs.SetInt(PreferenceKey, preference);
        var controllerType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("LobbyController")).First(type => type != null);
        var useOpenWorld = controllerType.GetMethod("ShouldUseOpenWorldLobby", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { staticMenu, hasSession });
        Assert.That(useOpenWorld, Is.EqualTo(expected));
    }

    private void CreateManager()
    {
        GameObject owner = new GameObject("Inactive settings owner");
        owner.transform.SetParent(root.transform, false);
        owner.SetActive(false);
        manager = owner.AddComponent(settingsType);
    }

    private void CreateAuthoredToggle()
    {
        CreateManager();
        GameObject row = new GameObject("OpenWorldLobby", typeof(RectTransform));
        row.transform.SetParent(root.transform, false);
        ((RectTransform)row.transform).sizeDelta = new Vector2(1608.1f, 155f);
        GameObject label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(row.transform, false);
        label.AddComponent<TextMeshProUGUI>().text = "Open world lobby";
        GameObject control = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
        control.transform.SetParent(row.transform, false);
        var toggle = control.GetComponent<Toggle>();
        toggle.targetGraphic = control.GetComponent<Image>();
        toggle.SetIsOnWithoutNotify(true);
        settingsType.GetField("openWorldLobbyToggle", Private).SetValue(manager, toggle);
    }

    private void InitializeToggle() => settingsType.GetMethod("InitializeOpenWorldLobbyToggle", Private).Invoke(manager, null);
    private void SetEnabled(bool enabled) => settingsType.GetMethod("SetOpenWorldLobby").Invoke(manager, new object[] { enabled });

    [Test]
    public void MissingPreferenceDefaultsOnWithoutSettingsInstanceOrStart()
    {
        Assert.That(Enabled, Is.True);
        Assert.That(PlayerPrefs.HasKey(PreferenceKey), Is.False);
        Assert.That(changes, Is.Empty);
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    public void SavedPreferenceIsAvailableBeforeStart(int value, bool expected)
    {
        PlayerPrefs.SetInt(PreferenceKey, value);
        Assert.That(Enabled, Is.EqualTo(expected));
    }

    [Test]
    public void SetterPersistsBeforeNotifyingAndOnlyNotifiesActualChanges()
    {
        CreateManager();
        SetEnabled(true);
        Assert.That(PlayerPrefs.HasKey(PreferenceKey), Is.True);
        Assert.That(changes, Is.Empty);
        SetEnabled(false);
        SetEnabled(false);
        SetEnabled(true);
        CollectionAssert.AreEqual(new[] { false, true }, changes);
        Assert.That(PlayerPrefs.GetInt(PreferenceKey), Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InitializationUsesAuthoredToggleWithoutChangingLayoutOrAppearance(bool interactable)
    {
        CreateAuthoredToggle();
        var authored = LobbyToggle;
        authored.interactable = interactable;
        var size = ((RectTransform)authored.transform.parent).sizeDelta;
        var colors = authored.colors;
        int objectCount = root.GetComponentsInChildren<Transform>(true).Length;
        InitializeToggle();
        Toggle lobby = LobbyToggle;
        Assert.That(lobby, Is.SameAs(authored));
        Assert.That(lobby.interactable, Is.EqualTo(interactable));
        Assert.That(lobby.isOn, Is.True);
        Assert.That(lobby.group, Is.Null);
        Assert.That(lobby.colors, Is.EqualTo(colors));
        Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
        Assert.That(((RectTransform)lobby.transform.parent).sizeDelta, Is.EqualTo(size));
        Assert.That(lobby.transform.parent.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Open world lobby"));
        Assert.That(changes, Is.Empty);
    }

    [Test]
    public void InitializationAndUserChangesPreserveDesignerCallbacksAndUnrelatedSettings()
    {
        CreateAuthoredToggle();
        var fullscreen = new GameObject("Fullscreen", typeof(RectTransform), typeof(Toggle));
        fullscreen.transform.SetParent(root.transform, false);
        var unrelated = fullscreen.GetComponent<Toggle>();
        ToggleGroup group = root.AddComponent<ToggleGroup>();
        unrelated.group = group;
        unrelated.SetIsOnWithoutNotify(true);
        int runtimeCalls = 0;
        unrelated.onValueChanged.AddListener(enabled => runtimeCalls++);
        UnityAction<bool> persistentCallback = (UnityAction<bool>)Delegate.CreateDelegate(
            typeof(UnityAction<bool>), manager, settingsType.GetMethod("SetVSync"));
        UnityEventTools.AddPersistentListener(unrelated.onValueChanged, persistentCallback);
        int designerCalls = 0;
        LobbyToggle.onValueChanged.AddListener(enabled => designerCalls++);
        PlayerPrefs.SetInt(PreferenceKey, 0);
        QualitySettings.vSyncCount = 2;
        int previousQuality = QualitySettings.GetQualityLevel();
        bool previousFullscreen = Screen.fullScreen;

        InitializeToggle();
        Assert.That(changes, Is.Empty);
        Assert.That(LobbyToggle.isOn, Is.False);
        Assert.That(LobbyToggle.onValueChanged.GetPersistentEventCount(), Is.Zero);
        LobbyToggle.isOn = true;
        LobbyToggle.isOn = false;

        Assert.That(QualitySettings.vSyncCount, Is.EqualTo(2));
        Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(previousQuality));
        Assert.That(Screen.fullScreen, Is.EqualTo(previousFullscreen));
        Assert.That(Application.targetFrameRate, Is.EqualTo(previousTargetFrameRate));
        Assert.That(unrelated.isOn, Is.True);
        Assert.That(unrelated.group, Is.SameAs(group));
        Assert.That(unrelated.onValueChanged.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(runtimeCalls, Is.Zero);
        Assert.That(designerCalls, Is.EqualTo(2));
        CollectionAssert.AreEqual(new[] { true, false }, changes);
    }

    [Test]
    public void UserTogglePersistsAndNotifiesWhileSettingsOwnerIsInactive()
    {
        CreateAuthoredToggle();
        InitializeToggle();
        LobbyToggle.isOn = false;
        Assert.That(Enabled, Is.False);
        Assert.That(PlayerPrefs.GetInt(PreferenceKey), Is.Zero);
        LobbyToggle.isOn = true;
        CollectionAssert.AreEqual(new[] { false, true }, changes);
    }

    [Test]
    public void ReinitializingDoesNotDuplicateRowOrListener()
    {
        CreateAuthoredToggle();
        InitializeToggle();
        Toggle original = LobbyToggle;
        int childCount = root.transform.childCount;
        InitializeToggle();
        Assert.That(LobbyToggle, Is.SameAs(original));
        Assert.That(root.transform.childCount, Is.EqualTo(childCount));
        LobbyToggle.isOn = false;
        CollectionAssert.AreEqual(new[] { false }, changes);
    }

    [Test]
    public void SetterSynchronizesToggleWithoutAdditionalUiCallbacks()
    {
        CreateAuthoredToggle();
        InitializeToggle();
        int uiCalls = 0;
        LobbyToggle.onValueChanged.AddListener(enabled => uiCalls++);
        SetEnabled(false);
        Assert.That(LobbyToggle.isOn, Is.False);
        Assert.That(uiCalls, Is.Zero);
        CollectionAssert.AreEqual(new[] { false }, changes);
    }

    [Test]
    public void DestroyHookRemovesItsToggleSubscription()
    {
        CreateAuthoredToggle();
        InitializeToggle();
        Toggle lobby = LobbyToggle;
        settingsType.GetMethod("OnDestroy", Private).Invoke(manager, null);
        lobby.isOn = false;
        Assert.That(PlayerPrefs.HasKey(PreferenceKey), Is.False);
        Assert.That(changes, Is.Empty);
    }

    [Test]
    public void MissingAuthoredToggleDoesNotCreateFallbackUi()
    {
        CreateManager();
        int objectCount = root.GetComponentsInChildren<Transform>(true).Length;
        InitializeToggle();
        Assert.That(LobbyToggle, Is.Null);
        Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BootSceneContainsEditableAutoJoinRowWithoutChangingQualityOrCreatingUi(bool autoJoin)
    {
        PlayerPrefs.SetInt(PreferenceKey, autoJoin ? 1 : 0);
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/BootScene.unity");
        try
        {
            manager = scene.GetRootGameObjects()
                .SelectMany(owner => owner.GetComponentsInChildren(settingsType, true)).Single();
            Toggle[] quality = (Toggle[])settingsType.GetField("qualityToggles", Private).GetValue(manager);
            bool[] previousStates = quality.Select(toggle => toggle.isOn).ToArray();
            var scroll = (ScrollRect)settingsType.GetField("settingsScrollRect", Private).GetValue(manager);
            int previousRows = scroll.content.childCount;
            Assert.That(LobbyToggle, Is.Not.Null);
            Assert.That(LobbyToggle.interactable, Is.True);
            Assert.That(LobbyToggle.group, Is.Null);

            InitializeToggle();

            Assert.That(LobbyToggle.transform.parent.parent, Is.SameAs(scroll.content));
            Assert.That(scroll.content.childCount, Is.EqualTo(previousRows));
            Assert.That(LobbyToggle.transform.parent.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Auto-join open world lobby on Start"));
            Assert.That(LobbyToggle.isOn, Is.EqualTo(autoJoin));
            CollectionAssert.AreEqual(previousStates, quality.Select(toggle => toggle.isOn).ToArray());
            Assert.That(changes, Is.Empty);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
