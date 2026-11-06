using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Applies player graphics preferences to screen-rendering game cameras, including cameras created after scene load.</summary>
public static class CameraGraphicsSettings
{
    private sealed class CameraState
    {
        public UniversalAdditionalCameraData Data;
        public LayerMask EffectsLayers;
        public bool PostProcessing;
        public AntialiasingMode AntiAliasing;
        public AntialiasingQuality AntiAliasingQuality;
        public bool AllowMSAA;
    }

    private static readonly Dictionary<Camera, CameraState> cameras = new Dictionary<Camera, CameraState>();
    private static readonly List<Camera> staleCameras = new List<Camera>();
    private static bool enabled;
    private static AntialiasingMode antiAliasing;
    private static bool effectsEnabled;

    public static void Enable()
    {
        if (enabled) return;
        enabled = true;
        GraphicsPreferences.Changed += ApplyToActiveCameras;
        // This runs before URP updates volumes and initializes camera stacks.
        RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
        ApplyToActiveCameras();
    }

    public static void Disable()
    {
        GraphicsPreferences.Changed -= ApplyToActiveCameras;
        RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
        foreach (var entry in cameras) Restore(entry.Key, entry.Value);
        cameras.Clear();
        staleCameras.Clear();
        enabled = false;
    }

    private static bool IsEligible(Camera camera)
    {
        if (camera == null || camera.cameraType != CameraType.Game || camera.targetTexture != null) return false;
        // UI-only cameras and URP overlays keep their authored presentation settings.
        if ((camera.cullingMask & ~(1 << 5)) == 0) return false;
        return !camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) || data.renderType == CameraRenderType.Base;
    }

    private static void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> renderingCameras)
    {
        PruneCameras();
        foreach (var camera in renderingCameras) ApplyToCamera(camera);
    }

    private static void ApplyToActiveCameras()
    {
        int quality = QualitySettings.GetQualityLevel();
        antiAliasing = GraphicsPreferences.ResolveAntiAliasing(GraphicsPreferences.AntiAliasingOption, quality);
        effectsEnabled = GraphicsPreferences.ResolvePostProcessing(GraphicsPreferences.PostProcessingOption, quality);
        PruneCameras();
        foreach (var camera in Camera.allCameras) ApplyToCamera(camera);
    }

    private static void PruneCameras()
    {
        staleCameras.Clear();
        foreach (var entry in cameras)
        {
            if (IsEligible(entry.Key)) continue;
            Restore(entry.Key, entry.Value);
            staleCameras.Add(entry.Key);
        }
        foreach (var camera in staleCameras) cameras.Remove(camera);
    }

    private static void ApplyToCamera(Camera camera)
    {
        if (!IsEligible(camera)) return;
        var data = camera.GetUniversalAdditionalCameraData();
        if (!cameras.TryGetValue(camera, out var state))
        {
            state = new CameraState
            {
                Data = data,
                EffectsLayers = data.volumeLayerMask,
                PostProcessing = data.renderPostProcessing,
                AntiAliasing = data.antialiasing,
                AntiAliasingQuality = data.antialiasingQuality,
                AllowMSAA = camera.allowMSAA
            };
            cameras.Add(camera, state);
        }
        data.antialiasing = antiAliasing;
        data.antialiasingQuality = AntialiasingQuality.Medium;
        // URP runs FXAA/SMAA through post passes. Excluding volumes permits AA alone.
        data.volumeLayerMask = effectsEnabled ? state.EffectsLayers : (LayerMask)0;
        data.renderPostProcessing = effectsEnabled || data.antialiasing != AntialiasingMode.None;
        camera.allowMSAA = false;
    }

    private static void Restore(Camera camera, CameraState state)
    {
        if (camera == null || state.Data == null) return;
        state.Data.volumeLayerMask = state.EffectsLayers;
        state.Data.renderPostProcessing = state.PostProcessing;
        state.Data.antialiasing = state.AntiAliasing;
        state.Data.antialiasingQuality = state.AntiAliasingQuality;
        camera.allowMSAA = state.AllowMSAA;
    }
}
