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
    private readonly Dictionary<string, int?> savedPreferences = new Dictionary<string, int?>();

    [SetUp]
    public void SetUp()
    {
        foreach (string key in new[] { "Graphics.Quality", "Display.TargetFrameRate", "Display.VSync", "Display.RefreshRate", "Graphics.RenderScale", "Graphics.CityAntiAliasing", "Graphics.CityPostProcessing", "Graphics.AntiAliasing", "Graphics.PostProcessing" })
        {
            savedPreferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            PlayerPrefs.DeleteKey(key);
        }
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
        var graphics = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("GraphicsPreferences")).First(t => t != null);
        graphics.GetMethod("ReleasePipeline").Invoke(null, null);
        foreach (var entry in savedPreferences)
        {
            if (entry.Value.HasValue) PlayerPrefs.SetInt(entry.Key, entry.Value.Value);
            else PlayerPrefs.DeleteKey(entry.Key);
        }
        PlayerPrefs.Save();
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
    public void FrameRateSelectionIsPersisted()
    {
        QualitySettings.vSyncCount = 0;
        Invoke("SetFrameRate", FrameRates.IndexOf(30));
        Assert.That(PlayerPrefs.GetInt("Display.TargetFrameRate"), Is.EqualTo(30));
        Invoke("SetQuality", (previousQuality + 1) % QualitySettings.names.Length);
        Assert.That(PlayerPrefs.GetInt("Display.TargetFrameRate"), Is.EqualTo(30));
        Assert.That(Application.targetFrameRate, Is.EqualTo(30));
    }

    [Test]
    public void GraphicsOverridesSurvivePresetChangesAndInvalidOptions()
    {
        Invoke("SetRenderScale", 2);
        Invoke("SetAntiAliasing", 1);
        Invoke("SetPostProcessing", 1);
        Invoke("SetQuality", (previousQuality + 1) % QualitySettings.names.Length);
        Invoke("SetRenderScale", -1);
        Invoke("SetAntiAliasing", 99);
        Assert.That(PlayerPrefs.GetInt("Graphics.RenderScale"), Is.EqualTo(2));
        Assert.That(PlayerPrefs.GetInt("Graphics.AntiAliasing"), Is.EqualTo(1));
        Assert.That(PlayerPrefs.GetInt("Graphics.PostProcessing"), Is.EqualTo(1));
        var pipeline = QualitySettings.renderPipeline;
        Assert.That(pipeline.name, Does.EndWith("(Player settings)"));
        Assert.That((float)pipeline.GetType().GetProperty("renderScale").GetValue(pipeline), Is.EqualTo(0.67f).Within(0.001f));
        Assert.That(UnityEditor.AssetDatabase.Contains(pipeline), Is.False);
    }

    [Test]
    public void GameCamerasUpdateImmediatelyAndDisableEffectsOnPerformantPreset()
    {
        var cameraObject = new GameObject("Game camera settings test", typeof(Camera));
        try
        {
            var policyType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CameraGraphicsSettings")).First(t => t != null);
            var dataType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData")).First(t => t != null);
            Invoke("SetQuality", 0);
            policyType.GetMethod("Enable").Invoke(null, null);
            var data = cameraObject.GetComponent(dataType);
            Assert.That(dataType.GetProperty("renderPostProcessing").GetValue(data), Is.False);
            Assert.That(dataType.GetProperty("antialiasing").GetValue(data).ToString(), Is.EqualTo("None"));
            Assert.That(cameraObject.GetComponent<Camera>().allowMSAA, Is.False);
            Invoke("SetPostProcessing", 1);
            Invoke("SetAntiAliasing", 3);
            Assert.That(dataType.GetProperty("renderPostProcessing").GetValue(data), Is.True, "AA needs URP post passes even with volume effects off");
            Assert.That(((LayerMask)dataType.GetProperty("volumeLayerMask").GetValue(data)).value, Is.Zero);
            Invoke("SetPostProcessing", 2);
            Assert.That(dataType.GetProperty("renderPostProcessing").GetValue(data), Is.True);
            Assert.That(dataType.GetProperty("antialiasing").GetValue(data).ToString(), Is.EqualTo("SubpixelMorphologicalAntiAliasing"));
            Invoke("SetQuality", 1);
            Assert.That(dataType.GetProperty("antialiasing").GetValue(data).ToString(), Is.EqualTo("SubpixelMorphologicalAntiAliasing"));
            Invoke("SetAntiAliasing", 0);
            Assert.That(dataType.GetProperty("antialiasing").GetValue(data).ToString(), Is.EqualTo("FastApproximateAntialiasing"));
        }
        finally
        {
            var policyType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CameraGraphicsSettings")).First(t => t != null);
            policyType.GetMethod("Disable").Invoke(null, null);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void NewSceneCamerasReceivePreferencesWhileUiAndTextureCamerasKeepTheirSettings()
    {
        var service = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CameraGraphicsSettings")).First(t => t != null);
        var dataType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData")).First(t => t != null);
        var scene = EditorSceneManager.NewPreviewScene();
        var game = new GameObject("New scene game camera");
        var preview = new GameObject("Preview camera");
        var ui = new GameObject("UI camera");
        var overlay = new GameObject("Overlay camera");
        var texture = new RenderTexture(16, 16, 0);
        try
        {
            Invoke("SetAntiAliasing", 3);
            Invoke("SetPostProcessing", 1);
            service.GetMethod("Enable").Invoke(null, null);
            foreach (var owner in new[] { game, preview, ui, overlay })
            {
                owner.AddComponent<Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner, scene);
            }
            preview.GetComponent<Camera>().targetTexture = texture;
            ui.GetComponent<Camera>().cullingMask = 1 << 5;
            var overlayData = overlay.AddComponent(dataType);
            var renderType = dataType.GetProperty("renderType");
            renderType.SetValue(overlayData, Enum.Parse(renderType.PropertyType, "Overlay"));
            // Simulate the pre-render context for cameras from the newly loaded scene.
            service.GetMethod("OnBeginContextRendering", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { default(UnityEngine.Rendering.ScriptableRenderContext), new List<Camera> { game.GetComponent<Camera>(), preview.GetComponent<Camera>(), ui.GetComponent<Camera>(), overlay.GetComponent<Camera>() } });
            var data = game.GetComponent(dataType);
            Assert.That(dataType.GetProperty("antialiasing").GetValue(data).ToString(), Is.EqualTo("SubpixelMorphologicalAntiAliasing"));
            Assert.That(preview.GetComponent<Camera>().allowMSAA, Is.True);
            Assert.That(ui.GetComponent<Camera>().allowMSAA, Is.True);
            Assert.That(dataType.GetProperty("antialiasing").GetValue(overlayData).ToString(), Is.EqualTo("None"));
            service.GetMethod("Disable").Invoke(null, null);
            Assert.That(game.GetComponent<Camera>().allowMSAA, Is.True);
            Assert.That(dataType.GetProperty("antialiasing").GetValue(data).ToString(), Is.EqualTo("None"));
        }
        finally
        {
            service.GetMethod("Disable").Invoke(null, null);
            foreach (var owner in new[] { game, preview, ui, overlay }) Object.DestroyImmediate(owner);
            Object.DestroyImmediate(texture);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void GlobalPreferencesMigrateCitySelectionsWithoutOverwritingNewSelections()
    {
        var graphics = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("GraphicsPreferences")).First(t => t != null);
        PlayerPrefs.SetInt("Graphics.CityAntiAliasing", 3);
        PlayerPrefs.SetInt("Graphics.CityPostProcessing", 1);
        graphics.GetMethod("MigrateLegacyPreferences").Invoke(null, null);
        Assert.That(PlayerPrefs.GetInt("Graphics.AntiAliasing"), Is.EqualTo(3));
        Assert.That(PlayerPrefs.GetInt("Graphics.PostProcessing"), Is.EqualTo(1));
        Invoke("SetAntiAliasing", 2);
        graphics.GetMethod("MigrateLegacyPreferences").Invoke(null, null);
        Assert.That(PlayerPrefs.GetInt("Graphics.AntiAliasing"), Is.EqualTo(2));
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
            foreach (string field in new[] { "refreshRateDropdown", "renderScaleDropdown", "antiAliasingDropdown", "postProcessingDropdown" })
            {
                var control = (TMP_Dropdown)settingsType.GetField(field, Private).GetValue(authored);
                Assert.That(control, Is.Not.Null, field);
                Assert.That(control.transform.parent.parent, Is.SameAs(resolution.transform.parent.parent));
            }
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
