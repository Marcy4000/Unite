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
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Create-missing, editor-only authoring. Never rewrites model/controller/material work.</summary>
public static class NinetalesAssetAuthoring
{
    private const string Art = NinetalesMoveRules.Folder + "Art/";
    private const string Visuals = "Assets/Resources/Effects/AlolanNinetales/";
    private const string Materials = "Assets/Materials/AlolanNinetales/";
    private static Scene preview;
    private static Mesh shard, ring, sector, disc, curtain;
    private static Material ice, white, pink, mist, aurora;
    private static readonly AvailableMoves[] Ids = { AvailableMoves.NinetalesPowderSnow, AvailableMoves.NinetalesIcyWind,
        AvailableMoves.NinetalesAvalanche, AvailableMoves.NinetalesDazzlingGleam, AvailableMoves.NinetalesBlizzard,
        AvailableMoves.NinetalesAuroraVeil, AvailableMoves.NinetalesSnowGlobe };

    [MenuItem("Tools/Alolan Ninetales/Create Missing Move Assets")]
    public static void Build()
    {
        foreach (string path in new[] { Art, Visuals, Materials, "Assets/Moves/AlolanNinetales/", "Assets/Resources/AimIndicators/" }) Folder(path.TrimEnd('/'));
        ice = Material("GlacialBlue", new Color(0.25f, 0.78f, 1)); white = Material("SnowCore", new Color(0.85f, 0.98f, 1));
        pink = Material("FairyLight", new Color(1, 0.65f, 0.92f)); mist = Material("FrozenMist", new Color(0.4f, 0.8f, 1, 0.18f));
        aurora = Material("AuroraCurtain", new Color(0.35f, 1, 0.8f, 0.18f));
        shard = Shard(); ring = Ring(); sector = Fan("SnowCone", NinetalesMoveRules.ConeAngle); disc = Fan("FrostDisc", 360); curtain = Curtain();
        preview = EditorSceneManager.NewPreviewScene();
        try
        {
            Decoration("Snowfall"); Decoration("Freeze"); Decoration("IceImpact"); Marks();
            PassiveWarning();
            foreach (var id in Ids) Effect(id);
            BasicProjectile();
            Indicator("NinetalesCone", "pref_Indicator_Sector_60_lv3", true);
            Indicator("NinetalesWind", "pref_Indicator_Arrow_lv3", false);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        foreach (var id in Ids) Register(NinetalesMoveRules.Folder + id + ".prefab");
        Register(NinetalesMoveRules.Folder + "NinetalesBasicProjectile.prefab");
        ConfigureMoves(); AssetDatabase.SaveAssets();
        Debug.Log("Alolan Ninetales moves authored and registered. Existing layouts, models, animations and stats preserved.");
    }

    private static void Effect(AvailableMoves id)
    {
        string path = NinetalesMoveRules.Folder + id + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(id.ToString()); root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var graphics = Node("Graphics", root.transform); PrefabMoveEffect effect;
        if (id == AvailableMoves.NinetalesPowderSnow || id == AvailableMoves.NinetalesDazzlingGleam)
        {
            var cone = root.AddComponent<NinetalesConeEffect>(); effect = cone;
            bool gleam = id == AvailableMoves.NinetalesDazzlingGleam;
            var fan = Node(gleam ? "FairyFlash" : "PowderGust", graphics.transform);
            MeshNode("ConeFootprint", fan.transform, sector, gleam ? pink : mist, Vector3.up * 0.12f, Vector3.one);
            for (int i = 0; i < 9; i++)
            {
                float angle = (i % 3 - 1) * 20 * Mathf.Deg2Rad, distance = 0.3f + i / 3 * 0.3f;
                MeshNode("SnowSpark" + i, fan.transform, shard, gleam ? white : ice,
                    new Vector3(Mathf.Sin(angle) * distance, 0.15f, Mathf.Cos(angle) * distance), new Vector3(0.018f, 0.1f, 0.018f));
            }
            Set(cone, "cone", fan); Flag(cone, "dazzlingGleam", gleam);
        }
        else if (id == AvailableMoves.NinetalesAvalanche)
        {
            var avalanche = root.AddComponent<NinetalesAvalancheEffect>(); effect = avalanche;
            var wall = Node("IceWall", graphics.transform); wall.layer = LayerMask.NameToLayer("Wall"); wall.tag = "Wall";
            var blocker = wall.AddComponent<BoxCollider>(); blocker.size = new Vector3(NinetalesMoveRules.WallWidth, NinetalesMoveRules.WallHeight, NinetalesMoveRules.WallThickness); blocker.center = Vector3.up * NinetalesMoveRules.WallHeight * 0.5f;
            blocker.includeLayers = LayerMask.GetMask("Players", "WildPokemons"); blocker.layerOverridePriority = 1;
            var obstacle = wall.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = blocker.size; obstacle.center = blocker.center; obstacle.carving = true; obstacle.carveOnlyStationary = false;
            for (int i = 0; i < 7; i++)
                MeshNode("IcePillar" + i, wall.transform, shard, i % 2 == 0 ? ice : white,
                    new Vector3((i - 3) * 0.55f, 1, 0), new Vector3(0.45f, 1.2f - Mathf.Abs(i - 3) * 0.1f, 0.3f));
            var gust = MeshNode("InitialGust", graphics.transform, disc, mist, Vector3.up * 0.15f, Vector3.one);
            var collapse = IceBurst("WallCollapse", graphics.transform);
            Set(avalanche, "wall", wall); Set(avalanche, "blockingCollider", blocker); Set(avalanche, "gust", gust); Set(avalanche, "collapse", collapse);
        }
        else if (id == AvailableMoves.NinetalesIcyWind || id == AvailableMoves.NinetalesBlizzard)
        {
            var wind = root.AddComponent<NinetalesWindEffect>(); effect = wind;
            Flag(wind, "blizzard", id == AvailableMoves.NinetalesBlizzard);
            var waves = new GameObject[2];
            for (int i = 0; i < 2; i++)
            {
                waves[i] = Node("WindBlast" + (i + 1), graphics.transform);
                for (int j = 0; j < 7; j++)
                    MeshNode("WindCrystal" + j, waves[i].transform, shard, j % 2 == 0 ? white : ice,
                        new Vector3((j - 3) * 0.13f, Mathf.Sin(j) * 0.15f, j % 2 * 0.2f), new Vector3(0.04f, 0.28f, 0.14f)).transform.localRotation = Quaternion.Euler(60, 0, 15);
                Snow(waves[i].transform, 0.4f, 30);
            }
            var frost = Node("FrostField", graphics.transform);
            MeshNode("FrostBoundary", frost.transform, ring, ice, Vector3.up * 0.1f, Vector3.one);
            MeshNode("FrozenGround", frost.transform, disc, mist, Vector3.up * 0.08f, Vector3.one);
            for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6; MeshNode("FrostShard" + i, frost.transform, shard, white, new Vector3(Mathf.Sin(a) * 0.8f, 0.1f, Mathf.Cos(a) * 0.8f), new Vector3(0.05f, 0.25f, 0.05f)); }
            Snow(frost.transform, 0.8f, 25);
            SetArray(wind, "blasts", waves); Set(wind, "frost", frost);
        }
        else if (id == AvailableMoves.NinetalesAuroraVeil)
        {
            var veil = root.AddComponent<NinetalesVeilEffect>(); effect = veil;
            var field = Node("Aurora", graphics.transform);
            MeshNode("AuroraBoundary", field.transform, ring, white, Vector3.up * 0.08f, Vector3.one);
            MeshNode("AuroraFloor", field.transform, disc, aurora, Vector3.up * 0.06f, Vector3.one);
            MeshNode("AuroraCurtain", field.transform, curtain, aurora, Vector3.zero, new Vector3(1, 0.45f, 1));
            MeshNode("InnerRainbow", field.transform, curtain, mist, Vector3.zero, new Vector3(0.97f, 0.38f, 0.97f));
            Snow(field.transform, 0.85f, 18); Set(veil, "aurora", field);
        }
        else
        {
            var globe = root.AddComponent<NinetalesSnowGlobeEffect>(); effect = globe;
            var gathering = Node("GatheringSnow", graphics.transform);
            MeshNode("SnowGlobeBoundary", gathering.transform, ring, ice, Vector3.up * 0.1f, Vector3.one);
            Snow(gathering.transform, 0.8f, 60);
            var explosion = IceBurst("SnowGlobeExplosion", graphics.transform);
            Set(globe, "gathering", gathering); Set(globe, "explosion", explosion);
        }
        Set(effect, "graphics", graphics); Save(root, path);
    }

    private static void BasicProjectile()
    {
        string path = NinetalesMoveRules.Folder + "NinetalesBasicProjectile.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node("NinetalesBasicProjectile"); root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var effect = root.AddComponent<NinetalesBasicProjectile>(); var graphics = Node("Graphics", root.transform);
        var normal = MeshNode("IcePellet", graphics.transform, shard, ice, Vector3.zero, Vector3.one * 0.18f);
        var boosted = IceBurst("BoostedIce", graphics.transform); boosted.transform.localScale = Vector3.one * 0.25f;
        Set(effect, "graphics", graphics); Set(effect, "normal", normal); Set(effect, "boosted", boosted); Save(root, path);
    }
    private static GameObject IceBurst(string name, Transform parent)
    {
        var root = Node(name, parent); MeshNode("ShockRing", root.transform, ring, white, Vector3.up * 0.15f, Vector3.one);
        for (int i = 0; i < 8; i++) { float angle = i * Mathf.PI / 4; var crystal = MeshNode("IceShard" + i, root.transform, shard, i % 2 == 0 ? ice : white, new Vector3(Mathf.Sin(angle) * 0.55f, 0.25f, Mathf.Cos(angle) * 0.55f), new Vector3(0.12f, 0.5f, 0.12f)); crystal.transform.localRotation = Quaternion.Euler(Mathf.Cos(angle) * 25, 0, -Mathf.Sin(angle) * 25); }
        Snow(root.transform, 0.5f, 30); return root;
    }
    private static void Decoration(string name)
    {
        string path = Visuals + name + ".prefab"; if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = IceBurst(name, null); root.AddComponent<TimedCombatVisual>();
        if (name == "Snowfall") { root.transform.Find("ShockRing").gameObject.SetActive(false); foreach (var filter in root.GetComponentsInChildren<MeshFilter>()) filter.transform.localPosition += Vector3.up * 1.5f; }
        Save(root, path);
    }
    private static void Marks()
    {
        string path = Visuals + "SnowStacks.prefab"; if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node("SnowStacks"); var view = root.AddComponent<StackMarkerView>(); var markers = new GameObject[3];
        for (int i = 0; i < markers.Length; i++) markers[i] = MeshNode("SnowStack" + (i + 1), root.transform, shard, ice, new Vector3((i - 1) * 0.25f, 2.1f, 0), new Vector3(0.09f, 0.16f, 0.09f));
        SetArray(view, "markers", markers); Save(root, path);
    }
    private static void PassiveWarning()
    {
        string path = Visuals + "SnowWarning.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node("SnowWarning"); var view = root.AddComponent<NinetalesPassiveWarningView>();
        MeshNode("WarningBoundary", root.transform, ring, ice, Vector3.up * 0.06f, Vector3.one);
        var fill = MeshNode("WarningBuildup", root.transform, disc, mist, Vector3.up * 0.05f, Vector3.one);
        Set(view, "buildup", fill.transform); Save(root, path);
    }
    private static void Indicator(string name, string modelName, bool cone)
    {
        string path = "Assets/Resources/AimIndicators/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        string folder = "Assets/Sprites/AimIndicator/" + modelName + "/";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + modelName + ".fbx");
        if (model == null) throw new InvalidOperationException("Missing indicator " + modelName);
        var root = Node(name); var sizing = Node("NormalizedFootprint", root.transform);
        var art = (GameObject)PrefabUtility.InstantiatePrefab(model, preview); art.transform.SetParent(sizing.transform, false); art.transform.localScale = new Vector3(1, -1, 1);
        var bounds = new Bounds(); bool first = true; float radius = 0;
        foreach (var filter in art.GetComponentsInChildren<MeshFilter>(true)) foreach (var vertex in filter.sharedMesh.vertices)
        { var point = sizing.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)); radius = Mathf.Max(radius, new Vector2(point.x, point.z).magnitude); if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point); }
        sizing.transform.localScale = cone ? new Vector3(1 / radius, 1, 1 / radius) : new Vector3(1 / bounds.size.x, 1, 1 / bounds.size.z);
        sizing.transform.localPosition = cone ? new Vector3(0, 0.06f - bounds.center.y, 0) : new Vector3(-bounds.center.x / bounds.size.x, 0.06f - bounds.center.y, -bounds.min.z / bounds.size.z);
        string materialPath = Materials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(mist) { name = name }; material.SetColor("_BaseColor", Color.white);
            var texturePath = AssetDatabase.FindAssets("t:Texture2D", new[] { folder.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            if (texturePath != null) { var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath); material.SetTexture("_BaseMap", texture); material.SetTexture("_MainTex", texture); }
            AssetDatabase.CreateAsset(material, materialPath);
        }
        foreach (var renderer in art.GetComponentsInChildren<Renderer>(true)) { renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.gameObject.layer = 8; }
        Save(root, path);
    }

    private static void ConfigureMoves()
    {
        string[] names = { "Powder Snow", "Icy Wind", "Avalanche", "Dazzling Gleam", "Blizzard", "Aurora Veil", "Snow Globe" };
        string[] suffix = { "S1", "S2", "S11", "S12", "S21", "S22", "G1" }, previews = { "S1_", "S2_", "S1A_", "S1B_", "S21_", "S22_", "U_" };
        string[] descriptions = {
            "A cone of powder snow damages enemies and slows them by 50% for 1s. Applies one snow stack.",
            "Two gusts of freezing air damage enemies, slow them by 20% for 1s and shove them a short distance. Each hit applies snow.",
            "Send a gust of ice toward the selected area, dealing damage and creating a wall that blocks both teams. The wall collapses after 2s, damaging nearby enemies. Each hit applies snow. At level 11, hits also slow by 50% for 1s.",
            "A cone of blinding light damages enemies, stuns them for 0.8s and applies snow. At level 11, damage increases and the stun lasts 1s.",
            "Blast twice in a line. The first blast passes through walls; the second deals double damage and shoves. Hitting a wall with the second blast creates a 4s frost field, damaging enemies every 0.5s. Hits slow by 35% for 1s and apply snow. At level 13, damage increases.",
            "Dash and create a stationary aurora for 5s. Ninetales and allies inside receive 35% less damage. Inside its own veil, Ninetales gains 10% movement speed and all attacks are boosted. Independently gain 15% attack speed and healing once per boosted attack that hits for 5s. At level 13, damage reduction increases to 40% and cooldown falls to 10s.",
            "Become unstoppable and unleash a blast around Ninetales. Already-frozen enemies take increased damage. Damage is dealt before applying snow. Gain 30% movement speed, 30% cooldown reduction and a 20% max-HP shield for 6s." };
        var assets = new MoveAsset[Ids.Length];
        for (int i = 0; i < Ids.Length; i++)
        {
            string path = "Assets/Moves/AlolanNinetales/" + Ids[i] + ".asset"; var asset = AssetDatabase.LoadAssetAtPath<MoveAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MoveAsset>(); asset.move = Ids[i]; asset.moveName = names[i]; asset.cooldown = MoveDatabase.GetMove(Ids[i]).Cooldown;
                asset.moveType = i == 6 ? MoveType.UniteMove : i == 1 || i == 4 || i == 5 ? MoveType.MoveB : MoveType.MoveA;
                asset.moveLabel = i == 5 ? MoveLabels.Buff : i == 3 ? MoveLabels.Hinderance : i == 1 || i == 4 ? MoveLabels.Ranged : MoveLabels.Area;
                asset.uniteEnergyCost = i == 6 ? NinetalesMoveRules.UniteEnergy : 0; asset.description = descriptions[i];
                asset.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MoveIcons/AlolanNinetales/t_Skill_Vulpix_" + suffix[i] + ".png");
                string image = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites/MoveIcons/AlolanNinetales/Previews" }).Select(AssetDatabase.GUIDToAssetPath).First(p => System.IO.Path.GetFileName(p).StartsWith("t_Growth_Skill_Vulpix_" + previews[i]));
                asset.preview = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(image)); AssetDatabase.CreateAsset(asset, path);
            }
            assets[i] = asset;
        }
        var pokemon = new SerializedObject(AssetDatabase.LoadAssetAtPath<PokemonBase>("Assets/Pokemons/PlayableMons/AlolanNinetales.asset"));
        pokemon.FindProperty("passive").enumValueIndex = (int)AvailablePassives.NinetalesPassive;
        int[] levels = { 0, 2, 3, 5, 8, 10, 12 }; int[][] choices = { new[] { 0, 1 }, new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4, 5 }, new[] { 6 }, new[] { 2, 3 }, new[] { 4, 5 } };
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

    private static Mesh Shard() => Mesh("IceCrystal", new[] { Vector3.up, Vector3.down * 0.8f, Vector3.left, Vector3.forward, Vector3.right, Vector3.back }, new[] { 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 2, 1, 3, 2, 1, 4, 3, 1, 5, 4, 1, 2, 5 });
    private static Mesh Fan(string name, float degrees)
    {
        var vertices = new List<Vector3> { Vector3.zero }; var triangles = new List<int>();
        for (int i = 0; i <= 64; i++) { float a = Mathf.Lerp(-degrees * 0.5f, degrees * 0.5f, i / 64f) * Mathf.Deg2Rad; vertices.Add(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a))); if (i > 0) triangles.AddRange(new[] { 0, i, i + 1 }); }
        return Mesh(name, vertices.ToArray(), triangles.ToArray());
    }
    private static Mesh Ring()
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= 64; i++) { float a = i * Mathf.PI / 32; vertices.Add(new Vector3(Mathf.Sin(a) * 0.985f, 0, Mathf.Cos(a) * 0.985f)); vertices.Add(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a))); if (i < 64) { int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); } }
        return Mesh("IceRing", vertices.ToArray(), triangles.ToArray());
    }
    private static Mesh Curtain()
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= 64; i++) { float a = i * Mathf.PI / 32; var point = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); vertices.Add(point); vertices.Add(point + Vector3.up * (1 + Mathf.Sin(a * 3) * 0.15f)); if (i < 64) { int n = i * 2; triangles.AddRange(new[] { n, n + 1, n + 2, n + 1, n + 3, n + 2 }); } }
        return Mesh("AuroraCurtain", vertices.ToArray(), triangles.ToArray());
    }
    private static Mesh Mesh(string name, Vector3[] vertices, int[] triangles)
    {
        string path = Art + name + ".asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
        mesh = new Mesh { name = name, vertices = vertices, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static Material Material(string name, Color color)
    {
        string path = Materials + name + ".mat"; var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/DragonEnergy.mat")) { name = name };
        material.SetColor("_BaseColor", color); material.SetFloat("_Cull", 0);
        if (color.a < 1) { material.SetFloat("_Surface", 1); material.SetFloat("_SrcBlend", 5); material.SetFloat("_DstBlend", 10); material.SetFloat("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000; }
        AssetDatabase.CreateAsset(material, path); return material;
    }
    private static void Snow(Transform parent, float radius, float rate)
    {
        var root = Node("SnowParticles", parent); var particles = root.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.startColor = new Color(0.7f, 0.95f, 1); main.startLifetime = 0.5f; main.startSpeed = 0.5f; main.startSize = 0.07f; main.maxParticles = 128; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = particles.emission; emission.rateOverTime = rate; var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
        root.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
    }
    private static GameObject Node(string name, Transform parent = null) { var root = new GameObject(name) { layer = 8 }; SceneManager.MoveGameObjectToScene(root, preview); if (parent != null) root.transform.SetParent(parent, false); return root; }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale) { var root = Node(name, parent); root.transform.localPosition = position; root.transform.localScale = scale; root.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return root; }
    private static void Save(GameObject root, string path) { PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); }
    private static void Set(Object target, string field, Object value) { var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Flag(Object target, string field, bool value) { var serialized = new SerializedObject(target); serialized.FindProperty(field).boolValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetArray(Object target, string field, GameObject[] values) { var serialized = new SerializedObject(target); var list = serialized.FindProperty(field); list.arraySize = values.Length; for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; serialized.ApplyModifiedPropertiesWithoutUndo(); }
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
