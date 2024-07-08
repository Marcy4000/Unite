using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JSAM;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private ScrollRect settingsScrollRect;

    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private Toggle[] qualityToggles;

    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    private Resolution[] resolutions;

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Populate resolution dropdown
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();
        int currentResolutionIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = $"{resolutions[i].width} x {resolutions[i].height} @{Mathf.RoundToInt((float)resolutions[i].refreshRateRatio.value)}hz";
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height &&
                resolutions[i].refreshRateRatio.value == Screen.currentResolution.refreshRateRatio.value)
            {
                currentResolutionIndex = i;
            }
        }
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);

        // Load saved settings
        fullscreenToggle.isOn = Screen.fullScreen;
        vsyncToggle.isOn = QualitySettings.vSyncCount == 1;
        for (int i = 0; i < qualityToggles.Length; i++)
        {
            qualityToggles[i].isOn = QualitySettings.GetQualityLevel() == i;
        }

        // Add listeners
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        vsyncToggle.onValueChanged.AddListener((value) => QualitySettings.vSyncCount = value ? 1 : 0);
        for (int i = 0; i < qualityToggles.Length; i++)
        {
            int qualityIndex = i;
            qualityToggles[i].onValueChanged.AddListener((value) => SetQuality(qualityIndex));
        }

        musicVolumeSlider.value = AudioManager.MusicVolume;
        sfxVolumeSlider.value = AudioManager.SoundVolume;
        masterVolumeSlider.value = AudioManager.MasterVolume;

        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            fullscreenToggle.interactable = false;
            vsyncToggle.interactable = false;
            resolutionDropdown.interactable = false;
        }

        CloseSettings();
    }

    private void UpdateOptions()
    {
        int currentResolutionIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height &&
                resolutions[i].refreshRateRatio.value == Screen.currentResolution.refreshRateRatio.value)
            {
                currentResolutionIndex = i;
            }
        }
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        fullscreenToggle.isOn = Screen.fullScreen;
        for (int i = 0; i < qualityToggles.Length; i++)
        {
            qualityToggles[i].isOn = QualitySettings.GetQualityLevel() == i;
        }

        musicVolumeSlider.value = AudioManager.MusicVolume;
        sfxVolumeSlider.value = AudioManager.SoundVolume;
        masterVolumeSlider.value = AudioManager.MasterVolume;
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
    }

    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode, resolution.refreshRateRatio);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        Screen.fullScreenMode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
    }

    public void SetMasterVolume(float volume)
    {
        AudioManager.MasterVolume = volume;
    }

    public void SetMusicVolume(float volume)
    {
        AudioManager.MusicVolume = volume;
    }

    public void SetSFXVolume(float volume)
    {
        AudioManager.SoundVolume = volume;
    }
}
