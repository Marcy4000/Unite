using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class DisplaySettingsTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Type settingsType;
    private GameObject root;
    private Component manager;
    private TMP_Dropdown resolutionDropdown;
    private TMP_Dropdown frameRateDropdown;
    private Toggle vsyncToggle;
    private int previousVSync;
    private int previousFrameRate;
    private int previousQuality;

    [SetUp]
    public void SetUp()
    {
        settingsType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("SettingsManager")).First(type => type != null);
        previousVSync = QualitySettings.vSyncCount;
        previousFrameRate = Application.targetFrameRate;
        previousQuality = QualitySettings.GetQualityLevel();
        root = new GameObject("Display settings tests");
        root.SetActive(false);
        manager = root.AddComponent(settingsType);
        resolutionDropdown = CreateControl<TMP_Dropdown>("Resolution");
        frameRateDropdown = CreateControl<TMP_Dropdown>("Max framerate");
        var caption = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
        caption.transform.SetParent(frameRateDropdown.transform, false);
        frameRateDropdown.captionText = caption.GetComponent<TextMeshProUGUI>();
        vsyncToggle = CreateControl<Toggle>("VSync");
        SetField("resolutionDropdown", resolutionDropdown);
        SetField("frameRateDropdown", frameRateDropdown);
        SetField("vsyncToggle", vsyncToggle);
        SetField("fullscreenToggle", CreateControl<Toggle>("Fullscreen"));
        Invoke("InitializeFrameRateOptions");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        QualitySettings.SetQualityLevel(previousQuality);
        QualitySettings.vSyncCount = previousVSync;
        Application.targetFrameRate = previousFrameRate;
    }

    private Control CreateControl<Control>(string name) where Control : Component
    {
        var owner = new GameObject(name, typeof(RectTransform), typeof(Control));
        owner.transform.SetParent(root.transform, false);
        return owner.GetComponent<Control>();
    }

    private void SetField(string name, object value) => settingsType.GetField(name, Private).SetValue(manager, value);
    private void Invoke(string name, params object[] arguments) => settingsType
        .GetMethod(name, Private | BindingFlags.Public).Invoke(manager, arguments);
    private List<int> FrameRates => (List<int>)settingsType.GetField("frameRates", Private).GetValue(manager);

    [Test]
    public void ResolutionsAreSortedUniqueSizesWithoutRefreshRates()
    {
        var available = new[]
        {
            new Resolution { width = 1920, height = 1080, refreshRateRatio = new RefreshRate { numerator = 144, denominator = 1 } },
            new Resolution { width = 1280, height = 720 },
            new Resolution { width = 1920, height = 1080, refreshRateRatio = new RefreshRate { numerator = 60, denominator = 1 } }
        };
        int changes = 0;
        resolutionDropdown.onValueChanged.AddListener(value => changes++);
        Invoke("InitializeResolutionOptions", available, new Vector2Int(1600, 900));

        Assert.That(resolutionDropdown.options.Count, Is.EqualTo(3));
        Assert.That(resolutionDropdown.options[0].text, Does.StartWith("1280 × 720"));
        Assert.That(resolutionDropdown.options[1].text, Does.StartWith("1600 × 900"));
        Assert.That(resolutionDropdown.options[2].text, Does.StartWith("1920 × 1080"));
        Assert.That(resolutionDropdown.options.All(option => !option.text.Contains("Hz") && !option.text.Contains("@")), Is.True);
        Assert.That(resolutionDropdown.value, Is.EqualTo(1));
        Assert.That(changes, Is.Zero);
    }

    [Test]
    public void EmptyResolutionListStillShowsActualWindowSize()
    {
        Invoke("InitializeResolutionOptions", Array.Empty<Resolution>(), new Vector2Int(1234, 789));
        Assert.That(resolutionDropdown.options.Count, Is.EqualTo(1));
        Assert.That(resolutionDropdown.options[0].text, Does.StartWith("1234 × 789"));
        Assert.That(resolutionDropdown.value, Is.Zero);
    }

    [Test]
    public void VSyncDisablesCapAndRestoresItWhenDisabled()
    {
        QualitySettings.vSyncCount = 0;
        Invoke("SetFrameRate", FrameRates.IndexOf(120));
        Assert.That(Application.targetFrameRate, Is.EqualTo(120));

        Invoke("SetVSync", true);
        Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
        Assert.That(vsyncToggle.isOn, Is.True);
        Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
        Assert.That(frameRateDropdown.interactable, Is.False);
        Assert.That(frameRateDropdown.captionText.text, Does.StartWith("VSync ("));
        Invoke("SetFrameRate", FrameRates.IndexOf(30));

        Invoke("SetVSync", false);
        Assert.That(frameRateDropdown.interactable, Is.True);
        Assert.That(vsyncToggle.isOn, Is.False);
        Assert.That(Application.targetFrameRate, Is.EqualTo(120));
        Assert.That(frameRateDropdown.captionText.text, Is.EqualTo("120 FPS"));
    }

    [Test]
    public void UnlimitedAndCustomCapsAreIndependentOfResolutions()
    {
        QualitySettings.vSyncCount = 0;
        SetField("selectedFrameRate", 47);
        Invoke("InitializeFrameRateOptions");
        Assert.That(FrameRates.Count(rate => rate == 47), Is.EqualTo(1));
        Invoke("SetFrameRate", FrameRates.IndexOf(47));
        Invoke("InitializeResolutionOptions", Array.Empty<Resolution>(), new Vector2Int(Screen.width, Screen.height));
        Invoke("SetResolution", 0);
        Assert.That(Application.targetFrameRate, Is.EqualTo(47));
        Invoke("SetFrameRate", 0);
        Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
        Assert.That(frameRateDropdown.captionText.text, Is.EqualTo("Unlimited"));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void QualityChangesPreserveDisplayPreferences(int vSyncCount)
    {
        SetField("selectedFrameRate", 90);
        QualitySettings.vSyncCount = vSyncCount;
        Invoke("SetQuality", (previousQuality + 1) % QualitySettings.names.Length);
        Assert.That(QualitySettings.vSyncCount, Is.EqualTo(vSyncCount));
        Assert.That(Application.targetFrameRate, Is.EqualTo(vSyncCount > 0 ? -1 : 90));
        Assert.That(vsyncToggle.isOn, Is.EqualTo(vSyncCount > 0));
    }

    [Test]
    public void RefreshingControlsDoesNotFireCallbacks()
    {
        int changes = 0;
        frameRateDropdown.onValueChanged.AddListener(value => changes++);
        vsyncToggle.onValueChanged.AddListener(value => changes++);
        QualitySettings.vSyncCount = 2;
        Invoke("RefreshDisplayControls");
        Assert.That(vsyncToggle.isOn, Is.True);
        Assert.That(changes, Is.Zero);
    }

    [Test]
    public void InvalidOptionIndicesDoNotChangeFrameRate()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 47;
        Invoke("SetFrameRate", -1);
        Invoke("SetFrameRate", int.MaxValue);
        Invoke("SetResolution", -1);
        Invoke("SetResolution", int.MaxValue);
        Assert.That(Application.targetFrameRate, Is.EqualTo(47));
    }

    [Test]
    public void BootSceneAuthorsSeparateAdjacentDisplayControls()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/BootScene.unity");
        try
        {
            var authored = scene.GetRootGameObjects()
                .SelectMany(owner => owner.GetComponentsInChildren(settingsType, true)).Single();
            var resolution = (TMP_Dropdown)settingsType.GetField("resolutionDropdown", Private).GetValue(authored);
            var frameRate = (TMP_Dropdown)settingsType.GetField("frameRateDropdown", Private).GetValue(authored);
            var fullscreen = (Toggle)settingsType.GetField("fullscreenToggle", Private).GetValue(authored);
            var vsync = (Toggle)settingsType.GetField("vsyncToggle", Private).GetValue(authored);
            Assert.That(frameRate, Is.Not.Null);
            Assert.That(frameRate, Is.Not.SameAs(resolution));
            Assert.That(frameRate.transform.parent.parent, Is.SameAs(resolution.transform.parent.parent));
            Assert.That(resolution.transform.parent.GetSiblingIndex(), Is.EqualTo(fullscreen.transform.parent.GetSiblingIndex() + 1));
            Assert.That(vsync.transform.parent.GetSiblingIndex(), Is.EqualTo(resolution.transform.parent.GetSiblingIndex() + 1));
            Assert.That(frameRate.transform.parent.GetSiblingIndex(), Is.EqualTo(vsync.transform.parent.GetSiblingIndex() + 1));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
