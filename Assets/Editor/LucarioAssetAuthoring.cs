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

/// <summary>Create-missing, Duraludon-style editable VFX. Never overwrites the imported model, animator or stats.</summary>
public static class LucarioAssetAuthoring
{
    public static readonly AvailableMoves[] Ids = { AvailableMoves.LucarioQuickAttack, AvailableMoves.LucarioMeteorMash,
        AvailableMoves.LucarioExtremeSpeed, AvailableMoves.LucarioPowerUpPunch, AvailableMoves.LucarioBoneRush,
        AvailableMoves.LucarioCloseCombat, AvailableMoves.LucarioAuraCannon };
    private const string ResourcesFolder = "Assets/Resources/Effects/Lucario/";
    private const string Materials = "Assets/Materials/Lucario/";
    private static Scene preview;
    private static Mesh ring;
    private static Material aura, white, dark, gold, particles;

    [MenuItem("Tools/Characters/Lucario/Create Missing Move Assets")]
    public static void Build()
    {
        foreach (var path in new[] { LucarioMoveRules.Folder.TrimEnd('/'), ResourcesFolder.TrimEnd('/'), Materials.TrimEnd('/'), "Assets/Moves/Lucario" }) Folder(path);
        ring = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Duraludon/Art/ThinRing.asset");
        if (ring == null) throw new InvalidOperationException("Missing Duraludon reference ring.");
        aura = Material("AuraBlue", new Color(0.04f, 0.55f, 1)); white = Material("AuraCore", new Color(0.68f, 0.95f, 1));
        dark = Material("AuraDeep", new Color(0.025f, 0.07f, 0.35f)); gold = Material("MeteorGold", new Color(1, 0.75f, 0.1f));
        particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        preview = EditorSceneManager.NewPreviewScene();
        try
        {
            Effect<LucarioQuickAttackEffect>(Ids[0], (root, cast, active, impact, effect) => DashArt(active, impact, false));
            Effect<LucarioMeteorMashEffect>(Ids[1], (root, cast, active, impact, effect) =>
            {
                for (int i = -1; i <= 1; i++)
                    Primitive("MeteorStreak", active.transform, PrimitiveType.Cube, i == 0 ? gold : white,
                        new Vector3(i * 0.6f, 1, LucarioMoveRules.MeteorRange * 0.5f), new Vector3(0.1f, 0.14f, LucarioMoveRules.MeteorRange));
                Wave(impact.transform, gold, 1.5f);
            });
            Effect<LucarioExtremeSpeedEffect>(Ids[2], (root, cast, active, impact, effect) => DashArt(active, impact, true));
            Effect<LucarioPowerUpPunchEffect>(Ids[3], (root, cast, active, impact, effect) =>
            {
                var orb = Orb("ChargeOrb", cast.transform, 0.5f); orb.transform.localPosition = new Vector3(0.5f, 1, 0.6f);
                Set(effect, "handVisual", orb.transform);
                Sparks(orb.transform, Color.cyan, 0.5f, 45); DashArt(active, impact, true);
            });
            Effect<LucarioBoneRushEffect>(Ids[4], (root, cast, active, impact, effect) =>
            {
                var bone = Bone(active.transform); Set(effect, "bone", bone.transform);
                Wave(impact.transform, aura, LucarioMoveRules.BoneCatchRadius); Sparks(impact.transform, Color.cyan, 1, 60);
            });
            Effect<LucarioCloseCombatEffect>(Ids[5], (root, cast, active, impact, effect) =>
            {
                for (int i = 0; i < 7; i++)
                {
                    float x = Mathf.Sin(i * 2.4f) * 0.9f, y = 0.7f + (i % 3) * 0.35f;
                    var streak = Primitive("AuraPunch" + i, active.transform, PrimitiveType.Capsule, aura,
                        new Vector3(x, y, 1.3f + (i % 2) * 1.1f), new Vector3(0.18f, 0.8f, 0.18f));
                    streak.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
                Sparks(active.transform, Color.cyan, 1.2f, 100); Wave(impact.transform, white, 1.5f);
            });
            Effect<LucarioAuraCannonEffect>(Ids[6], (root, cast, active, impact, effect) =>
            {
                var charging = Orb("ChargingAura", cast.transform, 0.8f); charging.transform.localPosition = Vector3.up;
                var sphere = Orb("AuraSphere", active.transform, LucarioMoveRules.AuraRadius); Set(effect, "sphere", sphere.transform);
                Sparks(sphere.transform, Color.cyan, 0.7f, 120); Wave(impact.transform, aura, LucarioMoveRules.AuraRadius);
            });
            foreach (string name in new[] { "Impact", "Spin", "Bone", "Fist", "Steadfast" }) Visual(name);
            Marker(); Indicator();
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        foreach (var id in Ids) Register(LucarioMoveRules.Folder + id + ".prefab");
        ConfigureMoves(); AssetDatabase.SaveAssets();
        Debug.Log("Lucario: seven moves, Steadfast, basic attacks, authored effects and registrations ready. Existing model/animator/stats preserved.");
    }
    private static void Effect<T>(AvailableMoves id, Action<GameObject, GameObject, GameObject, GameObject, T> author) where T : LucarioMoveEffect
    {
        string path = LucarioMoveRules.Folder + id + ".prefab"; if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(id.ToString()); root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var effect = root.AddComponent<T>(); var graphics = Node("Graphics", root.transform);
        var cast = Node("Cast", graphics.transform); var active = Node("Active", graphics.transform); var impact = Node("Impact", graphics.transform);
        Set(effect, "graphics", graphics); Set(effect, "castVisual", cast); Set(effect, "activeVisual", active); Set(effect, "impactVisual", impact);
        Wave(cast.transform, dark, 0.65f); author(root, cast, active, impact, effect);
        var collider = Node("AuthoredHitbox", root.transform).AddComponent<BoxCollider>();
        var definition = (LucarioMove)MoveDatabase.GetMove(id); float length = definition.Range;
        float width = id == AvailableMoves.LucarioQuickAttack ? LucarioMoveRules.QuickWidth : id == AvailableMoves.LucarioExtremeSpeed ? LucarioMoveRules.ExtremeWidth
            : id == AvailableMoves.LucarioPowerUpPunch ? LucarioMoveRules.PunchWidth : id == AvailableMoves.LucarioCloseCombat ? LucarioMoveRules.CombatWidth
            : id == AvailableMoves.LucarioMeteorMash ? LucarioMoveRules.MeteorWidth : 4;
        if (id == AvailableMoves.LucarioPowerUpPunch) length = LucarioMoveRules.PunchMaxRange;
        collider.size = new Vector3(width, 2.5f, length); collider.center = new Vector3(0, 1, length * 0.5f); collider.isTrigger = true; collider.enabled = false;
        active.SetActive(false); impact.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void DashArt(GameObject active, GameObject impact, bool intense)
    {
        for (int i = -1; i <= 1; i++)
            Primitive("AuraWake" + i, active.transform, PrimitiveType.Cube, i == 0 ? white : aura,
                new Vector3(i * 0.65f, 0.7f, -1.2f), new Vector3(0.08f, 0.1f, intense ? 3 : 2));
        Wave(active.transform, aura, 0.9f); Sparks(active.transform, Color.cyan, 0.7f, intense ? 100 : 45); Wave(impact.transform, white, 1);
    }
    private static GameObject Orb(string name, Transform parent, float radius)
    {
        var root = Node(name, parent);
        Primitive("LuminousCore", root.transform, PrimitiveType.Sphere, white, Vector3.zero, Vector3.one * radius * 1.3f);
        for (int i = 0; i < 3; i++)
        {
            var hoop = MeshNode("AuraHoop" + i, root.transform, ring, i == 1 ? white : aura, Vector3.zero, Vector3.one * radius);
            hoop.transform.localRotation = Quaternion.Euler(35 + i * 60, i * 60, 15 + i * 25);
        }
        return root;
    }
    private static GameObject Bone(Transform parent)
    {
        var root = Node("AuraBone", parent);
        Primitive("BoneShaft", root.transform, PrimitiveType.Cylinder, aura, Vector3.zero, new Vector3(0.12f, 0.8f, 0.12f));
        Primitive("BrightShaft", root.transform, PrimitiveType.Cylinder, white, Vector3.zero, new Vector3(0.055f, 0.84f, 0.055f));
        foreach (float end in new[] { -0.8f, 0.8f }) foreach (float side in new[] { -0.13f, 0.13f })
            Primitive("BoneEnd", root.transform, PrimitiveType.Sphere, white, new Vector3(side, end, 0), new Vector3(0.3f, 0.32f, 0.3f));
        return root;
    }
    private static void Visual(string name)
    {
        string path = ResourcesFolder + name + ".prefab"; if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(name); root.AddComponent<TimedCombatVisual>();
        if (name == "Bone" || name == "Fist" || name == "Steadfast") root.AddComponent<LucarioAttachedVisual>();
        if (name == "Bone") Bone(root.transform);
        else if (name == "Fist") { Orb("FistAura", root.transform, 0.28f); Sparks(root.transform, Color.cyan, 0.2f, 25); }
        else if (name == "Steadfast")
        {
            for (int i = 0; i < 3; i++)
            { var hoop = MeshNode("ShieldHoop", root.transform, ring, aura, Vector3.up, Vector3.one * 1.2f); hoop.transform.localRotation = Quaternion.Euler(i * 60, 0, i * 60); }
            Sparks(root.transform, Color.cyan, 0.7f, 25);
        }
        else { Wave(root.transform, name == "Spin" ? aura : white, 1); Sparks(root.transform, Color.cyan, 0.5f, 40); }
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void Marker()
    {
        string path = ResourcesFolder + "ExtremeMarker.prefab"; if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node("ExtremeMarker"); var view = root.AddComponent<LucarioMarkerVisual>();
        var ready = Node("Ready", root.transform); var cooling = Node("Cooling", root.transform);
        Wave(ready.transform, aura, 0.8f); Wave(cooling.transform, dark, 0.65f);
        for (int i = 0; i < 4; i++)
        { float a = i * Mathf.PI * 0.5f; Primitive("MarkerPip", ready.transform, PrimitiveType.Cube, white, new Vector3(Mathf.Sin(a), 0.08f, Mathf.Cos(a)) * 0.8f, new Vector3(0.12f, 0.08f, 0.12f)); }
        Set(view, "ready", ready); Set(view, "cooling", cooling); PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void Indicator()
    {
        string path = ResourcesFolder + "LineIndicator.prefab"; if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        const string sourceFolder = "Assets/Sprites/AimIndicator/pref_Indicator_Arrow_lv3/";
        var root = Node("LineIndicator"); var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourceFolder + "pref_Indicator_Arrow_lv3.fbx"), preview);
        model.transform.SetParent(root.transform, false);
        Bounds bounds = default; bool found = false;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true)) { if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds); }
        if (!found || bounds.size.x <= 0 || bounds.size.z <= 0) throw new InvalidOperationException("Cannot measure supplied Lucario aim arrow.");
        model.transform.localScale = Vector3.Scale(model.transform.localScale, new Vector3(1 / bounds.size.x, -1, 1 / bounds.size.z));
        model.transform.localPosition = new Vector3(-bounds.center.x / bounds.size.x, 0.04f, -bounds.min.z / bounds.size.z);
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var copy = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Trevenant/WoodHammerSector.mat"));
            string texture = r.sharedMaterial != null && r.sharedMaterial.name.Contains("_02_") ? "02" : "01";
            string materialPath = Materials + "AimArrow" + texture + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = copy; material.name = "AimArrow" + texture;
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(sourceFolder + "t_fx_Indicator_Arrow_" + texture + "_cj.png")); material.SetColor("_BaseColor", Color.cyan);
                // The supplied atlas has an opaque black background: additive blending, not ordinary alpha blending.
                material.SetFloat("_Surface", 1); material.SetFloat("_Blend", (float)BaseShaderGUI.BlendMode.Additive);
                material.SetFloat("_BlendModePreserveSpecular", 0); BaseShaderGUI.SetupMaterialBlendMode(material);
                material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)CullMode.Off);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else Object.DestroyImmediate(copy);
            r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void ConfigureMoves()
    {
        string[] icons = { "S1", "U1", "S12", "S11", "U12", "U11", "G1" };
        string[] previews = { "S1", "S2", "S1B", "S1A", "S22", "S21", "U" };
        string[] descriptions = {
            "Dash forward through enemies, dealing physical damage.",
            "After 0.4s punch forward, damaging enemies and shoving them 2 units with a 0.4s stun.",
            "Dash through enemies and heal per enemy hit. The first available Extreme Speed marker hit resets this move; that target's marker recovers after 10s. The next basic within 1s is a circular attack. Upgrade: gain 7.5% Attack for 2s.",
            "Hold up to 4s; fully charges in 1s. Gain 30% damage reduction and 15% movement slow. Release a piercing dash growing from 3 to 6 units, dealing charge-scaled damage plus 12% missing HP (wild bonus capped at 1000). Hits prepare a boosted attack. Direct move hits reduce cooldown by 1s per enemy/tick. Cancel/timeout: 1.4s cooldown. Upgrade: unstoppable while charging.",
            "Three bone strikes use separate close and forward areas; enemies in the overlap take both hits. Reapply Extreme Speed markers. Throw the bone, shoving enemies; recast within 3s to dash to it and damage nearby enemies. Cooldown begins on first use. Upgrade: catching the bone resets Extreme Speed or Power-Up Punch.",
            "Become unstoppable and punch 11 times from 0.4s to 1.2s, healing per enemy hit by each punch. The next basic within 5s becomes a lunging kick, preserving basic charge. Upgrade: more damage.",
            "Launch an accelerating aura sphere that pierces enemies, hitting up to four times close up and once at long range. Reapply Extreme Speed markers, or empower the next Power-Up Punch release by 20% with a 1s shove. Gain 30% speed/CDR and a 20% max HP shield for 6s." };
        var assets = new MoveAsset[Ids.Length];
        var settings = AddressableAssetSettingsDefaultObject.Settings; var previewGroup = settings.FindGroup("MovePreviews");
        for (int i = 0; i < Ids.Length; i++)
        {
            string path = "Assets/Moves/Lucario/" + Ids[i] + ".asset"; var asset = AssetDatabase.LoadAssetAtPath<MoveAsset>(path);
            if (asset == null)
            {
                var definition = MoveDatabase.GetMove(Ids[i]); asset = ScriptableObject.CreateInstance<MoveAsset>();
                asset.move = Ids[i]; asset.moveName = definition.Name; asset.cooldown = definition.Cooldown;
                asset.moveType = i == 6 ? MoveType.UniteMove : i == 0 || i == 2 || i == 3 ? MoveType.MoveA : MoveType.MoveB;
                asset.moveLabel = i == 0 || i == 2 || i == 3 || i == 4 ? MoveLabels.Dash : i == 6 ? MoveLabels.Ranged : MoveLabels.Melee;
                asset.uniteEnergyCost = i == 6 ? LucarioMoveRules.UniteEnergy : 0; asset.description = descriptions[i];
                asset.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MoveIcons/Lucario/t_Skill_Lucario_" + icons[i] + ".png");
                string image = "Assets/Sprites/MoveIcons/Lucario/Previews/t_Growth_Skill_Lucario_" + previews[i] + ".png";
                if (asset.icon == null || AssetDatabase.LoadAssetAtPath<Texture2D>(image) == null) throw new InvalidOperationException("Missing Lucario art: " + image);
                asset.preview = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(image)); AssetDatabase.CreateAsset(asset, path);
            }
            assets[i] = asset; settings.CreateOrMoveEntry(asset.preview.AssetGUID, previewGroup).address = AssetDatabase.GUIDToAssetPath(asset.preview.AssetGUID);
        }
        var pokemon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Lucario.asset"));
        pokemon.FindProperty("passive").enumValueIndex = (int)AvailablePassives.LucarioPassive; var rows = pokemon.FindProperty("learnableMoves");
        if (rows.arraySize == 0)
        {
            int[] levels = { 0, 2, 4, 6, 8, 10, 12 }; int[][] choices = { new[] { 0, 1 }, new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4, 5 }, new[] { 6 }, new[] { 2, 3 }, new[] { 4, 5 } };
            rows.arraySize = levels.Length;
            for (int i = 0; i < levels.Length; i++)
            {
                var row = rows.GetArrayElementAtIndex(i); row.FindPropertyRelative("level").intValue = levels[i]; row.FindPropertyRelative("isUpgraded").boolValue = i >= 5;
                var moves = row.FindPropertyRelative("moves"); moves.arraySize = choices[i].Length;
                for (int j = 0; j < choices[i].Length; j++) moves.GetArrayElementAtIndex(j).objectReferenceValue = assets[choices[i][j]];
            }
        }
        pokemon.ApplyModifiedPropertiesWithoutUndo();
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset")); var entries = registry.FindProperty("moveEntries");
        foreach (var asset in assets)
        {
            if (Enumerable.Range(0, entries.arraySize).Any(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("moveType").longValue == (long)asset.move)) continue;
            var entry = entries.GetArrayElementAtIndex(entries.arraySize++); entry.FindPropertyRelative("moveType").longValue = (long)asset.move; entry.FindPropertyRelative("moveAsset").objectReferenceValue = asset;
        }
        registry.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(previewGroup); EditorUtility.SetDirty(settings);
    }
    private static Material Material(string name, Color color)
    {
        string path = Materials + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
        material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/DragonEnergy.mat")) { name = name }; material.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(material, path); return material;
    }
    private static GameObject Node(string name, Transform parent = null)
    { var node = new GameObject(name) { layer = 8 }; SceneManager.MoveGameObjectToScene(node, preview); if (parent != null) node.transform.SetParent(parent, false); return node; }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    { var node = Node(name, parent); node.transform.localPosition = position; node.transform.localScale = scale; node.AddComponent<MeshFilter>().sharedMesh = mesh; var r = node.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; return node; }
    private static GameObject Primitive(string name, Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale)
    { var node = GameObject.CreatePrimitive(type); node.name = name; node.layer = 8; SceneManager.MoveGameObjectToScene(node, preview); node.transform.SetParent(parent, false); node.transform.localPosition = position; node.transform.localScale = scale; Object.DestroyImmediate(node.GetComponent<Collider>()); node.GetComponent<Renderer>().sharedMaterial = material; return node; }
    private static void Wave(Transform parent, Material material, float radius) => MeshNode("AuraRing", parent, ring, material, Vector3.up * 0.12f, Vector3.one * radius);
    private static void Sparks(Transform parent, Color color, float radius, float rate)
    {
        var node = Node("AuraParticles", parent); var system = node.AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.startColor = color; main.startLifetime = 0.3f; main.startSize = 0.08f; main.startSpeed = 1; main.loop = true; main.playOnAwake = true; main.maxParticles = 160; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = system.emission; emission.rateOverTime = rate; var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius; node.GetComponent<ParticleSystemRenderer>().sharedMaterial = particles;
    }
    private static void Set(Object target, string field, Object value) { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Register(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue == 0) throw new InvalidOperationException("Missing network hash: " + path);
        prefab = PrefabUtility.SavePrefabAsset(prefab); var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("MovesAssets");
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path; EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var data = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")); var list = data.FindProperty("List");
        if (Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab)) return;
        var entry = list.GetArrayElementAtIndex(list.arraySize++); entry.FindPropertyRelative("Override").enumValueIndex = 0; entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null; entry.FindPropertyRelative("SourceHashToOverride").longValue = 0; entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Folder(string path)
    { if (AssetDatabase.IsValidFolder(path)) return; Folder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'), System.IO.Path.GetFileName(path)); }
}
