using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Like Duraludon: bake geometry/audio once, preserve hand-edited prefab layouts.
// These are local presentation prefabs; owner-validated RPCs run server gameplay.
public static class VaporeonPrefabBuilder
{
    private const string Folder = VaporMoveRules.EffectFolder;
    private const string Materials = "Assets/Materials/Vaporeon/";
    private static Scene preview;
    private static Mesh ring, spiral, ribbon;
    private static Material water, deep, foam, particles;

    [MenuItem("Tools/Vaporeon/Create Missing Rework Prefabs")]
    public static void Build()
    {
        EnsureFolder(Folder.TrimEnd('/')); EnsureFolder(Folder + "Art"); EnsureFolder(Materials.TrimEnd('/'));
        ring = Bake("FoamRing", 0); spiral = Bake("WhirlpoolSpiral", 1); ribbon = Bake("WaterRibbon", 2);
        water = Material("Water", new Color(0.08f, 0.65f, 0.9f));
        deep = Material("DeepWater", new Color(0.025f, 0.23f, 0.48f));
        foam = Material("SeaFoam", new Color(0.8f, 0.98f, 1));
        particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        preview = EditorSceneManager.NewPreviewScene();
        try { foreach (VaporEffect effect in Enum.GetValues(typeof(VaporEffect))) Create(effect); }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null || settings.FindGroup("MovesAssets") == null) throw new InvalidOperationException("MovesAssets Addressable group is required.");
        var group = settings.FindGroup("MovesAssets");
        foreach (VaporEffect effect in Enum.GetValues(typeof(VaporEffect)))
        {
            string path = VaporMoveRules.EffectPath(effect);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
        }
        EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        ConfigureMoves(); ConfigureAnimations();
        AssetDatabase.SaveAssets();
        Debug.Log("Vaporeon rework authored: Whirlpool replaces unused move ID 26; all seven moves, upgrades and local effects registered.");
    }
    private static void Create(VaporEffect effect)
    {
        string path = VaporMoveRules.EffectPath(effect);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node("Vapor" + effect); var view = root.AddComponent<VaporVisual>();
        var graphics = Node("Graphics", root.transform);
        var accent = Node("Accent", graphics.transform);
        var rotating = Node("WaterMotion", accent.transform);
        Set(view, "graphics", graphics); Set(view, "accent", accent.transform); Set(view, "rotating", rotating.transform);
        bool area = effect == VaporEffect.Rain || effect == VaporEffect.Whirlpool || effect == VaporEffect.Swift
            || effect == VaporEffect.Aim || effect == VaporEffect.DiveWake || effect == VaporEffect.DiveWarning || effect == VaporEffect.TempestWarning;
        if (area)
        {
            MeshNode("AreaBoundary", accent.transform, ring, water, Vector3.up * 0.08f, Vector3.one);
            MeshNode("InnerFoam", accent.transform, ring, foam, Vector3.up * 0.10f, Vector3.one * 0.95f);
            var timer = MeshNode("RemainingDuration", accent.transform, ring, deep, Vector3.up * 0.11f, Vector3.one * 0.8f);
            Set(view, "progress", timer.transform);
            var teeth = Node("HostileTeeth", accent.transform);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                var tooth = MeshNode("WarningTooth" + i, teeth.transform, ribbon, foam,
                    new Vector3(Mathf.Sin(a), 0.12f, Mathf.Cos(a)), new Vector3(0.08f, 1, 0.15f));
                tooth.transform.localRotation = Quaternion.Euler(0, i * 30, 0);
            }
            Set(view, "enemyTeeth", teeth);
        }
        switch (effect)
        {
            case VaporEffect.Basic:
            case VaporEffect.Boosted:
                bool boosted = effect == VaporEffect.Boosted;
                Primitive("CompressedWater", accent.transform, PrimitiveType.Sphere, water, Vector3.zero, new Vector3(boosted ? 0.4f : 0.22f, boosted ? 0.4f : 0.22f, boosted ? 0.65f : 0.4f));
                Primitive("FoamTip", accent.transform, PrimitiveType.Sphere, foam, Vector3.forward * 0.15f, Vector3.one * (boosted ? 0.22f : 0.1f));
                Trail(accent.transform, boosted ? foam : water, boosted ? 0.23f : 0.1f); break;
            case VaporEffect.Swift:
                for (int i = 0; i < 4; i++)
                    MeshNode("FallingStar" + i, rotating.transform, ribbon, foam,
                        new Vector3(Mathf.Sin(i * 1.7f) * 0.65f, 0.15f, Mathf.Cos(i * 1.7f) * 0.65f), Vector3.one * 0.25f);
                Droplets(rotating.transform, false, 1, 24); break;
            case VaporEffect.AquaRing:
                for (int i = 0; i < 3; i++)
                {
                    var shell = MeshNode("ProtectiveRibbon" + i, rotating.transform, ring, i == 1 ? foam : water,
                        Vector3.up * 0.85f, Vector3.one * 1.05f);
                    shell.transform.localRotation = Quaternion.Euler(65, i * 60, 25);
                }
                MeshNode("ShieldFooting", accent.transform, ring, deep, Vector3.up * 0.12f, Vector3.one * 0.85f);
                Droplets(rotating.transform, false, 0.75f, 8); break;
            case VaporEffect.HelpingHand:
                for (int i = 0; i < 2; i++)
                    MeshNode("SupportRibbon" + i, rotating.transform, spiral, i == 0 ? water : foam,
                        Vector3.up * (0.5f + i * 0.4f), Vector3.one * 0.7f);
                break;
            case VaporEffect.Rain:
                for (int i = 0; i < 3; i++) MeshNode("RainRipple" + i, rotating.transform, ring, i == 1 ? foam : water,
                    Vector3.up * (0.09f + i * 0.01f), Vector3.one * (0.25f + i * 0.2f));
                Droplets(accent.transform, true, 0.95f, 65); break;
            case VaporEffect.Whirlpool:
                for (int i = 0; i < 3; i++)
                {
                    var arm = MeshNode("InwardSpiral" + i, rotating.transform, spiral, i == 1 ? foam : water,
                        Vector3.up * (0.12f + i * 0.02f), Vector3.one * 0.9f);
                    arm.transform.localRotation = Quaternion.Euler(0, i * 120, 0);
                }
                Droplets(rotating.transform, false, 0.85f, 16); break;
            case VaporEffect.DiveWake:
                for (int i = 0; i < 2; i++) MeshNode("SurfaceWake" + i, rotating.transform, ribbon, i == 0 ? water : foam,
                    new Vector3(0, 0.13f, -0.2f * i), Vector3.one * (0.6f + i * 0.2f));
                break;
            case VaporEffect.DiveWarning:
            case VaporEffect.TempestWarning:
                MeshNode("GatheringWater", rotating.transform, spiral, foam, Vector3.up * 0.13f, Vector3.one * 0.85f);
                break;
            case VaporEffect.Collapse:
                MeshNode("CollapseFoam", accent.transform, spiral, foam, Vector3.up * 0.15f, Vector3.one);
                MeshNode("CollapseWater", accent.transform, ring, water, Vector3.up * 0.12f, Vector3.one); break;
            case VaporEffect.TempestImpact:
            case VaporEffect.DiveSplash:
            case VaporEffect.ShieldBreak:
                MeshNode("OutwardWave", accent.transform, ring, foam, Vector3.up * 0.16f, Vector3.one);
                MeshNode("WaterWave", accent.transform, ring, water, Vector3.up * 0.12f, Vector3.one * 0.8f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4;
                    var splash = MeshNode("Splash" + i, accent.transform, ribbon, i % 2 == 0 ? water : foam,
                        new Vector3(Mathf.Sin(a) * 0.6f, 0.2f, Mathf.Cos(a) * 0.6f), new Vector3(0.16f, 1, 0.45f));
                    splash.transform.localRotation = Quaternion.Euler(-35, i * 45, 0);
                }
                Droplets(accent.transform, false, 0.7f, 80); break;
            case VaporEffect.HealPulse:
                MeshNode("HealingRipple", accent.transform, ring, foam, Vector3.up * 0.13f, Vector3.one);
                MeshNode("HealingRibbon", accent.transform, spiral, water, Vector3.up * 0.15f, Vector3.one * 0.7f); break;
            case VaporEffect.Hydration:
                for (int i = 0; i < 3; i++) MeshNode("FlowStream" + i, rotating.transform, ribbon, i == 1 ? foam : water,
                    new Vector3((i - 1) * 0.35f, 0.2f, -0.5f), new Vector3(0.2f, 1, 0.9f));
                break;
            case VaporEffect.BoostReady:
                MeshNode("BoostedReadyHalo", rotating.transform, ring, foam, Vector3.up * 1.8f, Vector3.one * 0.3f);
                for (int i = 0; i < 3; i++) Primitive("ReadyDrop" + i, rotating.transform, PrimitiveType.Sphere, water,
                    new Vector3(Mathf.Sin(i * Mathf.PI * 2 / 3) * 0.3f, 1.8f, Mathf.Cos(i * Mathf.PI * 2 / 3) * 0.3f), new Vector3(0.12f, 0.18f, 0.12f));
                break;
        }
        if (effect != VaporEffect.Aim && effect != VaporEffect.BoostReady && effect != VaporEffect.DiveWake)
        {
            var audio = root.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 1;
            audio.minDistance = 2; audio.maxDistance = 20; audio.volume = effect == VaporEffect.HealPulse ? 0.06f : 0.14f;
            audio.clip = Sound(effect == VaporEffect.Collapse || effect == VaporEffect.TempestImpact || effect == VaporEffect.ShieldBreak ? "WaterCrash"
                : effect == VaporEffect.AquaRing || effect == VaporEffect.HealPulse || effect == VaporEffect.Hydration ? "WaterChime" : "WaterCast");
            Set(view, "sound", audio);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static GameObject Node(string name, Transform parent = null)
    {
        var go = new GameObject(name) { layer = 8 }; SceneManager.MoveGameObjectToScene(go, preview);
        if (parent != null) go.transform.SetParent(parent, false); return go;
    }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = Node(name, parent); go.transform.localPosition = position; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return go;
    }
    private static void Primitive(string name, Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = 8; SceneManager.MoveGameObjectToScene(go, preview);
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>()); var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }
    private static void Droplets(Transform parent, bool rain, float radius, float rate)
    {
        var go = Node(rain ? "RainDrops" : "SeaSpray", parent); go.transform.localPosition = Vector3.up * (rain ? 3.2f : 0.1f);
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.startColor = new Color(0.45f, 0.88f, 1, 0.8f); main.startLifetime = rain ? 0.5f : 0.3f;
        main.startSpeed = rain ? 0 : 0.5f; main.startSize = rain ? 0.045f : 0.07f; main.maxParticles = 96;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission; emission.rateOverTime = rate;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius;
        shape.rotation = new Vector3(90, 0, 0); shape.radiusThickness = rain ? 1 : 0.15f;
        if (rain) { var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.y = -6f; }
        var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = particles;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }
    private static void Trail(Transform parent, Material material, float width)
    {
        var trail = Node("WaterTrail", parent).AddComponent<TrailRenderer>(); trail.sharedMaterial = material;
        trail.time = 0.16f; trail.startWidth = width; trail.endWidth = 0; trail.minVertexDistance = 0.05f; trail.shadowCastingMode = ShadowCastingMode.Off;
    }
    private static Material Material(string name, Color color)
    {
        string path = Materials + name + ".mat"; var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/DragonEnergy.mat");
        if (source == null) throw new InvalidOperationException("Duraludon DragonEnergy material is required.");
        var material = new Material(source) { name = name }; material.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(material, path); return material;
    }
    private static Mesh Bake(string name, int kind)
    {
        string path = Folder + "Art/" + name + ".asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= 96; i++)
        {
            float t = i / 96f, angle = t * Mathf.PI * (kind == 1 ? 3.4f : kind == 2 ? 1.2f : 2);
            float radius = kind == 1 ? Mathf.Lerp(0.96f, 0.08f, t) : 1;
            float width = kind == 0 ? 0.024f : Mathf.Sin(Mathf.PI * Mathf.Clamp(t, 0.01f, 0.99f)) * 0.08f;
            Vector3 direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            vertices.Add(direction * Mathf.Max(0, radius - width)); vertices.Add(direction * radius);
            if (i < 96) { int n = i * 2; triangles.AddRange(new[] { n, n + 1, n + 2, n + 1, n + 3, n + 2, n + 2, n + 1, n, n + 2, n + 3, n + 1 }); }
        }
        var mesh = new Mesh { name = name, vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        // The double-sided ribbons share vertices; recalculating would cancel
        // opposing normals. Author an explicit upward normal for ground VFX.
        var normals = new Vector3[vertices.Count]; for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
        mesh.normals = normals; mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static AudioClip Sound(string name)
    {
        string path = Folder + "Art/" + name + ".wav";
        if (!File.Exists(path))
        {
            const int rate = 22050; int count = (int)(rate * (name == "WaterCrash" ? 0.45f : 0.25f));
            var random = new System.Random(134);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    double t = i / (double)count, phase = i * 2 * Math.PI / rate;
                    double wave = name == "WaterChime" ? Math.Sin(phase * 740) * 0.3 + Math.Sin(phase * 1110) * 0.14
                        : Math.Sin(phase * (name == "WaterCrash" ? 130 - 60 * t : 320 + 160 * t)) * 0.25 + (random.NextDouble() * 2 - 1) * 0.2;
                    writer.Write((short)(wave * Math.Sin(Math.PI * t) * Math.Exp(-t * 3) * 24000));
                }
                File.WriteAllBytes(path, stream.ToArray());
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    private static Sprite WhirlpoolIcon()
    {
        const string path = "Assets/Sprites/MoveIcons/Vaporeon/VaporeonWhirlpool.png";
        if (!File.Exists(path))
        {
            const int side = 256; var texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
            var colors = new Color[side * side];
            for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
            {
                float px = (x - 127.5f) / 120, py = (y - 127.5f) / 120, radius = Mathf.Sqrt(px * px + py * py);
                float angle = Mathf.Atan2(py, px), wave = Mathf.Sin(angle * 3 + radius * 16);
                float arm = Mathf.SmoothStep(0.4f, 0.95f, wave) * Mathf.Clamp01((1 - radius) * 7);
                Color color = Color.Lerp(new Color(0.02f, 0.12f, 0.26f), new Color(0.22f, 0.85f, 1), arm);
                if (radius < 0.92f && radius > 0.87f) color = new Color(0.7f, 0.95f, 1);
                if (radius > 1) color.a = 0; colors[y * side + x] = color;
            }
            texture.SetPixels(colors); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static void ConfigureMoves()
    {
        var whirlpool = AssetDatabase.LoadAssetAtPath<MoveAsset>("Assets/Moves/Vaporeon/VaporWhirlpool.asset");
        if (whirlpool == null) { whirlpool = ScriptableObject.CreateInstance<MoveAsset>(); AssetDatabase.CreateAsset(whirlpool, "Assets/Moves/Vaporeon/VaporWhirlpool.asset"); }
        whirlpool.move = AvailableMoves.VaporWhirlpool; whirlpool.moveType = MoveType.MoveB; whirlpool.moveLabel = MoveLabels.Hinderance;
        whirlpool.moveName = "Whirlpool"; whirlpool.cooldown = 11; whirlpool.icon = WhirlpoolIcon();
        whirlpool.preview = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(whirlpool.icon)));
        whirlpool.description = "Create a 3s whirlpool at a location within 7m. Enemies inside are slowed by 25% and take six small damage pulses. After 0.9s, enemies still inside are pulled inward once (up to 1.25m). Overlapping slows do not stack; recently pulled enemies resist another Whirlpool pull for 1.5s. Does not displace objectives or hindrance-immune enemies. Upgrade: slow increases to 35%.";
        EditorUtility.SetDirty(whirlpool);
        ConfigureMove("VaporSwift", "Summon exactly four volleys of falling stars in a designated 1m-radius area, one every 0.7s. Each volley damages enemies hit.");
        ConfigureMove("VaporHelpingHand", "Grant a designated ally +35% Attack and Sp. Attack for 3.5s. Can be cast while moving. If no ally is selected, the move has no effect and does not go on cooldown.");
        ConfigureMove("VaporAquaRing", "Protect an ally within 8m, or yourself if no ally is selected, with a shield worth 20% of their max HP for 6s. Grant +25% Attack and Sp. Attack for up to 4.5s and eight healing pulses, starting shortly after casting. If the shield breaks, the ring and its remaining effects end. Recasting replaces your ring on that target. Upgrade: shield increases to 25% max HP.");
        ConfigureMove("VaporRainDance", "Hop up to 4m to a nearby reachable location, then summon rain in a 3m radius for 10s. Allies inside gain +55% Defense and Sp. Defense and are healed every 1.2s. Enemies inside are slowed by 20%. Effects end when leaving the rain; overlapping Vaporeon field slows do not stack. Upgrade: defensive bonuses increase to 65%.");
        ConfigureMove("VaporDive", "Submerge for up to 3s, becoming invincible while moving along the ground. Recast after 0.25s to resurface early. Other actions are unavailable underwater. After a brief 0.25s warning, resurface to damage nearby enemies, briefly incapacitate them and push them back slightly. Cooldown starts on resurfacing. Upgrade: cooldown is reduced to 9s.");
        ConfigureMove("VaporeonUnite", "Gather water for 0.6s, then release a 6m-radius tempest at the cast location. Damage enemies and reduce both defenses by 35% for 5s; significantly heal living allies, including yourself. Movement and other actions are restricted during the cast. Hindrances can interrupt the windup.");
        var registry = new SerializedObject(AssetDatabase.LoadAssetAtPath<MoveAssetRegistry>("Assets/Moves/MoveAssetRegistry.asset"));
        var entries = registry.FindProperty("moveEntries"); int index = -1;
        for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("moveType").longValue == 26) index = i;
        if (index < 0) { index = entries.arraySize; entries.arraySize++; }
        entries.GetArrayElementAtIndex(index).FindPropertyRelative("moveType").longValue = 26;
        entries.GetArrayElementAtIndex(index).FindPropertyRelative("moveAsset").objectReferenceValue = whirlpool; registry.ApplyModifiedPropertiesWithoutUndo();
        var pokemon = new SerializedObject(AssetDatabase.LoadAssetAtPath<PokemonBase>("Assets/Pokemons/PlayableMons/Vaporeon.asset"));
        pokemon.FindProperty("passive").intValue = (int)AvailablePassives.VaporeonPassive;
        pokemon.FindProperty("description").stringValue = "A durable ranged Supporter who protects allies and controls where teams fight. Hydration: successfully protecting or healing another ally grants Vaporeon 10% movement speed for 1.5s, once every 6s after evolving. Choose Aqua Ring for focused protection or Rain Dance for an allied stronghold, then Dive for personal safety or Whirlpool for ranged control.";
        var learnset = pokemon.FindProperty("learnableMoves");
        foreach (int row in new[] { 3, 6 })
        {
            var moves = learnset.GetArrayElementAtIndex(row).FindPropertyRelative("moves");
            moves.arraySize = 2; moves.GetArrayElementAtIndex(1).objectReferenceValue = whirlpool;
        }
        pokemon.ApplyModifiedPropertiesWithoutUndo();
        var info = AssetDatabase.LoadAssetAtPath<CharacterInfo>("Assets/Pokemons/PlayableMons/PokemonSelectInfo/Vaporeon.asset");
        info.Range = PokemonRange.Ranged; EditorUtility.SetDirty(info);
    }
    private static void ConfigureMove(string name, string description)
    {
        var asset = AssetDatabase.LoadAssetAtPath<MoveAsset>("Assets/Moves/Vaporeon/" + name + ".asset");
        if (asset == null) throw new InvalidOperationException("Missing Vaporeon move: " + name);
        asset.description = description;
        if (name == "VaporAquaRing" || name == "VaporHelpingHand") asset.moveLabel = MoveLabels.Buff;
        EditorUtility.SetDirty(asset);
    }
    private static void ConfigureAnimations()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Model/Pokemons/Vaporeon/Vaporeon.controller");
        const string parameter = "VaporeonActionSpeed";
        bool exists = false; foreach (var p in controller.parameters) if (p.name == parameter) exists = true;
        if (!exists) controller.AddParameter(new AnimatorControllerParameter { name = parameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        foreach (var child in controller.layers[0].stateMachine.states)
        {
            if (!child.state.name.Contains("ba21_tokusyu") && !child.state.name.Contains("ba01_land")
                && !child.state.name.Contains("ba02_roar") && !child.state.name.Contains("kw35_play")) continue;
            child.state.speedParameter = parameter; child.state.speedParameterActive = true; EditorUtility.SetDirty(child.state);
        }
        EditorUtility.SetDirty(controller);
    }
    private static void Set(Object target, string property, Object value)
    { var serialized = new SerializedObject(target); serialized.FindProperty(property).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
