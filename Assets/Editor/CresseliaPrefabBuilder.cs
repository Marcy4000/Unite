using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Duraludon's authored-asset approach: editable meshes/materials/particles and
// baked audio. Re-running creates missing assets without replacing hand edits.
public static class CresseliaPrefabBuilder
{
    private const string Folder = CresseliaMoveRules.EffectFolder;
    private const string Materials = "Assets/Materials/Cresselia/";
    private static Scene preview;
    private static Mesh ring, crescent;
    private static Material pearl, lavender, pink, gold, particle;

    [MenuItem("Tools/Cresselia/Create Missing Rework Prefabs")]
    public static void Build()
    {
        EnsureFolder(Folder.TrimEnd('/')); EnsureFolder(Folder + "Art"); EnsureFolder(Materials.TrimEnd('/'));
        ring = BakeArc("LunarRing", false); crescent = BakeArc("LunarCrescent", true);
        pearl = Material("Pearl", new Color(0.88f, 0.94f, 1));
        lavender = Material("Lavender", new Color(0.58f, 0.38f, 0.95f));
        pink = Material("DreamPink", new Color(1, 0.52f, 0.78f));
        gold = Material("LunarGold", new Color(1, 0.83f, 0.4f));
        particle = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        preview = EditorSceneManager.NewPreviewScene();
        try { foreach (CresseliaEffect effect in Enum.GetValues(typeof(CresseliaEffect))) Create(effect); }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("MovesAssets");
        foreach (CresseliaEffect effect in Enum.GetValues(typeof(CresseliaEffect)))
        {
            string path = CresseliaMoveRules.EffectPath(effect);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
        }
        EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        ConfigureAnimations();
        AssetDatabase.SaveAssets();
        Debug.Log("Cresselia lunar effects authored and registered. Gameplay is independent of cosmetic loading.");
    }

    private static void Create(CresseliaEffect effect)
    {
        string path = CresseliaMoveRules.EffectPath(effect);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node("Cresselia" + effect);
        var view = root.AddComponent<CresseliaVisual>();
        var graphics = Node("Graphics", root.transform);
        var accent = Node("Accent", graphics.transform);
        Set(view, "graphics", graphics); Set(view, "accent", accent.transform);
        switch (effect)
        {
            case CresseliaEffect.Basic:
            case CresseliaEffect.Boosted:
                bool boosted = effect == CresseliaEffect.Boosted;
                Primitive("MoonMote", accent.transform, PrimitiveType.Sphere, pearl, Vector3.zero, Vector3.one * (boosted ? 0.25f : 0.15f));
                if (boosted) MeshNode("DreamCrescent", accent.transform, crescent, pink, Vector3.zero, Vector3.one * 0.4f).transform.localRotation = Quaternion.Euler(90, 0, 0);
                Trail(accent.transform, boosted ? pink : lavender, boosted ? 0.2f : 0.1f);
                break;
            case CresseliaEffect.AuroraBeam:
                for (int i = 0; i < 3; i++)
                {
                    var ribbon = Primitive("AuroraRibbon" + i, accent.transform, PrimitiveType.Cylinder, i == 1 ? pearl : i == 0 ? lavender : pink,
                        new Vector3((i - 1) * 0.14f, 0, -0.6f), new Vector3(0.08f, 0.7f, 0.08f));
                    ribbon.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
                Trail(accent.transform, lavender, 0.32f); break;
            case CresseliaEffect.Psybeam:
                for (int i = 0; i < 3; i++)
                {
                    var beam = Primitive("PsychicRibbon" + i, accent.transform, PrimitiveType.Cylinder, i == 1 ? pearl : lavender,
                        new Vector3((i - 1) * 0.3f, 0.7f, 3), new Vector3(i == 1 ? 0.1f : 0.07f, 3, i == 1 ? 0.1f : 0.07f));
                    beam.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
                for (int i = 0; i < 4; i++)
                {
                    var hoop = MeshNode("BeamCrescent" + i, accent.transform, crescent, pink, new Vector3(0, 0.7f, i * 1.6f), Vector3.one * 0.5f);
                    hoop.transform.localRotation = Quaternion.Euler(90, i * 40, 0);
                }
                break;
            case CresseliaEffect.Moonblast:
                Primitive("PearlCore", accent.transform, PrimitiveType.Sphere, pearl, Vector3.zero, Vector3.one * 0.55f);
                for (int i = 0; i < 2; i++)
                    MeshNode("OrbitingCrescent" + i, accent.transform, crescent, i == 0 ? pink : lavender,
                        Vector3.zero, Vector3.one * 0.65f).transform.localRotation = Quaternion.Euler(90, i * 90, 35);
                Trail(accent.transform, pink, 0.3f); break;
            case CresseliaEffect.MoonblastImpact:
            case CresseliaEffect.UniteImpact:
                MeshNode("ImpactBoundary", accent.transform, ring, effect == CresseliaEffect.UniteImpact ? gold : pink, Vector3.up * 0.1f, Vector3.one);
                MeshNode("PearlShockwave", accent.transform, ring, pearl, Vector3.up * 0.13f, Vector3.one * 0.82f);
                Petals(accent.transform, effect == CresseliaEffect.UniteImpact ? 12 : 6, 0.8f, pink);
                Sparks(accent.transform, new Color(0.95f, 0.7f, 1), 0.8f, 70); break;
            case CresseliaEffect.Cleanse:
                MeshNode("CleansingRing", accent.transform, ring, gold, Vector3.up * 0.08f, Vector3.one * 0.85f);
                Shell(accent.transform, pearl, 3); Sparks(accent.transform, new Color(1, 0.85f, 0.55f), 0.65f, 35); break;
            case CresseliaEffect.LunarShield:
                Shell(accent.transform, lavender, 4);
                MeshNode("ProtectionRing", accent.transform, ring, gold, Vector3.up * 0.1f, Vector3.one * 0.8f);
                MeshNode("ShieldCrown", accent.transform, crescent, gold, Vector3.up * 1.9f, Vector3.one * 0.4f).transform.localRotation = Quaternion.Euler(90, 0, 0);
                Sparks(accent.transform, new Color(0.7f, 0.6f, 1), 0.8f, 8); break;
            case CresseliaEffect.ShieldHeal:
                Shell(accent.transform, pearl, 3); Petals(accent.transform, 5, 0.7f, gold);
                Sparks(accent.transform, new Color(1, 0.85f, 0.6f), 0.65f, 45); break;
            case CresseliaEffect.ShieldBreak:
                Petals(accent.transform, 6, 0.9f, lavender);
                Sparks(accent.transform, new Color(0.6f, 0.45f, 0.9f), 0.8f, 55); break;
            case CresseliaEffect.Moonlight:
                MeshNode("HealingBoundary", accent.transform, ring, gold, Vector3.up * 0.07f, Vector3.one);
                MeshNode("InnerBoundary", accent.transform, ring, lavender, Vector3.up * 0.08f, Vector3.one * 0.94f);
                var glyph = MeshNode("MoonSigil", accent.transform, crescent, pearl, Vector3.up * 0.09f, Vector3.one * 0.45f);
                Set(view, "boostGlow", glyph);
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * Mathf.PI / 3;
                    MeshNode("LunarGlyph" + i, accent.transform, crescent, pink,
                        new Vector3(Mathf.Sin(angle) * 0.78f, 0.1f, Mathf.Cos(angle) * 0.78f), Vector3.one * 0.08f).transform.localRotation = Quaternion.Euler(0, i * 60, 0);
                }
                Sparks(accent.transform, new Color(0.9f, 0.8f, 1), 0.85f, 14); break;
            case CresseliaEffect.HealPulse:
                MeshNode("HealingPulse", accent.transform, ring, pearl, Vector3.up * 0.12f, Vector3.one);
                break;
            case CresseliaEffect.UniteFlight:
                Shell(accent.transform, gold, 3);
                MeshNode("LunarHalo", accent.transform, ring, pearl, Vector3.up * 2.3f, Vector3.one * 0.6f);
                Sparks(accent.transform, new Color(1, 0.8f, 0.65f), 0.8f, 20); break;
            case CresseliaEffect.UniteWarning:
                MeshNode("LandingBoundary", accent.transform, ring, pink, Vector3.up * 0.08f, Vector3.one);
                var warning = Node("RotatingWarningCrescents", accent.transform);
                Petals(warning.transform, 8, 0.92f, lavender); Set(view, "boostGlow", warning);
                MeshNode("LandingCenter", accent.transform, crescent, pink, Vector3.up * 0.12f, Vector3.one * 0.18f); break;
            case CresseliaEffect.Passive:
                var escort = Node("EscortGlow", accent.transform);
                MeshNode("EscortCrescent", escort.transform, crescent, lavender, Vector3.up * 0.08f, Vector3.one * 0.75f);
                var boost = Node("BoostReady", accent.transform);
                MeshNode("ReadyCrescent", boost.transform, crescent, gold, Vector3.up * 2.05f, Vector3.one * 0.25f).transform.localRotation = Quaternion.Euler(90, 0, 0);
                Set(view, "escortGlow", escort); Set(view, "boostGlow", boost); break;
            case CresseliaEffect.BeamHeal:
                Primitive("ReturningMoonlight", accent.transform, PrimitiveType.Sphere, pearl, Vector3.zero, Vector3.one * 0.18f);
                Trail(accent.transform, gold, 0.1f); break;
        }
        var sound = root.AddComponent<AudioSource>(); sound.playOnAwake = false;
        sound.spatialBlend = effect == CresseliaEffect.Passive ? 0 : 1; sound.minDistance = 2; sound.maxDistance = 18; sound.volume = 0.16f;
        bool healing = effect == CresseliaEffect.ShieldHeal || effect == CresseliaEffect.Cleanse || effect == CresseliaEffect.HealPulse || effect == CresseliaEffect.Passive;
        sound.clip = Sound(healing ? "LunarChime" : effect == CresseliaEffect.UniteImpact ? "CrescentCrash" : "DreamCast");
        Set(view, "sound", sound);
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }

    private static GameObject Node(string name, Transform parent = null)
    {
        var go = new GameObject(name) { layer = 8 };
        SceneManager.MoveGameObjectToScene(go, preview);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = Node(name, parent); go.transform.localPosition = position; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return go;
    }
    private static GameObject Primitive(string name, Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = 8;
        SceneManager.MoveGameObjectToScene(go, preview); go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return go;
    }
    private static void Shell(Transform parent, Material material, int count)
    {
        for (int i = 0; i < count; i++)
            MeshNode("ProtectiveCrescent" + i, parent, crescent, material, Vector3.up * 0.9f, Vector3.one * 1.05f)
                .transform.localRotation = Quaternion.Euler(65, i * 360f / count, 25);
    }
    private static void Petals(Transform parent, int count, float radius, Material material)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2 / count;
            MeshNode("CrescentPetal" + i, parent, crescent, material, new Vector3(Mathf.Sin(angle) * radius, 0.15f, Mathf.Cos(angle) * radius),
                Vector3.one * 0.13f).transform.localRotation = Quaternion.Euler(0, i * 360f / count, 0);
        }
    }
    private static void Sparks(Transform parent, Color color, float radius, float rate)
    {
        var go = Node("LunarWisps", parent); var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.startColor = color; main.startLifetime = 0.5f; main.startSpeed = 0.5f; main.startSize = 0.055f;
        main.maxParticles = 50; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission; emission.rateOverTime = rate;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius;
        shape.radiusThickness = 0.1f; shape.rotation = new Vector3(90, 0, 0);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particle;
    }
    private static void Trail(Transform parent, Material material, float width)
    {
        var go = Node("MoonTrail", parent); var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = material; trail.time = 0.18f; trail.startWidth = width; trail.endWidth = 0; trail.minVertexDistance = 0.06f;
        trail.shadowCastingMode = ShadowCastingMode.Off;
    }
    private static Material Material(string name, Color color)
    {
        string path = Materials + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/DragonEnergy.mat");
        if (source == null) throw new InvalidOperationException("Duraludon's authored DragonEnergy material is required.");
        var result = new Material(source) { name = name }; result.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(result, path); return result;
    }
    private static Mesh BakeArc(string name, bool isCrescent)
    {
        string path = Folder + "Art/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= 64; i++)
        {
            float t = i / 64f, angle = (t - 0.5f) * (isCrescent ? 280 : 360) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            float width = isCrescent ? Mathf.Sin(t * Mathf.PI) * 0.27f : 0.025f;
            vertices.Add(d * (1 - width)); vertices.Add(d);
            if (i < 64) { int n = i * 2; triangles.AddRange(new[] { n, n + 1, n + 2, n + 1, n + 3, n + 2 }); }
        }
        var mesh = new Mesh { name = name, vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static AudioClip Sound(string name)
    {
        string path = Folder + "Art/" + name + ".wav";
        if (!File.Exists(path))
        {
            const int rate = 22050; int count = (int)(rate * (name == "CrescentCrash" ? 0.55f : 0.3f));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)count;
                    double phase = i * 2 * Math.PI / rate;
                    double envelope = Math.Sin(Math.PI * t) * Math.Exp(-t * 4);
                    double sample = name == "CrescentCrash" ? Math.Sin(phase * (120 - 70 * t)) * 0.6 + Math.Sin(phase * 620) * 0.15
                        : name == "LunarChime" ? Math.Sin(phase * 880) * 0.4 + Math.Sin(phase * 1320) * 0.2
                        : Math.Sin(phase * (520 + 260 * t)) * 0.45 + Math.Sin(phase * 1040) * 0.12;
                    writer.Write((short)(sample * envelope * 22000));
                }
                File.WriteAllBytes(path, stream.ToArray());
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    private static void ConfigureAnimations()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Model/Pokemons/Cresselia/Animations/Cresselia.controller");
        const string parameter = "CresseliaActionSpeed";
        bool exists = false; foreach (var item in controller.parameters) if (item.name == parameter) exists = true;
        if (!exists) controller.AddParameter(new AnimatorControllerParameter { name = parameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        foreach (var child in controller.layers[0].stateMachine.states)
        {
            string name = child.state.name;
            if (name != "pm0488_ba20_buturi01" && name != "pm0488_ba21_tokusyu01" && name != "pm0488_ba21_tokusyu02"
                && name != "pm0488_ba01_land01" && name != "pm0488_ba02_roar01") continue;
            child.state.speedParameter = parameter; child.state.speedParameterActive = true; EditorUtility.SetDirty(child.state);
        }
        EditorUtility.SetDirty(controller);
    }
    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
