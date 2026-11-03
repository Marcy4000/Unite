using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public class EnvironmentShaderRenderingTests
{
    private static readonly Color Background = new Color(0.23f, 0.37f, 0.91f, 0.69f);

    [TestCase("BasicFoliage", "ForwardLit")]
    [TestCase("BasicFoliage", "DepthOnly")]
    [TestCase("BasicFoliage", "DepthNormalsOnly")]
    [TestCase("TreeCrown", "ForwardLit")]
    [TestCase("TreeCrown", "DepthOnly")]
    [TestCase("TreeCrown", "DepthNormalsOnly")]
    [TestCase("TreeLeaf", "ForwardLit")]
    [TestCase("TreeLeaf", "DepthOnly")]
    [TestCase("TreeLeaf", "DepthNormalsOnly")]
    public void FoliageCutsTransparentTexelsEvenWithDisabledLegacyAlphaFlags(string variant, string pass)
    {
        Color[] pixels = Render(variant, pass, true, Color.black);
        Assert.That(Vector4.Distance(pixels[0], Background), Is.LessThan(0.001f), "Transparent leaf texel must not write color or depth normals.");
        Assert.That(Vector4.Distance(pixels[1], Background), Is.GreaterThan(0.1f), "Opaque leaf texel must still render.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BillboardPreservesTextureColorsWithOrWithoutEmission(bool emission)
    {
        Color[] pixels = Render("Billboard", "ForwardLit", false, emission ? Color.white : Color.black);
        Assert.That(pixels[0].r, Is.GreaterThan(0.2f));
        Assert.That(pixels[0].g, Is.LessThan(0.01f), "Red source texel must not become white.");
        Assert.That(pixels[1].g, Is.GreaterThan(0.2f));
        Assert.That(pixels[1].r, Is.LessThan(0.01f), "Green source texel must not become white.");
    }

    private static Color[] Render(string variant, string pass, bool transparentFirstTexel, Color emission)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Requires a graphics device.");
        Shader shader = Shader.Find("==MAIN==/Environment_Variant_" + variant);
        Assert.That(shader, Is.Not.Null);
        var material = new Material(shader);
        Material depthProbe = null;
        var texture = new Texture2D(2, 1, TextureFormat.RGBAFloat, false, true);
        var mesh = new Mesh();
        var target = RenderTexture.GetTemporary(8, 8, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(8, 8, TextureFormat.RGBAFloat, false, true);
        var commands = new CommandBuffer { name = "Chinese Lobby shader regression" };
        RenderTexture previousTarget = RenderTexture.active;
        Matrix4x4 previousView = Shader.GetGlobalMatrix("unity_MatrixV");
        Matrix4x4 previousProjection = Shader.GetGlobalMatrix("glstate_matrix_projection");
        try
        {
            texture.SetPixels(new[] { new Color(1f, 0f, 0f, transparentFirstTexel ? 0f : 1f), Color.green });
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply();
            material.SetTexture("_MainTex", texture);
            material.SetColor("_MainColor", Color.white);
            material.SetColor("_EmissionColor", emission);
            material.SetFloat("_UseAlphaInput", 0f);
            material.SetFloat("_UseAlphaClip", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_UseUnlitMode", variant == "Billboard" ? 0f : 1f);
            material.SetFloat("_DisableMainCityFogMode", 1f);
            material.SetFloat("_EnvironmentLegacyToneMap", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_ZTest", (float)CompareFunction.Always);
            if (material.HasProperty("_IFWIND"))
                material.SetFloat("_IFWIND", 0f);
            mesh.vertices = new[] { new Vector3(-1f, -1f, 0.5f), new Vector3(-1f, 1f, 0.5f), new Vector3(1f, 1f, 0.5f), new Vector3(1f, -1f, 0.5f) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.tangents = new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            int passIndex = material.FindPass(pass);
            Assert.That(passIndex, Is.GreaterThanOrEqualTo(0), pass);
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(true, true, Background);
            commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, passIndex);
            if (pass == "DepthOnly")
            {
                depthProbe = new Material(Shader.Find("==MAIN==/Environment_Variant_Billboard"));
                depthProbe.SetTexture("_MainTex", texture);
                depthProbe.SetColor("_MainColor", Color.white);
                depthProbe.SetFloat("_UseUnlitMode", 1f);
                depthProbe.SetFloat("_EnvironmentLegacyToneMap", 0f);
                depthProbe.SetFloat("_DisableMainCityFogMode", 1f);
                depthProbe.SetFloat("_Cull", (float)CullMode.Off);
                depthProbe.SetFloat("_ZTest", (float)CompareFunction.Equal);
                commands.DrawMesh(mesh, Matrix4x4.identity, depthProbe, 0, depthProbe.FindPass("ForwardLit"));
            }
            commands.SetViewProjectionMatrices(previousView, previousProjection);
            Graphics.ExecuteCommandBuffer(commands);
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, 8, 8), 0, 0);
            readback.Apply();
            return new[] { readback.GetPixel(2, 4), readback.GetPixel(6, 4) };
        }
        finally
        {
            RenderTexture.active = previousTarget;
            commands.Release();
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(readback);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(material);
            if (depthProbe != null)
                Object.DestroyImmediate(depthProbe);
        }
    }
}
