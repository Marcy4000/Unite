using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public class FakeMeshShadowRenderingTests
{
    [Test]
    public void OverlappingTrianglesBlendOnlyOnce()
    {
        Color[] pixels = Render(3, 0, 0, 4);
        Assert.That(pixels[0].r, Is.EqualTo(0.7f).Within(0.02f));
        Assert.That(pixels[0].g, Is.EqualTo(0.7f).Within(0.02f));
    }

    [Test]
    public void GeometryBeyondClipVolumeProjectsBackOntoGround()
    {
        Color[] pixels = Render(3, 0, 0, 1);
        Assert.That(pixels[0].r, Is.EqualTo(0.7f).Within(0.02f));
    }

    [Test]
    public void BendMovesSilhouetteInProjectionDirection()
    {
        Color[] overhead = Render(1, -0.5f, 0, 1);
        Color[] bent = Render(1, -0.5f, 1, 1);
        Assert.That(overhead[1].r, Is.EqualTo(0.7f).Within(0.02f));
        Assert.That(overhead[2].r, Is.EqualTo(1).Within(0.02f));
        Assert.That(bent[1].r, Is.EqualTo(1).Within(0.02f));
        Assert.That(bent[2].r, Is.EqualTo(0.7f).Within(0.02f));
    }

    private static Color[] Render(float height, float center, float bend, int draws)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Requires a graphics device.");
        Shader shader = Shader.Find("Unite/Fake Mesh Shadow");
        Assert.That(shader, Is.Not.Null);
        Assert.That(shader.isSupported, Is.True);
        var material = new Material(shader);
        var mesh = new Mesh();
        var commands = new CommandBuffer();
        var target = RenderTexture.GetTemporary(32, 32, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(32, 32, TextureFormat.RGBA32, false, true);
        var previousTarget = RenderTexture.active;
        var previousView = Shader.GetGlobalMatrix("unity_MatrixV");
        var previousProjection = Shader.GetGlobalMatrix("glstate_matrix_projection");
        try
        {
            // Face the test ground plane toward the camera; its normal is world Z.
            material.SetVector("_GroundPlane", new Vector4(0, 0, 1, 0));
            material.SetVector("_ProjectionDirection", new Vector4(bend, 0, -1, 0));
            material.SetFloat("_SurfaceOffset", 0);
            material.SetColor("_ShadowColor", new Color(0, 0, 0, 0.3f));
            mesh.vertices = new[]
            {
                new Vector3(center - 0.3f, -0.6f, height), new Vector3(center + 0.3f, -0.6f, height),
                new Vector3(center + 0.3f, 0.6f, height), new Vector3(center - 0.3f, 0.6f, height)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(true, true, Color.white);
            commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            for (int i = 0; i < draws; i++) commands.DrawMesh(mesh, Matrix4x4.identity, material);
            commands.SetViewProjectionMatrices(previousView, previousProjection);
            Graphics.ExecuteCommandBuffer(commands);
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
            readback.Apply();
            return new[] { readback.GetPixel(16, 16), readback.GetPixel(8, 16), readback.GetPixel(24, 16) };
        }
        finally
        {
            RenderTexture.active = previousTarget;
            commands.Release();
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(readback);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
        }
    }
}
