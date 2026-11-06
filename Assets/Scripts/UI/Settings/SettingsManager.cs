using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JSAM;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }
    public static bool AutoJoinOpenWorldLobby => PlayerPrefs.GetInt("OpenWorldLobbyEnabled", 1) != 0;
    public static event Action<bool> OpenWorldLobbyChanged;

    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject keybindsPanel;
    [SerializeField] private ScrollRect settingsScrollRect;

    [SerializeField] private GameObject keybindsOption;

    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown frameRateDropdown;
    [SerializeField] private TMP_Dropdown refreshRateDropdown;
    [SerializeField] private TMP_Dropdown renderScaleDropdown;
    [UnityEngine.Serialization.FormerlySerializedAs("cityAntiAliasingDropdown")]
    [SerializeField] private TMP_Dropdown antiAliasingDropdown;
    [UnityEngine.Serialization.FormerlySerializedAs("cityPostProcessingDropdown")]
    [SerializeField] private TMP_Dropdown postProcessingDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private Toggle openWorldLobbyToggle;
    [SerializeField] private Toggle[] qualityToggles;

    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider announcerVolumeSlider;

    [SerializeField] private VolumeTrack musicVolumeTrack;
    [SerializeField] private VolumeTrack sfxVolumeTrack;
    [SerializeField] private VolumeTrack announcerVolumeTrack;

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private readonly List<int> frameRates = new List<int>();
    private int selectedFrameRate = -1;
    private readonly List<int> refreshRates = new List<int> { 0, 60, 90, 120, 144, 165, 240 };
    public bool IsMenuOpen => (settingsPanel != null && settingsPanel.activeInHierarchy) ||
        (creditsPanel != null && creditsPanel.activeInHierarchy) ||
        (keybindsPanel != null && keybindsPanel.activeInHierarchy);

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

        int savedQuality = PlayerPrefs.GetInt(GraphicsPreferences.QualityKey, QualitySettings.GetQualityLevel());
        if (savedQuality >= 0 && savedQuality < QualitySettings.names.Length)
            QualitySettings.SetQualityLevel(savedQuality);
        if (!AndroidDisplaySettings.IsMobileDisplay && PlayerPrefs.HasKey("Display.VSync"))
            QualitySettings.vSyncCount = PlayerPrefs.GetInt("Display.VSync") != 0 ? 1 : 0;
        GraphicsPreferences.MigrateLegacyPreferences();
        CameraGraphicsSettings.Enable();
        GraphicsPreferences.Apply();
        AndroidDisplaySettings.ApplyMobileFrameRatePolicy();
    }

    void Start()
    {
        InitializeOpenWorldLobbyToggle();

        selectedFrameRate = AndroidDisplaySettings.IsMobileDisplay ? AndroidDisplaySettings.PreferredFrameRate
            : PlayerPrefs.GetInt(AndroidDisplaySettings.FrameRateKey, Application.targetFrameRate > 0 ? Application.targetFrameRate : -1);
        InitializeGraphicsOptions();
        InitializeResolutionOptions(Screen.resolutions, new Vector2Int(Screen.width, Screen.height));
        InitializeFrameRateOptions();
        ApplyFrameRate();
        RefreshDisplayControls();

        masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
        announcerVolumeSlider.onValueChanged.AddListener(SetAnnouncerVolume);
        // Load saved settings
        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        vsyncToggle.SetIsOnWithoutNotify(QualitySettings.vSyncCount > 0);
        for (int i = 0; i < qualityToggles.Length; i++)
        {
            qualityToggles[i].SetIsOnWithoutNotify(QualitySettings.GetQualityLevel() == i);
        }

        // Add listeners
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
        if (frameRateDropdown != null)
            frameRateDropdown.onValueChanged.AddListener(SetFrameRate);
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        vsyncToggle.onValueChanged.AddListener(SetVSync);
        for (int i = 0; i < qualityToggles.Length; i++)
        {
            int qualityIndex = i;
            qualityToggles[i].onValueChanged.AddListener((value) =>
            {
                if (value) SetQuality(qualityIndex);
            });
        }

        musicVolumeSlider.value = AudioManager.GetVolume(musicVolumeTrack);
        sfxVolumeSlider.value = AudioManager.GetVolume(sfxVolumeTrack);
        announcerVolumeSlider.value = AudioManager.GetVolume(announcerVolumeTrack);
        masterVolumeSlider.value = AudioManager.GetVolume();

        if (AndroidDisplaySettings.IsMobileDisplay)
            keybindsOption.SetActive(false);

        CloseSettings();
        CloseCredits();
        CloseKeybinds();
    }

    private void InitializeOpenWorldLobbyToggle()
    {
        if (openWorldLobbyToggle == null) return;
        openWorldLobbyToggle.SetIsOnWithoutNotify(AutoJoinOpenWorldLobby);
        openWorldLobbyToggle.onValueChanged.RemoveListener(SetOpenWorldLobby);
        openWorldLobbyToggle.onValueChanged.AddListener(SetOpenWorldLobby);
    }

    private void OnDestroy()
    {
        if (openWorldLobbyToggle != null)
            openWorldLobbyToggle.onValueChanged.RemoveListener(SetOpenWorldLobby);

        if (Instance == this)
        {
            CameraGraphicsSettings.Disable();
            GraphicsPreferences.ReleasePipeline();
            Instance = null;
        }
    }

    private void UpdateOptions()
    {
        if (openWorldLobbyToggle != null)
            openWorldLobbyToggle.SetIsOnWithoutNotify(AutoJoinOpenWorldLobby);

        InitializeResolutionOptions(Screen.resolutions, new Vector2Int(Screen.width, Screen.height));
        RefreshGraphicsControls();
        RefreshDisplayControls();
        for (int i = 0; i < qualityToggles.Length; i++)
        {
            qualityToggles[i].SetIsOnWithoutNotify(QualitySettings.GetQualityLevel() == i);
        }

        musicVolumeSlider.value = AudioManager.GetVolume(musicVolumeTrack);
        sfxVolumeSlider.value = AudioManager.GetVolume(sfxVolumeTrack);
        announcerVolumeSlider.value = AudioManager.GetVolume(announcerVolumeTrack);
        masterVolumeSlider.value = AudioManager.GetVolume();
    }

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
        settingsScrollRect.verticalNormalizedPosition = 1;
        UpdateOptions();
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        CloseCredits();
    }

    public void OpenCredits()
    {
        creditsPanel.SetActive(true);
    }

    public void CloseCredits()
    {
        creditsPanel.SetActive(false);
    }

    public void OpenKeybinds()
    {
        keybindsPanel.SetActive(true);
    }

    public void CloseKeybinds()
    {
        keybindsPanel.SetActive(false);
    }

    public void SetResolution(int resolutionIndex)
    {
        if (AndroidDisplaySettings.IsMobileDisplay) return;
        if (Application.platform == RuntimePlatform.WebGLPlayer ||
            resolutionIndex < 0 || resolutionIndex >= resolutions.Count) return;
        Vector2Int resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.x, resolution.y, Screen.fullScreenMode);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        // Android owns immersive mode separately from desktop window settings.
        if (AndroidDisplaySettings.IsMobileDisplay)
            return;

        Vector2Int resolution = resolutionDropdown != null && resolutionDropdown.value < resolutions.Count
            ? resolutions[resolutionDropdown.value] : new Vector2Int(Screen.width, Screen.height);
        Screen.SetResolution(resolution.x, resolution.y,
            isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(isFullscreen);
    }

    public void SetQuality(int qualityIndex)
    {
        if (qualityIndex < 0 || qualityIndex >= QualitySettings.names.Length) return;
        int vSyncCount = QualitySettings.vSyncCount;
        GraphicsPreferences.ReleasePipeline();
        QualitySettings.SetQualityLevel(qualityIndex);
        QualitySettings.vSyncCount = vSyncCount;
        PlayerPrefs.SetInt(GraphicsPreferences.QualityKey, qualityIndex);
        PlayerPrefs.Save();
        GraphicsPreferences.Apply();
        ApplyFrameRate();
        RefreshGraphicsControls();
        RefreshDisplayControls();
    }

    public void SetVSync(bool enabled)
    {
        if (!AndroidDisplaySettings.ApplyMobileFrameRatePolicy())
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            PlayerPrefs.SetInt("Display.VSync", enabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyFrameRate();
        }
        RefreshDisplayControls();
    }

    private void InitializeResolutionOptions(Resolution[] availableResolutions, Vector2Int currentSize)
    {
        resolutions.Clear();
        foreach (Resolution resolution in availableResolutions)
        {
            var size = new Vector2Int(resolution.width, resolution.height);
            if (size.x > 0 && size.y > 0 && !resolutions.Contains(size))
                resolutions.Add(size);
        }
        if (!resolutions.Contains(currentSize))
            resolutions.Add(currentSize);
        resolutions.Sort((left, right) => left.x != right.x
            ? left.x.CompareTo(right.x) : left.y.CompareTo(right.y));

        var options = new List<string>();
        Resolution desktop = Screen.currentResolution;
        foreach (Vector2Int size in resolutions)
            options.Add($"{size.x} × {size.y}" +
                (size.x == desktop.width && size.y == desktop.height ? " (Desktop)" : ""));
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.SetValueWithoutNotify(resolutions.IndexOf(currentSize));
        resolutionDropdown.RefreshShownValue();
    }

    private void InitializeFrameRateOptions()
    {
        if (frameRateDropdown == null) return;
        frameRates.Clear();
        frameRates.AddRange(new[] { 30, 60, 75, 90, 120, 144, 165, 180, 240, 360 });
        if (selectedFrameRate > 0 && !frameRates.Contains(selectedFrameRate))
            frameRates.Add(selectedFrameRate);
        frameRates.Sort();
        if (!AndroidDisplaySettings.IsMobileDisplay) frameRates.Insert(0, -1);
        var options = new List<string>();
        foreach (int frameRate in frameRates)
            options.Add(frameRate < 0 ? "Unlimited" : $"{frameRate} FPS");
        frameRateDropdown.ClearOptions();
        frameRateDropdown.AddOptions(options);
        frameRateDropdown.SetValueWithoutNotify(frameRates.IndexOf(selectedFrameRate));
    }

    public void SetFrameRate(int frameRateIndex)
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer ||
            (!AndroidDisplaySettings.IsMobileDisplay && QualitySettings.vSyncCount > 0) ||
            frameRateIndex < 0 || frameRateIndex >= frameRates.Count) return;
        selectedFrameRate = frameRates[frameRateIndex];
        PlayerPrefs.SetInt(AndroidDisplaySettings.FrameRateKey, selectedFrameRate);
        PlayerPrefs.Save();
        ApplyFrameRate();
        RefreshDisplayControls();
    }

    private void ApplyFrameRate()
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer) return;
        if (AndroidDisplaySettings.ApplyMobileFrameRatePolicy()) return;
        Application.targetFrameRate = QualitySettings.vSyncCount > 0 ? -1 : selectedFrameRate;
    }

    private void RefreshDisplayControls()
    {
        bool mobile = AndroidDisplaySettings.IsMobileDisplay;
        bool browser = Application.platform == RuntimePlatform.WebGLPlayer;
        bool synced = QualitySettings.vSyncCount > 0;
        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            fullscreenToggle.interactable = !mobile;
        }
        if (vsyncToggle != null)
        {
            vsyncToggle.SetIsOnWithoutNotify(synced);
            vsyncToggle.interactable = !mobile && !browser;
        }
        if (resolutionDropdown != null)
            resolutionDropdown.interactable = !mobile && !browser;
        if (frameRateDropdown == null) return;
        frameRateDropdown.interactable = !browser && (mobile || !synced);
        frameRateDropdown.SetValueWithoutNotify(Mathf.Max(0, frameRates.IndexOf(selectedFrameRate)));
        frameRateDropdown.RefreshShownValue();
        if (frameRateDropdown.captionText == null) return;
        if (browser)
            frameRateDropdown.captionText.text = "Automatic";
        else if (!mobile && synced)
            frameRateDropdown.captionText.text = "VSync (" +
                (Screen.currentResolution.refreshRateRatio.value / QualitySettings.vSyncCount).ToString("0.#") + " FPS)";
    }

    private void InitializeGraphicsOptions()
    {
        ConfigureDropdown(renderScaleDropdown, new[] { "Use preset", "50%", "67%", "75%", "85%", "100% (Native)" }, SetRenderScale);
        ConfigureDropdown(antiAliasingDropdown, new[] { "Use preset", "Off", "FXAA (Fast)", "SMAA (Sharper)" }, SetAntiAliasing);
        ConfigureDropdown(postProcessingDropdown, new[] { "Use preset", "Off", "On" }, SetPostProcessing);
        if (refreshRateDropdown != null)
        {
            int saved = AndroidDisplaySettings.PreferredRefreshRate;
            if (!refreshRates.Contains(saved)) { refreshRates.Add(saved); refreshRates.Sort(); }
            var options = new List<string>();
            foreach (int rate in refreshRates) options.Add(rate == 0 ? "System default" : rate + " Hz (Requested)");
            ConfigureDropdown(refreshRateDropdown, options.ToArray(), SetRefreshRate);
            // Physical display mode selection is exposed only where our native bridge supports it.
            refreshRateDropdown.transform.parent.gameObject.SetActive(Application.platform == RuntimePlatform.Android);
        }
        RefreshGraphicsControls();
    }

    private static void ConfigureDropdown(TMP_Dropdown dropdown, string[] options, UnityEngine.Events.UnityAction<int> callback)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        dropdown.onValueChanged.RemoveListener(callback);
        dropdown.onValueChanged.AddListener(callback);
    }

    private void RefreshGraphicsControls()
    {
        if (renderScaleDropdown != null) renderScaleDropdown.SetValueWithoutNotify(GraphicsPreferences.ScaleOption);
        if (antiAliasingDropdown != null) antiAliasingDropdown.SetValueWithoutNotify(GraphicsPreferences.AntiAliasingOption);
        if (postProcessingDropdown != null) postProcessingDropdown.SetValueWithoutNotify(GraphicsPreferences.PostProcessingOption);
        if (refreshRateDropdown != null) refreshRateDropdown.SetValueWithoutNotify(Mathf.Max(0, refreshRates.IndexOf(AndroidDisplaySettings.PreferredRefreshRate)));
    }

    private void SaveGraphicsOption(string key, int value, int count)
    {
        if (value < 0 || value >= count) return;
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
        GraphicsPreferences.Apply();
        RefreshGraphicsControls();
    }

    public void SetRenderScale(int option) => SaveGraphicsOption(GraphicsPreferences.ScaleKey, option, GraphicsPreferences.Scales.Length);
    public void SetAntiAliasing(int option) => SaveGraphicsOption(GraphicsPreferences.AntiAliasingKey, option, 4);
    public void SetPostProcessing(int option) => SaveGraphicsOption(GraphicsPreferences.PostProcessingKey, option, 3);

    public void SetRefreshRate(int option)
    {
        if (Application.platform != RuntimePlatform.Android || option < 0 || option >= refreshRates.Count) return;
        PlayerPrefs.SetInt(AndroidDisplaySettings.RefreshRateKey, refreshRates[option]);
        PlayerPrefs.Save();
        ApplyFrameRate();
        RefreshGraphicsControls();
    }

    public void SetOpenWorldLobby(bool enabled)
    {
        bool changed = AutoJoinOpenWorldLobby != enabled;
        PlayerPrefs.SetInt("OpenWorldLobbyEnabled", enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (openWorldLobbyToggle != null)
            openWorldLobbyToggle.SetIsOnWithoutNotify(enabled);

        if (changed)
            OpenWorldLobbyChanged?.Invoke(enabled);
    }

    public void SetMasterVolume(float volume)
    {
        AudioManager.SetVolume(volume);
    }

    public void SetMusicVolume(float volume)
    {
        AudioManager.SetVolume(musicVolumeTrack, volume);
    }

    public void SetSFXVolume(float volume)
    {
        AudioManager.SetVolume(sfxVolumeTrack, volume);
    }

    public void SetAnnouncerVolume(float volume)
    {
        AudioManager.SetVolume(announcerVolumeTrack, volume);
    }
}
