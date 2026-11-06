using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Saved overrides layered over quality presets. Never edits pipeline assets on disk.</summary>
public static class GraphicsPreferences
{
    public const string QualityKey = "Graphics.Quality";
    public const string ScaleKey = "Graphics.RenderScale";
    public const string AntiAliasingKey = "Graphics.AntiAliasing";
    public const string PostProcessingKey = "Graphics.PostProcessing";
    public static readonly float[] Scales = { 0f, 0.5f, 0.67f, 0.75f, 0.85f, 1f };
    public static event Action Changed;

    private static UniversalRenderPipelineAsset source;
    private static UniversalRenderPipelineAsset runtime;
    private static RenderPipelineAsset previousOverride;

    public static int ScaleOption => Mathf.Clamp(PlayerPrefs.GetInt(ScaleKey, 0), 0, Scales.Length - 1);
    public static int AntiAliasingOption => Mathf.Clamp(PlayerPrefs.GetInt(AntiAliasingKey, 0), 0, 3);
    public static int PostProcessingOption => Mathf.Clamp(PlayerPrefs.GetInt(PostProcessingKey, 0), 0, 2);

    public static void MigrateLegacyPreferences()
    {
        bool changed = false;
        foreach (var pair in new[] { ("Graphics.CityAntiAliasing", AntiAliasingKey), ("Graphics.CityPostProcessing", PostProcessingKey) })
        {
            if (PlayerPrefs.HasKey(pair.Item2) || !PlayerPrefs.HasKey(pair.Item1)) continue;
            PlayerPrefs.SetInt(pair.Item2, PlayerPrefs.GetInt(pair.Item1));
            changed = true;
        }
        if (changed) PlayerPrefs.Save();
    }

    public static float ResolveScale(int option, int quality) => option > 0 && option < Scales.Length
        ? Scales[option] : quality == 0 ? 0.75f : quality == 1 ? 0.9f : 1f;

    public static AntialiasingMode ResolveAntiAliasing(int option, int quality) => option == 0
        ? (quality == 0 ? AntialiasingMode.None : quality == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing)
        : option == 1 ? AntialiasingMode.None : option == 2 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;

    public static bool ResolvePostProcessing(int option, int quality) => option == 0 ? quality > 0 : option == 2;

    public static void Apply()
    {
        var active = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
        if (active != runtime)
        {
            ReleasePipeline();
            previousOverride = QualitySettings.renderPipeline;
            source = active as UniversalRenderPipelineAsset;
            if (source != null)
            {
                runtime = UnityEngine.Object.Instantiate(source);
                runtime.name = source.name + " (Player settings)";
                runtime.hideFlags = HideFlags.DontSave;
                QualitySettings.renderPipeline = runtime;
            }
        }
        if (runtime != null)
            runtime.renderScale = ResolveScale(ScaleOption, QualitySettings.GetQualityLevel());
        Changed?.Invoke();
    }

    // Call before switching quality, so the outgoing quality slot retains its authored asset.
    public static void ReleasePipeline()
    {
        if (runtime != null)
        {
            if (QualitySettings.renderPipeline == runtime) QualitySettings.renderPipeline = previousOverride;
            if (Application.isPlaying) UnityEngine.Object.Destroy(runtime);
            else UnityEngine.Object.DestroyImmediate(runtime);
        }
        runtime = null;
        source = null;
        previousOverride = null;
    }
}
