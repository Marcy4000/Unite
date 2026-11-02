using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class TrainerHeadColorTests
{
    private const string PrefabPath = "Assets/Prefabs/Lobby/TrainerHead.prefab";
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static float Tone(float value, bool eye)
    {
        float scaled = value * 0.45f;
        return eye
            ? Mathf.Pow(scaled / (scaled + 0.155f) * 1.019f, 2.2f)
            : scaled * (value * 1.1295f + 0.03f) / (scaled * (value * 1.0935f + 0.59f) + 0.08f);
    }

    [TestCase(false, 0f)]
    [TestCase(false, 0.03f)]
    [TestCase(false, 0.25f)]
    [TestCase(false, 1f)]
    [TestCase(true, 0f)]
    [TestCase(true, 0.03f)]
    [TestCase(true, 0.25f)]
    [TestCase(true, 1f)]
    public void PortraitRecolorsBeforeTheMatchingShaderToneCurve(bool eye, float tint)
    {
        var template = Resources.Load<Material>(eye ? "UI/TrainerHeadEyes" : "UI/TrainerHeadHair");
        Assert.That(template, Is.Not.Null);
        Assert.That(ShaderUtil.ShaderHasError(template.shader), Is.False);
        var material = new Material(template);
        var texture = new Texture2D(4, 1, TextureFormat.RGBAFloat, false, true);
        var target = RenderTexture.GetTemporary(4, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(4, 1, TextureFormat.RGBAFloat, false, true);
        var previousTarget = RenderTexture.active;
        bool previousSRGBWrite = GL.sRGBWrite;
        float[] neutralValues = { 0f, 0.1f, 0.6f, 2f };
        try
        {
            material.SetColor("_TrainerColor", new Color(tint, tint * 0.5f, tint * 0.2f, 1f));
            for (int index = 0; index < neutralValues.Length; index++)
            {
                float neutral = Tone(neutralValues[index], eye);
                texture.SetPixel(index, 0, new Color(neutral, neutral, neutral, 1f));
            }
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.clear);
            GL.sRGBWrite = false;
            Graphics.Blit(texture, target, material);
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, 4, 1), 0, 0);
            readback.Apply();
            for (int index = 0; index < neutralValues.Length; index++)
            {
                var actual = readback.GetPixel(index, 0);
                Assert.That(actual.r, Is.EqualTo(Tone(neutralValues[index] * tint, eye)).Within(0.002f));
                Assert.That(actual.g, Is.EqualTo(Tone(neutralValues[index] * tint * 0.5f, eye)).Within(0.002f));
                Assert.That(actual.b, Is.EqualTo(Tone(neutralValues[index] * tint * 0.2f, eye)).Within(0.002f));
                Assert.That(actual.a, Is.EqualTo(1f).Within(0.002f));
            }
        }
        finally
        {
            GL.sRGBWrite = previousSRGBWrite;
            RenderTexture.active = previousTarget;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(readback);
            Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void PrefabUsesSeparateHairAndEyeCurves()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var hair = prefab.transform.Find("Hair").GetComponent<Image>();
        var eyes = prefab.transform.Find("Pupils").GetComponent<Image>();
        Assert.That(hair.material, Is.EqualTo(Resources.Load<Material>("UI/TrainerHeadHair")));
        Assert.That(eyes.material, Is.EqualTo(Resources.Load<Material>("UI/TrainerHeadEyes")));
        Assert.That(hair.color, Is.EqualTo(Color.white));
        Assert.That(hair.material.GetFloat("_Eye"), Is.Zero);
        Assert.That(eyes.material.GetFloat("_Eye"), Is.EqualTo(1f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReinitializationReusesMaterialsWithoutRecoloringOtherHeads(bool masked)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var first = Object.Instantiate(prefab);
        var second = Object.Instantiate(prefab);
        GameObject mask = null;
        if (masked)
        {
            mask = new GameObject("Portrait color mask", typeof(RectTransform), typeof(Canvas), typeof(Image), typeof(Mask));
            first.transform.SetParent(mask.transform, false);
            second.transform.SetParent(mask.transform, false);
        }
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("PlayerHeadUI"))
            .First(candidate => candidate != null);
        var initialize = type.GetMethod("InitializeHead");
        var infoType = initialize.GetParameters()[0].ParameterType;
        var info = Activator.CreateInstance(infoType);
        var hairColor = new Color32(32, 90, 170, 255);
        var eyeColor = new Color32(180, 65, 25, 255);
        infoType.GetField("HairColor").SetValue(info, hairColor);
        infoType.GetField("EyeColor").SetValue(info, eyeColor);
        Material hair = null;
        Material pupil = null;
        try
        {
            var firstHead = first.GetComponent(type);
            var secondHead = second.GetComponent(type);
            initialize.Invoke(firstHead, new[] { info });
            initialize.Invoke(secondHead, new[] { info });
            hair = ((Image)type.GetField("hairImage", InstanceMembers).GetValue(firstHead)).material;
            pupil = ((Image)type.GetField("pupilImage", InstanceMembers).GetValue(firstHead)).material;
            infoType.GetField("HairColor").SetValue(info, new Color32(255, 0, 0, 255));
            initialize.Invoke(secondHead, new[] { info });
            Assert.That(hair.GetColor("_TrainerColor"), Is.EqualTo((Color)hairColor));
            Assert.That(((Image)type.GetField("hairImage", InstanceMembers).GetValue(secondHead)).material,
                Is.Not.SameAs(hair));
            initialize.Invoke(firstHead, new[] { info });
            Assert.That(((Image)type.GetField("hairImage", InstanceMembers).GetValue(firstHead)).material, Is.SameAs(hair));
            Assert.That(((Image)type.GetField("pupilImage", InstanceMembers).GetValue(firstHead)).material, Is.SameAs(pupil));
            Assert.That(hair.GetColor("_TrainerColor"), Is.EqualTo(Color.red));
            Assert.That(pupil.GetColor("_TrainerColor"), Is.EqualTo((Color)eyeColor));
            var hairImage = (Image)type.GetField("hairImage", InstanceMembers).GetValue(firstHead);
            var pupilImage = (Image)type.GetField("pupilImage", InstanceMembers).GetValue(firstHead);
            Assert.That(hairImage.materialForRendering.GetColor("_TrainerColor"), Is.EqualTo(Color.red));
            Assert.That(pupilImage.materialForRendering.GetColor("_TrainerColor"), Is.EqualTo((Color)eyeColor));
            if (masked) Assert.That(hairImage.materialForRendering, Is.Not.SameAs(hair));
            Assert.That(Resources.Load<Material>("UI/TrainerHeadEyes").GetColor("_TrainerColor"), Is.EqualTo(Color.white));
        }
        finally
        {
            type.GetMethod("OnDestroy", InstanceMembers).Invoke(first.GetComponent(type), null);
            type.GetMethod("OnDestroy", InstanceMembers).Invoke(second.GetComponent(type), null);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            if (mask != null) Object.DestroyImmediate(mask);
        }
        Assert.That(hair == null, Is.True);
        Assert.That(pupil == null, Is.True);
    }
}
