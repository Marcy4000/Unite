using System;
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

/// <summary>Editor-only, create-missing authoring. Never replaces user models, stats, controllers or hand-edited art.</summary>
public static class EspeonAssetAuthoring
{
    public static readonly AvailableMoves[] Ids = { AvailableMoves.EspeonSwift, AvailableMoves.EspeonGrowl,
        AvailableMoves.EspeonPsyshock, AvailableMoves.EspeonStoredPower, AvailableMoves.EspeonPsybeam,
        AvailableMoves.EspeonFutureSight, AvailableMoves.EspeonPsychicSolare };
    private const string ResourcesFolder = "Assets/Resources/Effects/Espeon/";
    private static Scene preview;
    private static Mesh ring, shard, star;
    private static Material lavender, pink, white, gold, particles;

    [MenuItem("Tools/Characters/Espeon/Create Missing Move Assets")]
    public static void Build()
    {
        foreach (string path in new[] { EspeonMoveRules.Folder + "Art", ResourcesFolder.TrimEnd('/'), "Assets/Materials/Espeon", "Assets/Moves/Espeon" }) Folder(path);
        ring = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Duraludon/Art/ThinRing.asset");
        shard = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Duraludon/Art/SteelShard.asset");
        // The project's authored shard filename is looked up instead of assuming it.
        if (shard == null) shard = AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Prefabs/Objects/Moves/Duraludon/Art" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.IndexOf("Shard", StringComparison.OrdinalIgnoreCase) >= 0).Select(AssetDatabase.LoadAssetAtPath<Mesh>).FirstOrDefault();
        if (ring == null || shard == null) throw new InvalidOperationException("Missing Duraludon reference art.");
        lavender = Material("PsychicLavender", new Color(0.6f, 0.2f, 1));
        pink = Material("PsychicRose", new Color(1, 0.16f, 0.65f));
        white = Material("PsychicCore", new Color(0.9f, 0.85f, 1));
        gold = Material("SwiftGold", new Color(1, 0.77f, 0.2f));
        particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        star = BakeStar();
        preview = EditorSceneManager.NewPreviewScene();
        try
        {
            Effect<EspeonVolleyEffect>(Ids[0], (root, graphics, effect) => Volley(root, graphics, effect, false));
            Effect<EspeonGrowlEffect>(Ids[1], (root, graphics, effect) => Orb(graphics.transform, 0.3f, gold));
            Effect<EspeonVolleyEffect>(Ids[2], (root, graphics, effect) => Volley(root, graphics, effect, true));
            Effect<EspeonStoredPowerEffect>(Ids[3], Stored);
            Effect<EspeonPsybeamEffect>(Ids[4], Beam);
            Effect<EspeonFutureSightEffect>(Ids[5], Future);
            Effect<EspeonPsychicSolareEffect>(Ids[6], Solare);
            Basic(); Stock(); Visual("Beam"); Visual("Burst"); Visual("MagicBounce");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        foreach (var id in Ids) Register(EspeonMoveRules.Folder + id + ".prefab");
        Register(EspeonMoveRules.Folder + "EspeonBasicProjectile.prefab");
        ConfigureMoves();
        foreach (string name in new[] { "BonusPsyshock", "BonusStoredPower" })
        {
            string destination = ResourcesFolder + name + ".png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(destination) == null)
                AssetDatabase.CopyAsset("Assets/Sprites/MoveIcons/Espeon/t_Skill_Espeon_" + (name == "BonusPsyshock" ? "S11_1" : "S12_1") + ".png", destination);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Espeon moves, learnset, editable VFX and network/addressable prefabs authored. Existing models, stats and animator preserved.");
    }
    private static void Effect<T>(AvailableMoves id, Action<GameObject, GameObject, T> author) where T : PrefabMoveEffect
    {
        string path = EspeonMoveRules.Folder + id + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(id.ToString()); root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var graphics = Node("Graphics", root.transform); var effect = root.AddComponent<T>();
        Set(effect, "graphics", graphics); author(root, graphics, effect);
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void Volley(GameObject root, GameObject graphics, EspeonVolleyEffect effect, bool psychic)
    {
        var bolts = new Transform[5];
        for (int i = 0; i < bolts.Length; i++)
        {
            var bolt = Node("Projectile" + i, graphics.transform);
            bolt.transform.localPosition = new Vector3(i - 2, 0.8f, 0);
            if (psychic) Orb(bolt.transform, 0.22f, lavender);
            else MeshNode("Star", bolt.transform, star, gold, Vector3.zero, Vector3.one * 0.3f).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Sparks(bolt.transform, psychic ? new Color(0.7f, 0.3f, 1) : Color.yellow, 0.05f, 16);
            bolts[i] = bolt.transform;
        }
        Array(effect, "projectiles", bolts);
        Set(effect, "projectileHitbox", Sphere(root, "ProjectileHitbox", EspeonMoveRules.ProjectileRadius));
    }
    private static void Stored(GameObject root, GameObject graphics, EspeonStoredPowerEffect effect)
    {
        var bolts = new Transform[EspeonMoveRules.EmpoweredBlasts];
        for (int i = 0; i < bolts.Length; i++)
        {
            var bolt = Node("PsychicBlast" + i, graphics.transform);
            float a = i * Mathf.PI * 2 / bolts.Length;
            bolt.transform.localPosition = new Vector3(Mathf.Sin(a), 1.2f, Mathf.Cos(a));
            Orb(bolt.transform, 0.2f, pink); Sparks(bolt.transform, new Color(1, 0.3f, 0.8f), 0.06f, 14);
            bolts[i] = bolt.transform;
        }
        Array(effect, "projectiles", bolts);
        Set(effect, "acquisitionArea", Sphere(root, "AcquisitionArea", EspeonMoveRules.StoredRange));
    }
    private static void Beam(GameObject root, GameObject graphics, EspeonPsybeamEffect effect)
    {
        var collider = Node("BeamHitbox", root.transform).AddComponent<BoxCollider>();
        collider.size = new Vector3(EspeonMoveRules.PsybeamWidth, 2, EspeonMoveRules.PsybeamRange);
        collider.center = Vector3.forward * EspeonMoveRules.PsybeamRange * 0.5f; collider.isTrigger = true; collider.enabled = false;
        Set(effect, "beamArea", collider);
        // A small muzzle jewel remains inspectable; the extended beam uses the shared visual prefab.
        Orb(graphics.transform, 0.25f, lavender);
    }
    private static void Future(GameObject root, GameObject graphics, EspeonFutureSightEffect effect)
    {
        var mark = Node("FutureSightMark", graphics.transform);
        MeshNode("PredictionBoundary", mark.transform, ring, lavender, Vector3.up * 0.1f, Vector3.one * EspeonMoveRules.FutureRadius);
        MeshNode("CountdownHalo", mark.transform, ring, pink, Vector3.up * 1.8f, Vector3.one * 0.5f);
        for (int i = 0; i < 3; i++) MeshNode("PsychicEye" + i, mark.transform, shard, white,
            new Vector3((i - 1) * 0.2f, 2.1f, 0), Vector3.one * 0.12f);
        Sparks(mark.transform, new Color(0.7f, 0.2f, 1), 1.6f, 24);
        Set(effect, "markVisual", mark.transform);
        Set(effect, "explosionArea", Sphere(root, "FutureExplosionArea", EspeonMoveRules.FutureRadius));
    }
    private static void Solare(GameObject root, GameObject graphics, EspeonPsychicSolareEffect effect)
    {
        var field = Node("SuspensionField", graphics.transform);
        for (int i = 0; i < 3; i++) MeshNode("PsychicBoundary" + i, field.transform, ring, i == 1 ? pink : lavender,
            Vector3.up * (0.12f + i * 0.45f), Vector3.one * (EspeonMoveRules.UniteRadius - i * 0.25f));
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI / 6;
            MeshNode("LevitationShard" + i, field.transform, shard, i % 2 == 0 ? white : pink,
                new Vector3(Mathf.Sin(a) * 5.5f, 0.5f, Mathf.Cos(a) * 5.5f), new Vector3(0.12f, 0.4f, 0.12f));
        }
        Sparks(field.transform, new Color(0.7f, 0.2f, 1), EspeonMoveRules.UniteRadius, 100);
        var burst = Node("SolarExplosion", graphics.transform);
        MeshNode("Shockwave", burst.transform, ring, white, Vector3.up * 0.2f, Vector3.one * EspeonMoveRules.UniteRadius);
        MeshNode("OuterShockwave", burst.transform, ring, pink, Vector3.up * 0.3f, Vector3.one * (EspeonMoveRules.UniteRadius + 0.3f));
        Sparks(burst.transform, new Color(1, 0.4f, 0.8f), EspeonMoveRules.UniteRadius, 180);
        Set(effect, "field", field.transform); Set(effect, "burst", burst);
        Set(effect, "area", Sphere(root, "PsychicSolareArea", EspeonMoveRules.UniteRadius));
    }
    private static void Basic()
    {
        string path = EspeonMoveRules.Folder + "EspeonBasicProjectile.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node("EspeonBasicProjectile"); root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var graphics = Node("Graphics", root.transform); Orb(graphics.transform, 0.14f, white);
        Sparks(graphics.transform, new Color(0.8f, 0.6f, 1), 0.05f, 10);
        Set(root.AddComponent<EspeonBasicProjectile>(), "graphics", graphics);
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void Stock()
    {
        string path = ResourcesFolder + "Stock.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node("EspeonStock"); var view = root.AddComponent<EspeonStockView>();
        var stocks = new Transform[5];
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2 / 5;
            var node = Node("StoredBlast" + i, root.transform); node.transform.localPosition = new Vector3(Mathf.Sin(a) * 0.8f, 1.6f, Mathf.Cos(a) * 0.8f);
            Orb(node.transform, 0.12f, lavender); stocks[i] = node.transform;
        }
        var jewel = Node("BoostReadyJewel", root.transform); jewel.transform.localPosition = Vector3.up * 2;
        Orb(jewel.transform, 0.16f, white);
        var halo = MeshNode("BonusCastHalo", root.transform, ring, pink, Vector3.up * 0.15f, Vector3.one * 1.1f);
        var data = new SerializedObject(view); var list = data.FindProperty("stocks"); list.arraySize = stocks.Length;
        for (int i = 0; i < stocks.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = stocks[i].gameObject;
        data.FindProperty("readyJewel").objectReferenceValue = jewel; data.FindProperty("empoweredRing").objectReferenceValue = halo;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void Visual(string name)
    {
        string path = ResourcesFolder + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(name); root.AddComponent<TimedCombatVisual>();
        if (name == "Beam")
        {
            // Unit-length baked beam points down +Z, so runtime scaling never creates geometry.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "PsychicRay";
            SceneManager.MoveGameObjectToScene(cube, preview); cube.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(cube.GetComponent<Collider>()); cube.transform.localPosition = Vector3.forward * 0.5f;
            cube.transform.localScale = new Vector3(0.45f, 0.45f, 1); cube.GetComponent<MeshRenderer>().sharedMaterial = lavender;
            var core = Object.Instantiate(cube, root.transform); core.name = "WhiteCore"; core.transform.localScale = new Vector3(0.15f, 0.15f, 1);
            core.GetComponent<MeshRenderer>().sharedMaterial = white;
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                var hoop = MeshNode("PsychicRing" + i, root.transform, ring, i == 0 ? white : pink, Vector3.up * i * 0.08f, Vector3.one * (1 - i * 0.15f));
                if (name == "MagicBounce") hoop.transform.localRotation = Quaternion.Euler(i * 60, i * 45, 30);
            }
            Sparks(root.transform, new Color(0.8f, 0.3f, 1), 0.8f, 90);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void ConfigureMoves()
    {
        string[] icons = { "S1", "S2", "S11", "S12", "S21", "S22", "U1" };
        string[] descriptions = {
            "Launch three piercing stars through an aimed convergence point. Repeat hits against one target deal reduced damage.",
            "Damage a designated enemy and slow it by 30% for 2s.",
            "Fire five converging piercing projectiles. Each hit slows by 15% for 3s, up to 75%; all five stun for 1.25s. Upgrade: faster projectiles. A Future Sight bonus cast explodes at convergence.",
            "Generate one blast every 2s and per enemy hit by a boosted beam, up to five. Release stored blasts, prioritizing a Future Sight target, then enemy players over wild Pokemon. Gain 70% speed for 1.2s. Upgrade: bonus damage below 50% HP. Bonus cast: eight blasts, healing 150 per successful hit.",
            "Fire a ray that splits beyond its first target, dealing damage plus 6% of that target's max HP and spreading full-duration slows and stuns. Upgrade: additional max-HP damage when the initial target is stunned.",
            "Mark an enemy for a cleansable explosion after 2s, with missing-HP damage. Basic attacks, Psyshock and Stored Power hits reduce this move's cooldown by 0.8s. An Espeon KO of the marked enemy player grants a separate empowered Move 1 cast for 5s. Upgrade: 15% more damage to marked targets.",
            "Become unstoppable and suspend nearby enemies, including late entrants, then explode and shove them. The field follows Espeon through Eject Button. Gain 30% speed, 30% CDR and a 20% max-HP shield for 6s." };
        var assets = new MoveAsset[Ids.Length];
        for (int i = 0; i < Ids.Length; i++)
        {
            string path = "Assets/Moves/Espeon/" + Ids[i] + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<MoveAsset>(path);
            if (asset == null)
            {
                var definition = MoveDatabase.GetMove(Ids[i]);
                asset = ScriptableObject.CreateInstance<MoveAsset>(); asset.move = Ids[i]; asset.moveName = definition.Name;
                asset.cooldown = definition.Cooldown; asset.moveType = i == 6 ? MoveType.UniteMove : i == 0 || i == 2 || i == 3 ? MoveType.MoveA : MoveType.MoveB;
                asset.moveLabel = i == 6 ? MoveLabels.Area : i == 1 || i == 3 || i == 5 ? MoveLabels.SureHit : MoveLabels.Ranged;
                asset.uniteEnergyCost = i == 6 ? 90000 : 0; asset.description = descriptions[i];
                asset.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MoveIcons/Espeon/t_Skill_Espeon_" + icons[i] + ".png");
                string image = AssetDatabase.FindAssets("t_Growth_Skill_Espeon_" + icons[i] + "_", new[] { "Assets/Sprites/MoveIcons/Espeon/Previews" })
                    .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                if (image == null || asset.icon == null) throw new InvalidOperationException("Missing Espeon move art: " + icons[i]);
                asset.preview = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(image));
                AssetDatabase.CreateAsset(asset, path);
            }
            assets[i] = asset;
        }
        var pokemon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Espeon.asset"));
        pokemon.FindProperty("passive").enumValueIndex = (int)AvailablePassives.EspeonPassive;
        var rows = pokemon.FindProperty("learnableMoves");
        if (rows.arraySize == 0)
        {
            int[] levels = { 0, 1, 3, 5, 7, 9, 11 };
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
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset"));
        var entries = registry.FindProperty("moveEntries");
        foreach (var asset in assets)
        {
            bool exists = Enumerable.Range(0, entries.arraySize).Any(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("moveType").longValue == (long)asset.move);
            if (exists) continue;
            var entry = entries.GetArrayElementAtIndex(entries.arraySize++);
            entry.FindPropertyRelative("moveType").longValue = (long)asset.move;
            entry.FindPropertyRelative("moveAsset").objectReferenceValue = asset;
        }
        registry.ApplyModifiedPropertiesWithoutUndo();
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var previews = settings.FindGroup("MovePreviews");
        foreach (var asset in assets)
            settings.CreateOrMoveEntry(asset.preview.AssetGUID, previews).address = AssetDatabase.GUIDToAssetPath(asset.preview.AssetGUID);
        EditorUtility.SetDirty(previews); EditorUtility.SetDirty(settings);
    }
    private static Mesh BakeStar()
    {
        string path = EspeonMoveRules.Folder + "Art/SwiftStar.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
        var vertices = new Vector3[11]; var triangles = new int[30];
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI / 5; float r = i % 2 == 0 ? 1 : 0.4f;
            vertices[i + 1] = new Vector3(Mathf.Sin(a) * r, Mathf.Cos(a) * r, 0);
            triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % 10 + 1;
        }
        mesh = new Mesh { name = "SwiftStar", vertices = vertices, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static GameObject Node(string name, Transform parent = null)
    {
        var node = new GameObject(name) { layer = 8 }; SceneManager.MoveGameObjectToScene(node, preview);
        if (parent != null) node.transform.SetParent(parent, false); return node;
    }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var node = Node(name, parent); node.transform.localPosition = position; node.transform.localScale = scale;
        node.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = node.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return node;
    }
    private static void Orb(Transform parent, float size, Material material)
    {
        MeshNode("PsychicShard", parent, shard, material, Vector3.zero, new Vector3(size, size * 1.3f, size));
        MeshNode("LuminousHalo", parent, ring, white, Vector3.zero, Vector3.one * size);
    }
    private static SphereCollider Sphere(GameObject root, string name, float radius)
    {
        var collider = Node(name, root.transform).AddComponent<SphereCollider>(); collider.radius = radius; collider.isTrigger = true; collider.enabled = false; return collider;
    }
    private static Material Material(string name, Color color)
    {
        string path = "Assets/Materials/Espeon/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
        material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/DragonEnergy.mat")) { name = name };
        material.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(material, path); return material;
    }
    private static void Sparks(Transform parent, Color color, float radius, float rate)
    {
        var node = Node("PsychicSparks", parent); var system = node.AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.startColor = color; main.startLifetime = 0.35f; main.startSize = 0.08f; main.startSpeed = 0.3f;
        main.loop = true; main.playOnAwake = true; main.maxParticles = 150; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = system.emission; emission.rateOverTime = rate;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
        node.GetComponent<ParticleSystemRenderer>().sharedMaterial = particles;
    }
    private static void Set(Object target, string field, Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Array(Object target, string field, Transform[] values)
    {
        var data = new SerializedObject(target); var list = data.FindProperty(field); list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Register(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue == 0) throw new InvalidOperationException("Missing network spawn hash: " + path);
        prefab = PrefabUtility.SavePrefabAsset(prefab);
        var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("MovesAssets");
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
        EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")); var list = registry.FindProperty("List");
        if (Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab)) return;
        var entry = list.GetArrayElementAtIndex(list.arraySize++); entry.FindPropertyRelative("Override").enumValueIndex = 0;
        entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab; entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null;
        entry.FindPropertyRelative("SourceHashToOverride").longValue = 0; entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;
        registry.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Folder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'), System.IO.Path.GetFileName(path));
    }
}
