using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class ZoomRenderScalePolicy
{
    private UniversalRenderPipelineAsset pipeline;
    private float originalScale;
    private float appliedScale;

    public static float CalculateMultiplier(float distance, float fieldOfView)
    {
        float footprint = Mathf.Max(0f, distance) * Mathf.Tan(Mathf.Clamp(fieldOfView, 1f, 120f) * Mathf.Deg2Rad * 0.5f);
        float normalFootprint = new Vector3(0f, 18.85f, -18.6f).magnitude * Mathf.Tan(25f * Mathf.Deg2Rad * 0.5f);
        return Mathf.Lerp(1f, 0.8f, Mathf.InverseLerp(1.15f, 3.2f, footprint / normalFootprint));
    }

    public void Apply(float distance, float fieldOfView)
    {
        var active = (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline)
            as UniversalRenderPipelineAsset;
        if (pipeline != active)
        {
            Restore();
            pipeline = active;
            if (pipeline != null)
                originalScale = appliedScale = pipeline.renderScale;
        }
        if (pipeline == null) return;
        if (!Mathf.Approximately(pipeline.renderScale, appliedScale))
            originalScale = pipeline.renderScale;

        float multiplier = CalculateMultiplier(distance, fieldOfView);
        float target = Mathf.Clamp(originalScale * multiplier, 0.1f, 2f);
        if (multiplier < 1f)
            target = Mathf.Clamp(Mathf.Round(target * 100f) / 100f, Mathf.Max(0.1f, originalScale * 0.8f), originalScale);
        if (!Mathf.Approximately(pipeline.renderScale, target))
            pipeline.renderScale = target;
        appliedScale = pipeline.renderScale;
    }

    public void Restore()
    {
        if (pipeline != null && Mathf.Approximately(pipeline.renderScale, appliedScale))
            pipeline.renderScale = originalScale;
        pipeline = null;
    }
}
