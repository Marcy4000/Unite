using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Draw the reconstructed shaders' inverted hulls explicitly, after their surfaces.
/// SRPDefaultUnlit is not a portable multipass mechanism, particularly with depth priming.
/// Uses URP's RenderObjectsPass, supporting both Render Graph and compatibility mode.
/// </summary>
public sealed class TrainerOutlineRendererFeature : ScriptableRendererFeature
{
    public const string PassTag = "TrainerOutline";
    private RenderObjectsPass opaqueOutlines;
    private RenderObjectsPass transparentOutlines;

    public override void Create()
    {
        var tags = new[] { PassTag };
        var cameraSettings = new RenderObjects.CustomCameraSettings();
        // All layers: the camera's culling results still restrict visibility. In particular,
        // do not omit UIObject3D's isolated layer or Scene View cameras.
        opaqueOutlines = new RenderObjectsPass("Trainer outlines (opaque)",
            RenderPassEvent.AfterRenderingOpaques, tags, RenderQueueType.Opaque, -1, cameraSettings);
        transparentOutlines = new RenderObjectsPass("Trainer outlines (transparent)",
            RenderPassEvent.AfterRenderingTransparents, tags, RenderQueueType.Transparent, -1, cameraSettings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var target = renderingData.cameraData.camera.targetTexture;
        if (target != null && target.format == RenderTextureFormat.Depth) return;
        renderer.EnqueuePass(opaqueOutlines);
        renderer.EnqueuePass(transparentOutlines);
    }
}
