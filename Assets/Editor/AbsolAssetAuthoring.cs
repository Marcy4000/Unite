using System;
using System.Collections.Generic;
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

// Editor-only asset authoring, like Duraludon. All gameplay tuning stays in
// AbsolMoveRules; existing prefab layouts, models and animations are preserved.
public static class AbsolAssetAuthoring
{
    private const string Art = AbsolMoveRules.Folder + "Art/";
    private const string Visuals = "Assets/Resources/Effects/Absol/";
    private static Scene preview;
    private static Mesh crescent, edge, rear, ring;
    private static Material violet, blue, white, aim;

    [MenuItem("Tools/Absol/Create Missing Move Assets")]
    public static void Build()
    {
        foreach (var path in new[] { Art.TrimEnd('/'), Visuals.TrimEnd('/'), "Assets/Materials/Absol", "Assets/Resources/AimIndicators", "Assets/Moves/Absol" }) Folder(path);
        violet = Material("DisasterViolet", new Color(0.35f, 0.08f, 0.85f));
        blue = Material("PsychicBlue", new Color(0.05f, 0.45f, 1));
        white = Material("MoonlitCore", new Color(0.8f, 0.87f, 1));
        aim = Material("AimCyan", new Color(0.2f, 0.9f, 1, 0.45f));
        crescent = Arc("DisasterCrescent", 140, 0.23f); edge = Arc("MoonlitEdge", 140, 0.035f); rear = Arc("BackstabArc", 120, 0.15f);
        ring = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Duraludon/Art/ThinRing.asset");
        preview = EditorSceneManager.NewPreviewScene();
        try
        {
            MeleeVisual(false); MeleeVisual(true); MarkVisual(); BackVisual();
            foreach (var id in Ids) Effect(id);
            Indicator("AbsolArc"); Indicator("AbsolSlash"); Indicator("AbsolMidnightSlash");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        foreach (var id in Ids) Register(AbsolMoveRules.Folder + id + ".prefab");
        ConfigureMoves(); AssetDatabase.SaveAssets();
        Debug.Log("Absol assets authored and registered. Existing prefab layouts and model animations were preserved.");
    }

    private static readonly AvailableMoves[] Ids = { AvailableMoves.AbsolFeint, AvailableMoves.AbsolSlash, AvailableMoves.AbsolNightSlash, AvailableMoves.AbsolPursuit, AvailableMoves.AbsolPsychoCut, AvailableMoves.AbsolSuckerPunch, AvailableMoves.AbsolMidnightSlash };

    private static void Effect(AvailableMoves id)
    {
        string path = AbsolMoveRules.Folder + id + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(id.ToString()); root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var graphics = Node("Graphics", root.transform); PrefabMoveEffect effect;
        if (id == AvailableMoves.AbsolFeint || id == AvailableMoves.AbsolNightSlash || id == AvailableMoves.AbsolPursuit)
        {
            var dash = root.AddComponent<AbsolDashEffect>(); effect = dash;
            var serialized = new SerializedObject(dash); serialized.FindProperty("move").longValue = (long)id; serialized.ApplyModifiedPropertiesWithoutUndo();
            var trail = Node("DashTrail", graphics.transform); Slash(trail.transform, Vector3.up * 0.3f, Vector3.one * 1.25f); Sparks(trail.transform, 0.5f); Set(dash, "trail", trail);
        }
        else if (id == AvailableMoves.AbsolSlash)
        {
            effect = root.AddComponent<AbsolSlashEffect>(); MeshNode("ReadySlash", graphics.transform, edge, violet, Vector3.up * 0.15f, Vector3.one * 0.3f);
        }
        else if (id == AvailableMoves.AbsolPsychoCut)
        {
            var cut = root.AddComponent<AbsolPsychoCutEffect>(); effect = cut;
            var blade = Node("PsychicBlade", graphics.transform); Slash(blade.transform, Vector3.zero, new Vector3(0.75f, 1, 0.5f)); Sparks(blade.transform, 0.2f);
            Set(cut, "blade", blade); Set(cut, "markerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Visuals + "PsychoCutMarks.prefab").GetComponent<StackMarkerView>());
        }
        else if (id == AvailableMoves.AbsolSuckerPunch)
        {
            var sucker = root.AddComponent<AbsolSuckerPunchEffect>(); effect = sucker;
            var stance = Node("CounterStance", graphics.transform);
            MeshNode("CounterRing", stance.transform, ring, violet, Vector3.up * 0.12f, Vector3.one);
            MeshNode("CounterEdge", stance.transform, rear, blue, Vector3.up * 0.25f, Vector3.one * 1.2f); Sparks(stance.transform, 0.8f); Set(sucker, "stanceVisual", stance);
        }
        else
        {
            var unite = root.AddComponent<AbsolMidnightSlashEffect>(); effect = unite;
            var flurry = Node("FlurryRectangle", graphics.transform); var ticks = new GameObject[5];
            for (int i = 0; i < ticks.Length; i++)
            {
                ticks[i] = Node("FlurryTick" + (i + 1), flurry.transform);
                for (int j = 0; j < 4; j++)
                {
                    var slash = Slash(ticks[i].transform, new Vector3(j % 2 == 0 ? -0.22f : 0.22f, 0.2f + j * 0.06f, 0.23f + j / 2 * 0.5f), new Vector3(0.4f, 1, 0.3f));
                    slash.localRotation = Quaternion.Euler(0, (i % 2 == 0 ? 1 : -1) * (j % 2 == 0 ? 55 : -55), 0);
                }
            }
            var serialized = new SerializedObject(unite); var list = serialized.FindProperty("flurrySlashes"); list.arraySize = ticks.Length;
            for (int i = 0; i < ticks.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = ticks[i]; serialized.ApplyModifiedPropertiesWithoutUndo();
            var fan = Node("FinalCone", graphics.transform);
            for (int i = 0; i < 3; i++) Slash(fan.transform, Vector3.up * 0.12f, new Vector3(0.28f, 1, 0.9f)).localRotation = Quaternion.Euler(0, (i - 1) * 18, 0);
            Set(unite, "flurry", flurry); Set(unite, "finalFan", fan);
        }
        Set(effect, "graphics", graphics); PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }

    private static void MeleeVisual(bool boosted)
    {
        string name = boosted ? "BoostedSlash" : "MeleeSlash", path = Visuals + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(name); root.AddComponent<TimedCombatVisual>();
        for (int i = 0; i < (boosted ? 3 : 1); i++) Slash(root.transform, new Vector3(0, 0.3f + i * 0.1f, 0), Vector3.one * 0.85f).localRotation = Quaternion.Euler(0, i * 10, i * 15);
        Sparks(root.transform, 0.25f); PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void MarkVisual()
    {
        string path = Visuals + "PsychoCutMarks.prefab"; if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node("PsychoCutMarks"); var view = root.AddComponent<StackMarkerView>(); var serialized = new SerializedObject(view); var list = serialized.FindProperty("markers"); list.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var mark = MeshNode("PsychicMark" + (i + 1), root.transform, crescent, i == 2 ? white : blue, new Vector3((i - 1) * 0.28f, 2.1f, 0), Vector3.one * 0.18f);
            mark.transform.localRotation = Quaternion.Euler(60, 0, 0); list.GetArrayElementAtIndex(i).objectReferenceValue = mark;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void BackVisual()
    {
        string path = Visuals + "BackHighlight.prefab"; if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node("BackHighlight"); var sector = MeshNode("RearSector", root.transform, rear, aim, Vector3.up * 0.08f, Vector3.one * 0.9f);
        sector.transform.localRotation = Quaternion.Euler(0, 180, 0); PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static Transform Slash(Transform parent, Vector3 position, Vector3 scale)
    {
        var slash = MeshNode("DisasterCrescent", parent, crescent, violet, position, scale);
        MeshNode("MoonlitEdge", slash.transform, edge, white, Vector3.up * 0.015f, Vector3.one); return slash.transform;
    }
    private static void Indicator(string name)
    {
        string modelName = name == "AbsolArc" ? "pref_Indicator_Arc_R_2m5_lv3"
            : name == "AbsolSlash" ? "pref_Indicator_Sector_" + AbsolMoveRules.SlashAngle.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "_lv3"
            : "pref_Indicator_Arrow_2dir_lv3";
        ImportedIndicator(name, modelName);
    }

    // Safe to run independently: only migrates the old generated footprint,
    // and leaves imported indicator layouts/material overrides alone thereafter.
    public static void UpdateAimIndicators()
    {
        preview = EditorSceneManager.NewPreviewScene();
        try { Indicator("AbsolArc"); Indicator("AbsolSlash"); Indicator("AbsolMidnightSlash"); }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void ImportedIndicator(string name, string modelName)
    {
        string path = "Assets/Resources/AimIndicators/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            var footprint = existing.transform.Find("Footprint");
            var filter = footprint != null ? footprint.GetComponent<MeshFilter>() : null;
            if (existing.transform.childCount != 1 || filter == null
                || AssetDatabase.GetAssetPath(filter.sharedMesh) != Art + name + "Indicator.asset") return;
        }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sprites/AimIndicator/" + modelName + "/" + modelName + ".fbx");
        if (model == null) throw new InvalidOperationException("Missing aim indicator: " + modelName);
        var root = existing != null ? PrefabUtility.LoadPrefabContents(path) : Node(name);
        try
        {
            if (existing != null) Object.DestroyImmediate(root.transform.Find("Footprint").gameObject);
            var sizing = new GameObject("NormalizedFootprint");
            SceneManager.MoveGameObjectToScene(sizing, root.scene); sizing.transform.SetParent(root.transform, false);
            var art = (GameObject)PrefabUtility.InstantiatePrefab(model, root.scene);
            art.transform.SetParent(sizing.transform, false);
            // Aim.ShowCustomIndicator replaces the ROOT scale. Keep the face
            // correction on the imported child so runtime sizing cannot erase it.
            art.transform.localScale = new Vector3(1, -1, 1);
            var bounds = new Bounds(); bool first = true; float radius = 0;
            foreach (var filter in art.GetComponentsInChildren<MeshFilter>(true))
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = sizing.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    radius = Mathf.Max(radius, new Vector2(point.x, point.z).magnitude);
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
            bool arc = name == "AbsolArc", sector = name == "AbsolSlash";
            float width = arc ? bounds.max.x : bounds.size.x;
            // Sectors must retain their authored angle and pivot: normalize
            // radius uniformly rather than stretching their bounding rectangle.
            sizing.transform.localScale = sector ? new Vector3(1 / radius, 1, 1 / radius) : new Vector3(1 / width, 1, 1 / bounds.size.z);
            sizing.transform.localPosition = sector ? new Vector3(0, 0.06f - bounds.center.y, 0)
                : new Vector3(arc ? 0 : -bounds.center.x / width, 0.06f - bounds.center.y, -bounds.min.z / bounds.size.z);
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;
            foreach (var renderer in art.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                if (sector) renderer.sharedMaterial = SlashSectorMaterial(modelName);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (existing != null) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
        }
    }

    private static Material SlashSectorMaterial(string modelName)
    {
        const string path = "Assets/Materials/Absol/SlashAimSector.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        // The sector FBX's embedded material has no texture assignment. Keep
        // importer settings intact and wire its supplied atlas on our own copy.
        var material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Absol/AimCyan.mat")) { name = "SlashAimSector" };
        material.SetColor("_BaseColor", Color.white);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/AimIndicator/" + modelName + "/t_fx_indicator_Sector_02_sam.png");
        material.SetTexture("_BaseMap", texture); material.SetTexture("_MainTex", texture);
        AssetDatabase.CreateAsset(material, path); return material;
    }

    private static void ConfigureMoves()
    {
        string[] names = { "Feint", "Slash", "Night Slash", "Pursuit", "Psycho Cut", "Sucker Punch", "Midnight Slash" };
        string[] icons = { "S1", "S2", "S11", "S12", "S21", "S22", "G1" }, previews = { "S1", "S2", "S11", "S12", "S21", "S22", "U1" };
        string[] descriptions = {
            "Dash in an alternating arc through enemies, ignoring Defense and shields. Hits increase attack speed by 60% for 0.5s.",
            "Slash enemies in front. This move has 15% additional critical-hit chance.",
            "Dash in an arc. Hitting an enemy unlocks a straight second dash for 6s. Hits increase attack speed briefly; critical-hit chance increases against injured enemies. The full cooldown starts after the second dash or window expiry. At level 11, the second dash heals for 50% of damage dealt, including shield damage.",
            "Dash, empowering the next basic attack for 3s with doubled range and a leap. It replaces any boosted attack and resets attack charge. Backstabs deal extra damage and reduce remaining cooldown by 70%. At level 11, gain 35% movement speed for up to 2s before attacking.",
            "Throw a piercing psychic slash that strongly slows enemies with a decaying slow and applies three cleansable marks for 5s. Direct damage consumes marks; the third deals increased damage. Slash and marks have 15% extra critical-hit chance. At level 13, gain 20% movement speed while an enemy remains marked, regardless of distance.",
            "Enter an unstoppable counter stance for 1s, slowing yourself and the target by 50%, then charge behind it. Being attacked or the target attacking triggers an immediate stronger counter, dodges the first hit and shoves enemies. At level 13, gain 40% attack speed for 3s after hitting.",
            "Become unstoppable, then untargetable and invulnerable while five slashes hit the rectangle in front. Flurry damage is divided among enemies. Finish with a cone of energy slashes, hitting up to four times at close range; extra final hits deal 30% damage. Remain action-locked throughout. Gain 80% movement speed and a 20% max-HP shield for 6s afterward." };
        var assets = new MoveAsset[Ids.Length];
        for (int i = 0; i < Ids.Length; i++)
        {
            string path = "Assets/Moves/Absol/" + Ids[i] + ".asset"; var asset = AssetDatabase.LoadAssetAtPath<MoveAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MoveAsset>(); var definition = MoveDatabase.GetMove(Ids[i]); asset.move = Ids[i]; asset.moveName = names[i]; asset.cooldown = definition.Cooldown;
                asset.moveType = i == 6 ? MoveType.UniteMove : i == 1 || i == 4 || i == 5 ? MoveType.MoveB : MoveType.MoveA;
                asset.moveLabel = i == 5 ? MoveLabels.SureHit : i == 6 ? MoveLabels.Area : i == 4 ? MoveLabels.Ranged : i == 1 ? MoveLabels.Melee : MoveLabels.Dash;
                asset.uniteEnergyCost = i == 6 ? AbsolMoveRules.UniteEnergy : 0; asset.description = descriptions[i];
                asset.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MoveIcons/Absol/t_Skill_Absol_" + icons[i] + ".png");
                asset.preview = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID("Assets/Sprites/MoveIcons/Absol/Preview/t_Growth_Skill_Absol_" + previews[i] + ".png"));
                AssetDatabase.CreateAsset(asset, path);
            }
            assets[i] = asset;
        }
        var pokemon = new SerializedObject(AssetDatabase.LoadAssetAtPath<PokemonBase>("Assets/Pokemons/PlayableMons/Absol.asset"));
        pokemon.FindProperty("passive").enumValueIndex = (int)AvailablePassives.AbsolPassive;
        int[] levels = { 0, 2, 4, 6, 8, 10, 12 }; int[][] choices = { new[] { 0, 1 }, new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4, 5 }, new[] { 6 }, new[] { 2, 3 }, new[] { 4, 5 } };
        var rows = pokemon.FindProperty("learnableMoves");
        if (rows.arraySize == 0)
        {
            rows.arraySize = levels.Length;
            for (int i = 0; i < levels.Length; i++) { var row = rows.GetArrayElementAtIndex(i); row.FindPropertyRelative("level").intValue = levels[i]; row.FindPropertyRelative("isUpgraded").boolValue = i >= 5; var moves = row.FindPropertyRelative("moves"); moves.arraySize = choices[i].Length; for (int j = 0; j < choices[i].Length; j++) moves.GetArrayElementAtIndex(j).objectReferenceValue = assets[choices[i][j]]; }
        }
        pokemon.ApplyModifiedPropertiesWithoutUndo();
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset")); var entries = registry.FindProperty("moveEntries");
        foreach (var asset in assets) { bool exists = false; for (int i = 0; i < entries.arraySize; i++) exists |= entries.GetArrayElementAtIndex(i).FindPropertyRelative("moveType").longValue == (long)asset.move; if (exists) continue; var entry = entries.GetArrayElementAtIndex(entries.arraySize++); entry.FindPropertyRelative("moveType").longValue = (long)asset.move; entry.FindPropertyRelative("moveAsset").objectReferenceValue = asset; }
        registry.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Mesh Arc(string name, float degrees, float width)
    {
        string path = Art + name + ".asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var vertices = new Vector3[98]; var triangles = new List<int>();
        for (int i = 0; i <= 48; i++) { float t = i / 48f, a = Mathf.Lerp(-degrees / 2, degrees / 2, t) * Mathf.Deg2Rad, inner = 1 - width * Mathf.Sin(t * Mathf.PI); vertices[i * 2] = new Vector3(Mathf.Sin(a) * inner, 0, Mathf.Cos(a) * inner); vertices[i * 2 + 1] = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); if (i < 48) { int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); } }
        return SaveMesh(path, vertices, triangles.ToArray());
    }
    private static Mesh SaveMesh(string path, Vector3[] vertices, int[] triangles)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); bool exists = mesh != null;
        if (!exists) mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path) }; else mesh.Clear();
        mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (exists) EditorUtility.SetDirty(mesh); else AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
    private static Material Material(string name, Color color)
    {
        string path = "Assets/Materials/Absol/" + name + ".mat"; var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/DragonEnergy.mat")) { name = name }; material.SetColor("_BaseColor", color);
        if (color.a < 1) { material.SetFloat("_Surface", 1); material.SetFloat("_SrcBlend", 5); material.SetFloat("_DstBlend", 10); material.SetFloat("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000; }
        AssetDatabase.CreateAsset(material, path); return material;
    }
    private static GameObject Node(string name, Transform parent = null) { var root = new GameObject(name) { layer = 8 }; SceneManager.MoveGameObjectToScene(root, preview); if (parent != null) root.transform.SetParent(parent, false); return root; }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale) { var root = Node(name, parent); root.transform.localPosition = position; root.transform.localScale = scale; root.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return root; }
    private static void Sparks(Transform parent, float radius)
    {
        var root = Node("PsychicSparks", parent); var particles = root.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.startColor = new Color(0.4f, 0.4f, 1); main.startLifetime = 0.3f; main.startSpeed = 0.3f; main.startSize = 0.06f; main.maxParticles = 64; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = particles.emission; emission.rateOverTime = 18; var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
        root.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
    }
    private static void Set(Object target, string field, Object value) { var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Folder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path)); }
    private static void Register(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue == 0) throw new InvalidOperationException("Missing spawn hash: " + path);
        prefab = PrefabUtility.SavePrefabAsset(prefab); var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("MovesAssets"); settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path; EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")); var list = registry.FindProperty("List");
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) return;
        var entry = list.GetArrayElementAtIndex(list.arraySize++); entry.FindPropertyRelative("Override").enumValueIndex = 0; entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null; entry.FindPropertyRelative("SourceHashToOverride").longValue = 0; entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null; registry.ApplyModifiedPropertiesWithoutUndo();
    }
}
