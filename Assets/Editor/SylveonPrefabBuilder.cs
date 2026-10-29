using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Like Duraludon: editor-authored art/hitboxes, preserving existing prefab edits.</summary>
public static class SylveonPrefabBuilder
{
    private const string Folder = SylvMoveRules.Folder;
    private const string Materials = "Assets/Materials/Sylveon/";
    private static Scene preview;
    private static Mesh heart, flame, ring;
    private static Material pink, pale, blue, violet, particles;

    [MenuItem("Tools/Sylveon/Create Missing Move Prefabs")]
    public static void Build()
    {
        EnsureFolder(Folder + "Art"); EnsureFolder(Materials.TrimEnd('/'));
        heart = BakeHeart(); flame = BakeFlame();
        ring = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Duraludon/Art/ThinRing.asset");
        pink = Material("FairyRose", new Color(1, 0.24f, 0.55f));
        pale = Material("PearlLight", new Color(1, 0.87f, 0.94f));
        blue = Material("RibbonBlue", new Color(0.3f, 0.78f, 1));
        violet = Material("MysticalViolet", new Color(0.64f, 0.2f, 1));
        particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        preview = EditorSceneManager.NewPreviewScene();
        try
        {
            Create<SylvMysticalFireEffect>("SylvMysticalFire", Fire);
            Create<SylvDrainingKissEffect>("SylvDrainingKiss", Kiss);
            Create<HomingMoveEffect>("SylvBabyDollEyes", Eyes);
            Create<SylvCalmMindEffect>("SylvCalmMind", Calm);
            Create<SylvFairyFrolicEffect>("SylvFairyFrolic", Frolic);
            BasicAttack(false); BasicAttack(true);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        AddMissingLandingWarning();
        foreach (string name in new[] { "SylvMysticalFire", "SylvDrainingKiss", "SylvBabyDollEyes", "SylvCalmMind", "SylvFairyFrolic", "SylvBasicAttack", "SylvBoostedAttack" })
            Register(Folder + name + ".prefab");
        ConfigureMoves(); ConfigureAnimations();
        AssetDatabase.SaveAssets();
        Debug.Log("Sylveon moves and authored prefabs registered. Hyper Voice and existing prefab layouts were preserved.");
    }

    private static void Create<T>(string name, Action<GameObject, GameObject, T> author) where T : PrefabMoveEffect
    {
        string path = Folder + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(name);
        root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var graphics = Node("Graphics", root.transform);
        T effect = root.AddComponent<T>();
        Set(effect, "graphics", graphics);
        author(root, graphics, effect);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void Fire(GameObject root, GameObject graphics, SylvMysticalFireEffect effect)
    {
        var flames = new Transform[5];
        for (int i = 0; i < flames.Length; i++)
        {
            var node = Node("Flame" + (i + 1), graphics.transform);
            float angle = i * Mathf.PI * 2 / flames.Length;
            node.transform.localPosition = new Vector3(Mathf.Sin(angle), 1.1f, Mathf.Cos(angle));
            MeshNode("VioletFlame", node.transform, flame, violet, Vector3.zero, new Vector3(0.28f, 0.5f, 0.28f));
            MeshNode("RoseCore", node.transform, flame, pink, Vector3.up * 0.04f, new Vector3(0.16f, 0.34f, 0.16f));
            Sparks(node.transform, new Color(0.8f, 0.3f, 1), 0.08f, 18);
            flames[i] = node.transform;
        }
        var serialized = new SerializedObject(effect);
        var list = serialized.FindProperty("flames"); list.arraySize = flames.Length;
        for (int i = 0; i < flames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = flames[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BasicAttack(bool boosted)
    {
        string name = boosted ? "SylvBoostedAttack" : "SylvBasicAttack";
        string path = Folder + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(name); root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var projectile = root.AddComponent<HomingProjectile>(); projectile.speed = 20;
        var hitbox = root.AddComponent<SphereCollider>(); hitbox.isTrigger = true; hitbox.radius = 0.2f;
        var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
        var graphics = Node("Graphics", root.transform);
        MeshNode("FairyBolt", graphics.transform, heart, boosted ? pink : pale, Vector3.zero, Vector3.one * (boosted ? 0.3f : 0.17f));
        Sparks(graphics.transform, new Color(1, 0.65f, 0.85f), 0.08f, boosted ? 20 : 8);
        if (boosted)
        {
            var serialized = new SerializedObject(projectile);
            var stats = serialized.FindProperty("attackerOnHitStats"); stats.arraySize = 1;
            var stat = stats.GetArrayElementAtIndex(0); WriteStat(stat, 75, Stat.Speed, SylvMoveRules.BoostedSpeed);
            stat.FindPropertyRelative("IsBuff").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }

    private static void Kiss(GameObject root, GameObject graphics, SylvDrainingKissEffect effect)
    {
        Heart(graphics.transform, 0.35f);
        Sparks(graphics.transform, new Color(1, 0.35f, 0.65f), 0.12f, 20);
    }

    private static void Eyes(GameObject root, GameObject graphics, HomingMoveEffect effect)
    {
        Heart(graphics.transform, 0.22f);
        MeshNode("GentleHalo", graphics.transform, ring, blue, Vector3.zero, Vector3.one * 0.3f)
            .transform.localRotation = Quaternion.Euler(90, 0, 0);
        var serialized = new SerializedObject(effect);
        var damage = serialized.FindProperty("damage");
        damage.FindPropertyRelative("ratio").floatValue = 0.63f;
        damage.FindPropertyRelative("slider").intValue = 18;
        damage.FindPropertyRelative("baseDmg").intValue = 330;
        damage.FindPropertyRelative("type").enumValueIndex = (int)DamageType.Special;
        damage.FindPropertyRelative("multiplier").floatValue = 1;
        var stats = serialized.FindProperty("onHitStats"); stats.arraySize = 2;
        WriteStat(stats.GetArrayElementAtIndex(0), 15, Stat.Attack, SylvMoveRules.EyesAttack);
        WriteStat(stats.GetArrayElementAtIndex(1), 60, Stat.Speed, SylvMoveRules.EyesSlow);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Calm(GameObject root, GameObject graphics, SylvCalmMindEffect effect)
    {
        MeshNode("MindAura", graphics.transform, ring, pink, Vector3.up * 0.12f, Vector3.one * 0.9f);
        Sparks(graphics.transform, new Color(1, 0.7f, 0.9f), 0.8f, 12);
        var guard = Node("ReadyToBlock", graphics.transform);
        var shield = Node("GrantedShield", graphics.transform);
        for (int i = 0; i < 3; i++)
        {
            MeshNode("MindRibbon" + i, guard.transform, ring, blue, Vector3.up, Vector3.one * 0.9f)
                .transform.localRotation = Quaternion.Euler(65, i * 60, 25);
            MeshNode("ShieldRibbon" + i, shield.transform, ring, pale, Vector3.up, Vector3.one * 1.15f)
                .transform.localRotation = Quaternion.Euler(65, i * 60, -25);
        }
        Set(effect, "guard", guard); Set(effect, "shield", shield);
        shield.SetActive(false);
    }

    private static void Frolic(GameObject root, GameObject graphics, SylvFairyFrolicEffect effect)
    {
        var area = Node("LandingHitbox", root.transform).AddComponent<SphereCollider>();
        area.radius = 5; area.isTrigger = true; area.enabled = false;
        var air = Node("Airborne", graphics.transform);
        for (int i = 0; i < 3; i++)
            MeshNode("RisingRibbon" + i, air.transform, ring, i % 2 == 0 ? pink : blue,
                Vector3.up * (0.4f + i * 0.65f), Vector3.one * (1.5f - i * 0.3f));
        Sparks(air.transform, new Color(1, 0.65f, 0.85f), 1, 40);
        var landing = Node("LandingBurst", graphics.transform);
        for (int i = 0; i < 3; i++)
            MeshNode("FairyWave" + i, landing.transform, ring, i % 2 == 0 ? pink : pale,
                Vector3.up * (0.12f + i * 0.08f), Vector3.one * (5 - i * 0.4f));
        Sparks(landing.transform, new Color(1, 0.4f, 0.7f), 4.5f, 140);
        var buff = Node("FairyFrolicBuff", graphics.transform);
        MeshNode("HealingAura", buff.transform, ring, pink, Vector3.up * 0.12f, Vector3.one);
        Sparks(buff.transform, new Color(1, 0.7f, 0.9f), 0.85f, 10);
        Set(effect, "landingArea", area); Set(effect, "airborne", air); Set(effect, "landing", landing); Set(effect, "buff", buff);
        AddLandingWarning(graphics, effect, area);
        landing.SetActive(false); buff.SetActive(false);
    }

    // Upgrade an already-authored prefab without rebuilding its hand-edited art.
    [MenuItem("Tools/Sylveon/Add Missing Unite Landing Warning")]
    public static void AddMissingLandingWarning()
    {
        string path = Folder + "SylvFairyFrolic.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var effect = root.GetComponent<SylvFairyFrolicEffect>();
            var serialized = new SerializedObject(effect);
            if (serialized.FindProperty("landingWarning").objectReferenceValue != null) return;
            AddLandingWarning(root.transform.Find("Graphics").gameObject, effect,
                (SphereCollider)serialized.FindProperty("landingArea").objectReferenceValue);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void AddLandingWarning(GameObject graphics, SylvFairyFrolicEffect effect, SphereCollider area)
    {
        var legacy = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SylveonUnite.prefab");
        var warning = Object.Instantiate(legacy.transform.Find("GroundCircle").gameObject, graphics.transform, false);
        warning.name = "LandingWarning";
        var renderers = warning.GetComponentsInChildren<SpriteRenderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        // Reuse the old quarter-circle sprites, but match the current damage
        // radius exactly rather than inheriting the old prefab's approximate scale.
        float diameter = Mathf.Max(bounds.size.x, bounds.size.z);
        warning.transform.localScale *= area.radius * 2 / diameter;
        warning.transform.localPosition = new Vector3(area.center.x, 0.05f, area.center.z);
        warning.SetActive(false);
        Set(effect, "landingWarning", warning);
    }

    private static void ConfigureMoves()
    {
        var fire = Move("SylvMysticalFire", AvailableMoves.SylvMysticalCringe, MoveType.MoveA, MoveLabels.Dash, 7,
            "Mystical Fire", "Leap and create four homing flames (five when upgraded). Prioritizes enemy players. Each hit reduces Sp. Attack by 15% for 1s, up to 60%, and reduces remaining cooldown by 12%. Repeat hits on the same target deal 28% damage.", "S11", "S1A");
        var kiss = Move("SylvDrainingKiss", AvailableMoves.SylvDrainingKiss, MoveType.MoveB, MoveLabels.SureHit, 9,
            "Draining Kiss", "A kiss travels between Sylveon and its target three times, damaging and slowing the target and healing Sylveon on each return. Breaks when separated too far. Upgrading increases healing.", "S21", "S21");
        var calm = AssetDatabase.LoadAssetAtPath<MoveAsset>("Assets/Moves/Sylveon/SylvCalmMind.asset");
        calm.cooldown = 8;
        calm.description = "Increase movement speed by 30%, Sp. Attack by 40% and Sp. Defense by 10% for 3s. When upgraded, block the first move damage during this window and gain a shield for 3s. Basic attacks are not blocked; hindrances still apply.";
        EditorUtility.SetDirty(calm);
        var eyes = AssetDatabase.LoadAssetAtPath<MoveAsset>("Assets/Moves/Sylveon/SylvBabyDollEyes.asset");
        eyes.moveLabel = MoveLabels.SureHit;
        eyes.description = "Stare at an enemy, dealing damage and reducing Attack by 15% and movement speed by 60% for 1s.";
        EditorUtility.SetDirty(eyes);
        var unite = AssetDatabase.LoadAssetAtPath<MoveAsset>("Assets/Moves/Sylveon/SylvUnite.asset");
        unite.description = "Jump into the air, cleansing most statuses and becoming briefly invincible. Land to damage nearby enemies and heal for 60% of HP damage dealt. For 10s afterward, heal for 50% of HP damage dealt. Damage absorbed by shields does not heal.";
        EditorUtility.SetDirty(unite);
        var pokemon = AssetDatabase.LoadAssetAtPath<PokemonBase>("Assets/Pokemons/PlayableMons/Sylveon.asset");
        foreach (LearnableMove learn in pokemon.LearnableMoves)
        {
            if (learn.moves.Any(move => move.move == AvailableMoves.SylvHyperVoice) && !learn.moves.Contains(fire))
                learn.moves = learn.moves.Concat(new[] { fire }).ToArray();
            if (learn.moves.Any(move => move.move == AvailableMoves.SylvCalmMind) && !learn.moves.Contains(kiss))
                learn.moves = learn.moves.Concat(new[] { kiss }).ToArray();
        }
        EditorUtility.SetDirty(pokemon);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset"));
        var entries = registry.FindProperty("moveEntries");
        foreach (MoveAsset move in new[] { fire, kiss })
        {
            bool found = false;
            for (int i = 0; i < entries.arraySize; i++)
                found |= entries.GetArrayElementAtIndex(i).FindPropertyRelative("moveType").intValue == (int)move.move;
            if (found) continue;
            int index = entries.arraySize++; var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("moveType").intValue = (int)move.move;
            entry.FindPropertyRelative("moveAsset").objectReferenceValue = move;
        }
        registry.ApplyModifiedPropertiesWithoutUndo();
    }

    private static MoveAsset Move(string file, AvailableMoves id, MoveType slot, MoveLabels label, float cooldown,
        string name, string description, string icon, string previewKey)
    {
        string path = "Assets/Moves/Sylveon/" + file + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<MoveAsset>(path);
        if (existing != null) return existing;
        var move = ScriptableObject.CreateInstance<MoveAsset>();
        move.move = id; move.moveType = slot; move.moveLabel = label; move.cooldown = cooldown;
        move.moveName = name; move.description = description;
        move.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MoveIcons/Sylveon/t_Skill_Sylveon_" + icon + ".png");
        string guid = AssetDatabase.FindAssets("t_Growth_Skill_Sylveon_" + previewKey + "_", new[] { "Assets/Sprites/MoveIcons/Sylveon/Preview" }).FirstOrDefault();
        if (guid != null)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
            move.preview = new AssetReferenceSprite(guid);
            if (sprite != null) move.preview.SetEditorSubObject(sprite);
        }
        AssetDatabase.CreateAsset(move, path);
        return move;
    }

    private static void ConfigureAnimations()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Model/Pokemons/Sylveon/Sylveon.controller");
        var machine = controller.layers[0].stateMachine;
        foreach (string name in new[] { "ani_spell1a_bat_0700", "ani_spell2a_bat_0700", "ani_spellu_bat_0700", "ani_atk1_bat_0700", "ani_atk2_bat_0700", "ani_atk3_bat_0700" })
        {
            if (machine.states.Any(state => state.state.name == name)) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Model/Pokemons/Sylveon/Animations/" + name + ".anim");
            if (clip != null) machine.AddState(name).motion = clip;
        }
        EditorUtility.SetDirty(controller);
    }

    private static GameObject Node(string name, Transform parent = null)
    {
        var node = new GameObject(name) { layer = 8 };
        SceneManager.MoveGameObjectToScene(node, preview);
        if (parent != null) node.transform.SetParent(parent, false);
        return node;
    }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var node = Node(name, parent); node.transform.localPosition = position; node.transform.localScale = scale;
        node.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = node.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return node;
    }
    private static void Heart(Transform parent, float size)
    {
        MeshNode("Heart", parent, heart, pink, Vector3.zero, Vector3.one * size);
        MeshNode("PearlHeart", parent, heart, pale, Vector3.back * 0.04f, Vector3.one * size * 0.65f);
    }
    private static Material Material(string name, Color color)
    {
        string path = Materials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/DragonEnergy.mat")) { name = name };
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path); return material;
    }
    private static Mesh BakeHeart()
    {
        string path = Folder + "Art/FairyHeart.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
        const int segments = 48;
        var vertices = new Vector3[segments + 1]; var triangles = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2 / segments;
            vertices[i + 1] = new Vector3(16 * Mathf.Pow(Mathf.Sin(a), 3),
                13 * Mathf.Cos(a) - 5 * Mathf.Cos(2 * a) - 2 * Mathf.Cos(3 * a) - Mathf.Cos(4 * a), 0) / 16;
            triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % segments + 1;
        }
        return SaveMesh(path, vertices, triangles);
    }
    private static Mesh BakeFlame()
    {
        string path = Folder + "Art/MysticalFlame.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
        return SaveMesh(path, new[] { new Vector3(0.2f, 1.4f, 0), Vector3.down * 0.7f, Vector3.left, Vector3.forward, Vector3.right, Vector3.back },
            new[] { 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 2, 1, 3, 2, 1, 4, 3, 1, 5, 4, 1, 2, 5 });
    }
    private static Mesh SaveMesh(string path, Vector3[] vertices, int[] triangles)
    {
        var mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path), vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static void Sparks(Transform parent, Color color, float radius, float rate)
    {
        var node = Node("FairySparks", parent); var system = node.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.loop = true; main.playOnAwake = true;
        main.startLifetime = 0.4f; main.startSpeed = 0.25f; main.startSize = 0.08f;
        main.startColor = color; main.maxParticles = 120; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = system.emission; emission.rateOverTime = rate;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius;
        shape.rotation = new Vector3(90, 0, 0);
        node.GetComponent<ParticleSystemRenderer>().sharedMaterial = particles;
    }
    private static void WriteStat(SerializedProperty stat, int amount, Stat affected, int id)
    {
        stat.FindPropertyRelative("Amount").intValue = amount;
        stat.FindPropertyRelative("AffectedStat").enumValueIndex = (int)affected;
        stat.FindPropertyRelative("Duration").floatValue = 1;
        stat.FindPropertyRelative("IsTimed").boolValue = true;
        stat.FindPropertyRelative("IsBuff").boolValue = false;
        stat.FindPropertyRelative("Percentage").boolValue = true;
        stat.FindPropertyRelative("CanBeRemoved").boolValue = true;
        stat.FindPropertyRelative("ID").intValue = id;
    }
    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Register(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue == 0)
            throw new InvalidOperationException("Missing network spawn hash: " + path);
        prefab = PrefabUtility.SavePrefabAsset(prefab);
        var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("MovesAssets");
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
        EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset"));
        var list = registry.FindProperty("List");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) return;
        int index = list.arraySize++; var entry = list.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("Override").enumValueIndex = 0;
        entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null;
        entry.FindPropertyRelative("SourceHashToOverride").longValue = 0;
        entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;
        registry.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
