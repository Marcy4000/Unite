using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Duraludon-style create-missing authoring. All meshes/VFX are saved assets, never built during a match.</summary>
public static class TrevenantAssetAuthoring
{
    public static readonly AvailableMoves[] Ids = { AvailableMoves.TrevenantWillOWisp, AvailableMoves.TrevenantBranchPoke,
        AvailableMoves.TrevenantWoodHammer, AvailableMoves.TrevenantCurse, AvailableMoves.TrevenantHornLeech,
        AvailableMoves.TrevenantPainSplit, AvailableMoves.TrevenantPhantomForest };
    private const string ResourcesFolder = "Assets/Resources/Effects/Trevenant/";
    private const string Art = TrevenantMoveRules.Folder + "Art/";
    private static Scene preview;
    private static Mesh ring, arc;
    private static Material green, purple, bark, core, aim, particles;

    [MenuItem("Tools/Characters/Trevenant/Create Missing Move Assets")]
    public static void Build()
    {
        foreach (string path in new[] { Art.TrimEnd('/'), ResourcesFolder.TrimEnd('/'), "Assets/Materials/Trevenant", "Assets/Moves/Trevenant" }) Folder(path);
        ring = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Duraludon/Art/ThinRing.asset");
        if (ring == null) throw new InvalidOperationException("Missing Duraludon reference ring mesh.");
        arc = SectorOutline();
        green = Material("SpectralWood", new Color(0.3f, 0.95f, 0.12f));
        purple = Material("GhostViolet", new Color(0.62f, 0.05f, 0.95f));
        bark = Material("HauntedBark", new Color(0.14f, 0.07f, 0.2f));
        core = Material("SpiritCore", new Color(0.9f, 0.5f, 1));
        aim = Material("AimMint", new Color(0.3f, 0.95f, 0.9f));
        particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        preview = EditorSceneManager.NewPreviewScene();
        try
        {
            Effect<TrevenantWillOWispEffect>(Ids[0], Wisp);
            Effect<TrevenantBranchPokeEffect>(Ids[1], (root, cast, active, impact, effect) =>
            {
                Branch(active.transform, new Vector3(0, 0.8f, 0), new Vector3(0, 0.8f, TrevenantMoveRules.BranchRange), green, 0.16f);
                Wave(impact.transform, green, TrevenantMoveRules.BranchRadius);
                HitSphere(root, TrevenantMoveRules.BranchRadius);
            });
            Effect<TrevenantWoodHammerEffect>(Ids[2], (root, cast, active, impact, effect) =>
            {
                MeshNode("SweepArc", active.transform, arc, green, Vector3.up * 0.3f, Vector3.one * TrevenantMoveRules.HammerRange);
                for (int i = -1; i <= 1; i += 2) Hammer(active.transform, new Vector3(i * 2.2f, 1, 1), 0.7f);
                Hammer(impact.transform, new Vector3(0, 1.5f, 2.5f), 1.3f);
                Wave(impact.transform, core, 2); HitBox(root, TrevenantMoveRules.HammerSecondWidth, TrevenantMoveRules.HammerSecondRange);
            });
            Effect<TrevenantCurseEffect>(Ids[3], (root, cast, active, impact, effect) =>
            {
                var zone = Node("CursedZone", active.transform); Set(effect, "zone", zone.transform);
                Wave(zone.transform, purple, 1); Sparks(zone.transform, new Color(0.65f, 0.1f, 1), 0.9f, 45);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4;
                    Branch(zone.transform, new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.75f,
                        new Vector3(Mathf.Sin(a), 0.6f, Mathf.Cos(a)) * 0.95f, bark, 0.04f);
                }
                zone.transform.localScale = Vector3.one * TrevenantMoveRules.CurseRadius;
                Wave(impact.transform, purple, TrevenantMoveRules.AuthoredCurseRadius); Sparks(impact.transform, Color.magenta, TrevenantMoveRules.AuthoredCurseRadius, 100);
                HitSphere(root, TrevenantMoveRules.CurseRadius);
            });
            Effect<TrevenantHornLeechEffect>(Ids[4], (root, cast, active, impact, effect) =>
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    Branch(active.transform, new Vector3(i * 0.4f, 0.4f, 0), new Vector3(i * 1.3f, 1, 2.5f), green, 0.18f);
                    Branch(active.transform, new Vector3(i, 0.8f, 1.5f), new Vector3(i * 1.5f, 1.8f, 2.5f), green, 0.09f);
                }
                Wave(impact.transform, green, 1.5f); HitBox(root, TrevenantMoveRules.HornWidth, 2);
            });
            Effect<TrevenantPainSplitEffect>(Ids[5], (root, cast, active, impact, effect) =>
            {
                var tether = Primitive("SpiritTether", active.transform, PrimitiveType.Cube, purple, Vector3.up, new Vector3(0.12f, 0.12f, 1));
                Set(effect, "tether", tether.transform); Wave(active.transform, purple, 0.8f); Wave(impact.transform, core, 0.8f);
            });
            Effect<TrevenantPhantomForestEffect>(Ids[6], (root, cast, active, impact, effect) =>
            {
                Wave(cast.transform, purple, TrevenantMoveRules.UniteRadius);
                Wave(impact.transform, purple, TrevenantMoveRules.UniteRadius);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2 / 5;
                    var tree = Node("PhantomTree" + i, impact.transform);
                    tree.transform.localPosition = new Vector3(Mathf.Sin(a) * 2.5f, 0, Mathf.Cos(a) * 2.5f);
                    Branch(tree.transform, Vector3.zero, Vector3.up * 4, bark, 0.3f);
                    for (int j = -1; j <= 1; j += 2)
                    {
                        Branch(tree.transform, new Vector3(0, 2, 0), new Vector3(j * 1.2f, 3.4f, 0.3f), bark, 0.12f);
                        Branch(tree.transform, new Vector3(j * 1.2f, 3.4f, 0.3f), new Vector3(j, 4, -0.2f), purple, 0.05f);
                    }
                    Primitive("GhostEye", tree.transform, PrimitiveType.Sphere, core, new Vector3(0, 2.2f, -0.3f), Vector3.one * 0.22f);
                }
                Sparks(impact.transform, new Color(0.7f, 0.08f, 1), TrevenantMoveRules.UniteRadius, 100); HitSphere(root, TrevenantMoveRules.UniteRadius);
            });
            Visual("Claw"); Visual("Healing"); Visual("ForestMark"); Indicators();
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        TrevenantWoodHammerAuthoring.Build();
        foreach (var id in Ids) Register(TrevenantMoveRules.Folder + id + ".prefab");
        CopyIcon("S11_1", "HammerFollowup"); CopyIcon("S12_1", "CurseDetonate");
        ConfigureMoves(); AssetDatabase.SaveAssets();
        Debug.Log("Trevenant: seven moves, Natural Cure, learnset, authored effects and registrations ready. Existing models/stats preserved.");
    }
    private static void Effect<T>(AvailableMoves id, Action<GameObject, GameObject, GameObject, GameObject, T> author) where T : TrevenantMoveEffect
    {
        string path = TrevenantMoveRules.Folder + id + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(id.ToString()); root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var graphics = Node("Graphics", root.transform); var effect = root.AddComponent<T>();
        var cast = Node("Cast", graphics.transform); var active = Node("Active", graphics.transform); var impact = Node("Impact", graphics.transform);
        Set(effect, "graphics", graphics); Set(effect, "castVisual", cast); Set(effect, "activeVisual", active); Set(effect, "impactVisual", impact);
        Wave(cast.transform, green, 0.8f); author(root, cast, active, impact, effect);
        active.SetActive(false); impact.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void Wisp(GameObject root, GameObject cast, GameObject active, GameObject impact, TrevenantWillOWispEffect effect)
    {
        var data = new SerializedObject(effect); var flames = data.FindProperty("flames"); flames.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var flame = Primitive("Wisp" + i, active.transform, PrimitiveType.Sphere, purple,
                new Vector3((i - 1) * TrevenantMoveRules.WispSpacing, 0.8f, 0), new Vector3(0.55f, 0.9f, 0.55f));
            Primitive("SpiritCore", flame.transform, PrimitiveType.Sphere, core, Vector3.zero, Vector3.one * 0.6f);
            Sparks(flame.transform, Color.magenta, 0.25f, 30); flames.GetArrayElementAtIndex(i).objectReferenceValue = flame.transform;
        }
        data.ApplyModifiedPropertiesWithoutUndo(); Wave(impact.transform, purple, 0.6f); HitSphere(root, TrevenantMoveRules.WispRadius);
    }
    private static void Visual(string name)
    {
        string path = ResourcesFolder + name + ".prefab"; if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(name); root.AddComponent<TimedCombatVisual>();
        if (name == "ForestMark")
        {
            root.AddComponent<TrevenantMarkVisual>(); Wave(root.transform, purple, 1);
            for (int i = -1; i <= 1; i += 2) Branch(root.transform, Vector3.zero, new Vector3(i * 0.5f, 0.8f, 0), green, 0.12f);
        }
        else
        {
            Wave(root.transform, green, 1);
            if (name == "Claw")
                for (int i = -1; i <= 1; i++) Branch(root.transform, new Vector3(i * 0.3f, 0, -0.5f), new Vector3(i * 0.3f, 1, 0.6f), green, 0.04f);
            Sparks(root.transform, name == "Healing" ? Color.green : new Color(0.5f, 1, 0.1f), 0.8f, 30);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void Indicators()
    {
        foreach (bool line in new[] { false, true })
        {
            string path = ResourcesFolder + (line ? "HammerLineIndicator" : "HammerSectorIndicator") + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
            var root = Node(line ? "HammerLineIndicator" : "HammerSectorIndicator");
            if (!line) MeshNode("SectorOutline", root.transform, arc, aim, Vector3.up * 0.05f, Vector3.one * TrevenantMoveRules.AuthoredHammerRange);
            else
            {
                float width = TrevenantMoveRules.AuthoredHammerWidth * 0.5f, length = TrevenantMoveRules.AuthoredHammerLength;
                foreach (float x in new[] { -width, width }) Primitive("Side", root.transform, PrimitiveType.Cube, aim, new Vector3(x, 0.05f, length * 0.5f), new Vector3(0.06f, 0.02f, length));
                Primitive("End", root.transform, PrimitiveType.Cube, aim, new Vector3(0, 0.05f, length), new Vector3(width * 2, 0.02f, 0.06f));
            }
            PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
        }
    }
    private static void ConfigureMoves()
    {
        string[] icons = { "S1", "S2", "S11", "S12", "S21", "S22", "U" };
        string[] images = { "S1", "S2", "S1A", "S1B", "S2A", "S2B", "U1" };
        string[] descriptions = {
            "Shoot three ghost flames. First hit deals full damage; further flames deal half damage. Burn for 3s, reducing Attack by 10% and Sp. Atk by 5%.",
            "Dash through walls and jab enemies, slowing them by 40% for 2s. Recover 9% max HP over three seconds per enemy hit.",
            "Sweep enemies together and stun for 0.5s. On hit, recast within 5s to slam a hammer with a 1.1–1.5s stun based on remaining HP. Each activation costs 5% current HP. Upgrade: second hit reduces damage dealt by 20% for 3s.",
            "Create a cursed zone for 6s, damaging and slowing enemies every 0.5s. Costs 2% max HP per second. Dealing or receiving damage, including self-damage, fills the gauge (20 events). Recast to detonate. At full gauge, stun for 0.6s and reset the other move. Upgrade: larger radius.",
            "Charge forward, unstoppable but unable to cross walls, carrying enemies and throwing them at the end. Heal three times per enemy hit, with more recovery at lower HP. Next three attacks deal bonus damage and reduce Horn Leech cooldown by 0.5s on hit. Upgrade: gain 30% movement speed for 1.5s.",
            "Tether one enemy for 6s, redirecting 30–50% incoming damage as true damage based on remaining HP. Every 0.5s, damage and heal if your HP percentage is lower. Gain decaying movement speed. Breaks only on distance or death; recasting replaces the tether. Upgrade: 8.5s duration.",
            "Summon a phantom forest, damaging, throwing and slowing enemies. Mark for 6s so allied basic attacks trigger bonus damage. Gain a shield per enemy hit, up to five; while that shield remains, received healing increases by 50%. Also gain standard Unite speed and shield buffs." };
        var assets = new MoveAsset[Ids.Length];
        for (int i = 0; i < Ids.Length; i++)
        {
            string path = "Assets/Moves/Trevenant/" + Ids[i] + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<MoveAsset>(path);
            if (asset == null)
            {
                var definition = MoveDatabase.GetMove(Ids[i]); asset = ScriptableObject.CreateInstance<MoveAsset>();
                asset.move = Ids[i]; asset.moveName = definition.Name; asset.cooldown = definition.Cooldown;
                asset.moveType = i == 6 ? MoveType.UniteMove : i == 0 || i == 2 || i == 3 ? MoveType.MoveA : MoveType.MoveB;
                asset.moveLabel = i == 5 ? MoveLabels.SureHit : i == 1 || i == 4 ? MoveLabels.Dash : MoveLabels.Area;
                asset.uniteEnergyCost = i == 6 ? TrevenantMoveRules.UniteEnergy : 0; asset.description = descriptions[i];
                asset.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MoveIcons/Trevenant/t_Skill_Trevenant_" + icons[i] + ".png");
                string image = AssetDatabase.FindAssets("t_Growth_Skill_Phantump_" + images[i] + "_", new[] { "Assets/Sprites/MoveIcons/Trevenant/Previews" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                if (image == null || asset.icon == null) throw new InvalidOperationException("Missing Trevenant move art: " + icons[i]);
                asset.preview = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(image)); AssetDatabase.CreateAsset(asset, path);
            }
            assets[i] = asset;
        }
        var pokemon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Trevenant.asset"));
        pokemon.FindProperty("passive").enumValueIndex = (int)AvailablePassives.TrevenantPassive;
        var rows = pokemon.FindProperty("learnableMoves");
        if (rows.arraySize == 0)
        {
            int[] levels = { 0, 2, 4, 6, 8, 10, 12 };
            int[][] choices = { new[] { 0, 1 }, new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4, 5 }, new[] { 6 }, new[] { 2, 3 }, new[] { 4, 5 } };
            rows.arraySize = levels.Length;
            for (int i = 0; i < levels.Length; i++)
            {
                var row = rows.GetArrayElementAtIndex(i); row.FindPropertyRelative("level").intValue = levels[i]; row.FindPropertyRelative("isUpgraded").boolValue = i >= 5;
                var moves = row.FindPropertyRelative("moves"); moves.arraySize = choices[i].Length;
                for (int j = 0; j < choices[i].Length; j++) moves.GetArrayElementAtIndex(j).objectReferenceValue = assets[choices[i][j]];
            }
        }
        pokemon.ApplyModifiedPropertiesWithoutUndo();
        var info = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/PokemonSelectInfo/Trevenant.asset"));
        info.FindProperty("pClass").enumValueIndex = (int)PokemonClass.Defender; info.FindProperty("Difficulty").enumValueIndex = (int)PokemonDifficulty.Intermediate;
        info.FindProperty("Range").enumValueIndex = (int)PokemonRange.Melee; info.FindProperty("DamageType").enumValueIndex = (int)DamageType.Physical; info.ApplyModifiedPropertiesWithoutUndo();
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset")); var entries = registry.FindProperty("moveEntries");
        foreach (var asset in assets)
        {
            if (Enumerable.Range(0, entries.arraySize).Any(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("moveType").longValue == (long)asset.move)) continue;
            var entry = entries.GetArrayElementAtIndex(entries.arraySize++); entry.FindPropertyRelative("moveType").longValue = (long)asset.move; entry.FindPropertyRelative("moveAsset").objectReferenceValue = asset;
        }
        registry.ApplyModifiedPropertiesWithoutUndo();
        var settings = AddressableAssetSettingsDefaultObject.Settings; var previews = settings.FindGroup("MovePreviews");
        foreach (var asset in assets) settings.CreateOrMoveEntry(asset.preview.AssetGUID, previews).address = AssetDatabase.GUIDToAssetPath(asset.preview.AssetGUID);
        EditorUtility.SetDirty(previews); EditorUtility.SetDirty(settings);
    }
    private static void CopyIcon(string suffix, string name)
    { string path = ResourcesFolder + name + ".png"; if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) AssetDatabase.CopyAsset("Assets/Sprites/MoveIcons/Trevenant/t_Skill_Trevenant_" + suffix + ".png", path); }
    private static Mesh SectorOutline()
    {
        string path = Art + "HammerSector.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= 32; i++)
        {
            float a = -Mathf.PI * 0.5f + i * Mathf.PI / 32;
            vertices.Add(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a))); vertices.Add(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.98f);
            if (i < 32) { int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); }
        }
        int b = vertices.Count; vertices.AddRange(new[] { new Vector3(-1, 0, -0.01f), new Vector3(1, 0, -0.01f), new Vector3(-1, 0, 0.01f), new Vector3(1, 0, 0.01f) });
        triangles.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
        var mesh = new Mesh { name = "HammerSector" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static Material Material(string name, Color color)
    {
        string path = "Assets/Materials/Trevenant/" + name + ".mat"; var result = AssetDatabase.LoadAssetAtPath<Material>(path); if (result != null) return result;
        result = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/DragonEnergy.mat")) { name = name }; result.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(result, path); return result;
    }
    private static GameObject Node(string name, Transform parent = null)
    { var node = new GameObject(name) { layer = 8 }; SceneManager.MoveGameObjectToScene(node, preview); if (parent != null) node.transform.SetParent(parent, false); return node; }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var node = Node(name, parent); node.transform.localPosition = position; node.transform.localScale = scale;
        node.AddComponent<MeshFilter>().sharedMesh = mesh; var r = node.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; return node;
    }
    private static GameObject Primitive(string name, Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale)
    {
        var node = GameObject.CreatePrimitive(type); node.name = name; node.layer = 8; SceneManager.MoveGameObjectToScene(node, preview); node.transform.SetParent(parent, false);
        node.transform.localPosition = position; node.transform.localScale = scale; Object.DestroyImmediate(node.GetComponent<Collider>()); node.GetComponent<MeshRenderer>().sharedMaterial = material; return node;
    }
    private static void Branch(Transform parent, Vector3 from, Vector3 to, Material material, float radius)
    { var node = Primitive("SpectralBranch", parent, PrimitiveType.Cylinder, material, (from + to) * 0.5f, new Vector3(radius, (to - from).magnitude * 0.5f, radius)); node.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from); }
    private static void Hammer(Transform parent, Vector3 position, float scale)
    { var root = Node("WoodenHammer", parent); root.transform.localPosition = position; root.transform.localScale = Vector3.one * scale; Branch(root.transform, Vector3.zero, Vector3.up * 1.3f, green, 0.12f); Primitive("HammerHead", root.transform, PrimitiveType.Cylinder, green, Vector3.up * 1.3f, new Vector3(0.8f, 0.65f, 0.8f)).transform.localRotation = Quaternion.Euler(0, 0, 90); }
    private static void Wave(Transform parent, Material material, float radius) => MeshNode("SpiritRing", parent, ring, material, Vector3.up * 0.12f, Vector3.one * radius);
    private static void HitSphere(GameObject root, float radius) { var c = Node("AuthoredHitbox", root.transform).AddComponent<SphereCollider>(); c.radius = radius; c.center = Vector3.up * 0.8f; c.isTrigger = true; c.enabled = false; }
    private static void HitBox(GameObject root, float width, float length) { var c = Node("AuthoredHitbox", root.transform).AddComponent<BoxCollider>(); c.size = new Vector3(width, 2, length); c.center = new Vector3(0, 1, length * 0.5f); c.isTrigger = true; c.enabled = false; }
    private static void Sparks(Transform parent, Color color, float radius, float rate)
    {
        var node = Node("SpiritParticles", parent); var system = node.AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.startColor = color; main.startLifetime = 0.45f; main.startSize = 0.1f; main.startSpeed = 0.5f; main.loop = true; main.playOnAwake = true; main.maxParticles = 150; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = system.emission; emission.rateOverTime = rate; var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius; node.GetComponent<ParticleSystemRenderer>().sharedMaterial = particles;
    }
    private static void Set(Object target, string field, Object value) { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Register(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue == 0) throw new InvalidOperationException("Missing network spawn hash: " + path);
        prefab = PrefabUtility.SavePrefabAsset(prefab); var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("MovesAssets");
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path; EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")); var list = registry.FindProperty("List");
        if (Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab)) return;
        var entry = list.GetArrayElementAtIndex(list.arraySize++); entry.FindPropertyRelative("Override").enumValueIndex = 0; entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null; entry.FindPropertyRelative("SourceHashToOverride").longValue = 0; entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null; registry.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Folder(string path)
    { if (AssetDatabase.IsValidFolder(path)) return; string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path)); }
}
