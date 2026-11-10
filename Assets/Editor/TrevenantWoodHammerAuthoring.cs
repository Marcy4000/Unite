using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Targeted, one-time upgrade of Wood Hammer's placeholder graphics. Leaves gameplay, network identity and the supplied FBX/material untouched.</summary>
public static class TrevenantWoodHammerAuthoring
{
    private const string Art = TrevenantMoveRules.Folder + "Art/";
    private const string Materials = "Assets/Materials/Trevenant/";
    private const string IndicatorSource = "Assets/Sprites/AimIndicator/pref_Indicator_Sector_180_lv3/pref_Indicator_Sector_180_lv3.fbx";
    private static Scene scene;
    private static Mesh log, ring, arc, leaf;
    private static Material wood, grain, glow, trail;

    [MenuItem("Tools/Characters/Trevenant/Upgrade Wood Hammer Presentation")]
    public static void Build()
    {
        log = LogMesh(); leaf = LeafMesh();
        ring = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Duraludon/Art/ThinRing.asset");
        arc = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "HammerSector.asset");
        wood = Material("HammerBark", "HauntedBark", new Color(0.12f, 0.3f, 0.045f));
        grain = Material("HammerHeartwood", "SpectralWood", new Color(0.57f, 0.8f, 0.15f));
        glow = Material("HammerSpirit", "SpiritCore", new Color(0.75f, 1, 0.16f));
        trail = TrailMaterial();
        UpgradeHammer(); CreateSectorIndicator();
        AssetDatabase.SaveAssets();
        Debug.Log("Trevenant Wood Hammer: hand-bound spectral log hammers, swing trails, impact bursts and supplied 180-degree indicator authored.");
    }
    private static void UpgradeHammer()
    {
        string path = TrevenantMoveRules.Folder + "TrevenantWoodHammer.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponentInChildren<TrevenantWoodHammerView>(true) != null) return; // Preserve subsequent artist edits.
            scene = root.scene;
            var old = root.transform.Find("Graphics"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var graphics = Node("Graphics", root.transform);
            var effect = root.GetComponent<TrevenantWoodHammerEffect>();
            Set(effect, "graphics", graphics);
            Set(effect, "castVisual", Node("Cast", graphics.transform));
            var active = Node("Active", graphics.transform); active.SetActive(false); Set(effect, "activeVisual", active);
            var impact = Node("Impact", graphics.transform); impact.SetActive(false); Set(effect, "impactVisual", impact);
            var presentation = Node("HandBoundHammers", graphics.transform);
            var view = presentation.AddComponent<TrevenantWoodHammerView>(); Set(effect, "hammerView", view);
            var sweepTrails = new List<TrailRenderer>(); var slamTrails = new List<TrailRenderer>();
            var left = Hammer("LeftHandHammer", presentation.transform, sweepTrails);
            var right = Hammer("RightHandHammer", presentation.transform, sweepTrails);
            var slam = Hammer("TwoHandSlamHammer", presentation.transform, slamTrails);
            Set(view, "leftHammer", left.transform); Set(view, "rightHammer", right.transform); Set(view, "slamHammer", slam.transform);
            var sweep = Node("SweepingImpact", presentation.transform);
            MeshNode("ImpactCrescent", sweep.transform, arc, glow, Vector3.zero, Vector3.one * TrevenantMoveRules.HammerRange);
            Burst(sweep.transform, 1.8f, 20);
            var burst = Node("SlamImpact", presentation.transform);
            MeshNode("OuterShockwave", burst.transform, ring, glow, Vector3.zero, Vector3.one * 1.8f);
            MeshNode("InnerShockwave", burst.transform, ring, grain, Vector3.up * 0.02f, Vector3.one * 1.2f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                var crack = Primitive("RadiatingWoodSplinter", burst.transform, PrimitiveType.Cube, wood,
                    new Vector3(Mathf.Sin(a), 0.12f, Mathf.Cos(a)) * 1.2f, new Vector3(0.1f, 0.12f, 1.1f));
                crack.transform.localRotation = Quaternion.Euler(-15, i * 45, 0);
            }
            Burst(burst.transform, 1.5f, 35);
            Set(view, "sweepImpact", sweep.transform); Set(view, "slamImpact", burst.transform);
            var data = new SerializedObject(view); Array(data, "sweepTrails", sweepTrails); Array(data, "slamTrails", slamTrails); data.ApplyModifiedPropertiesWithoutUndo();
            left.SetActive(false); right.SetActive(false); slam.SetActive(false); sweep.SetActive(false); burst.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static GameObject Hammer(string name, Transform parent, List<TrailRenderer> trails)
    {
        var root = Node(name, parent);
        Primitive("GnarledHandle", root.transform, PrimitiveType.Cylinder, wood, Vector3.up * 0.42f, new Vector3(0.23f, 0.55f, 0.23f));
        Primitive("LivingWoodGrip", root.transform, PrimitiveType.Cylinder, grain, Vector3.up * 0.18f, new Vector3(0.26f, 0.22f, 0.26f));
        MeshNode("TaperedLogHead", root.transform, log, wood, Vector3.up * 1.05f, Vector3.one);
        for (int side = -1; side <= 1; side += 2)
        {
            var cap = Primitive("CutWoodEnd", root.transform, PrimitiveType.Cylinder, grain, new Vector3(side * 0.91f, 1.05f, 0), new Vector3(1.02f, 0.018f, 1.02f));
            cap.transform.localRotation = Quaternion.Euler(0, 0, 90);
            for (int j = 0; j < 3; j++)
            {
                var growthRing = MeshNode("GlowingGrowthRing", root.transform, ring, glow, new Vector3(side * 0.94f, 1.05f, 0), Vector3.one * (0.13f + j * 0.14f));
                growthRing.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            var tip = Node("SwingTrailTip", root.transform); tip.transform.localPosition = new Vector3(side * 0.95f, 1.05f, 0);
            var ribbon = tip.AddComponent<TrailRenderer>(); ribbon.sharedMaterial = trail;
            ribbon.time = TrevenantMoveRules.HammerTrailLifetime; ribbon.minVertexDistance = 0.025f;
            ribbon.startWidth = 0.22f; ribbon.endWidth = 0.015f; ribbon.numCornerVertices = 2; ribbon.numCapVertices = 2;
            ribbon.startColor = new Color(0.65f, 1, 0.15f, 0.8f); ribbon.endColor = new Color(0.3f, 0.9f, 0.08f, 0);
            ribbon.shadowCastingMode = ShadowCastingMode.Off; ribbon.receiveShadows = false; ribbon.emitting = false; trails.Add(ribbon);
            var vine = Primitive("SpiralingVine", root.transform, PrimitiveType.Cylinder, glow, new Vector3(side * 0.4f, 1.05f, 0), new Vector3(1.14f, 0.028f, 1.14f));
            vine.transform.localRotation = Quaternion.Euler(10, 0, 90);
            var sprout = MeshNode("SpectralLeaf", root.transform, leaf, glow, new Vector3(side * 0.27f, 0.68f, 0.05f), new Vector3(side * 0.55f, 0.55f, 0.55f));
            sprout.transform.localRotation = Quaternion.Euler(0, side * 20, side * -25);
        }
        return root;
    }
    private static void CreateSectorIndicator()
    {
        string path = "Assets/Resources/" + TrevenantMoveRules.Visuals + "pref_Indicator_Sector_180_lv3.prefab";
        var material = SectorMaterial();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            var existing = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var renderer in existing.GetComponentsInChildren<Renderer>(true))
                    if (AssetDatabase.GetAssetPath(renderer.sharedMaterial).StartsWith("Assets/Sprites/AimIndicator/pref_Indicator_Sector_180_lv3/", StringComparison.Ordinal))
                    { renderer.sharedMaterial = material; renderer.receiveShadows = false; renderer.shadowCastingMode = ShadowCastingMode.Off; changed = true; }
                if (changed) PrefabUtility.SaveAsPrefabAsset(existing, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(existing); }
            return;
        }
        var preview = EditorSceneManager.NewPreviewScene(); scene = preview;
        try
        {
            var root = Node("pref_Indicator_Sector_180_lv3");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(IndicatorSource);
            if (source == null) throw new InvalidOperationException("Missing supplied Wood Hammer sector indicator.");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, preview); model.transform.SetParent(root.transform, false);
            Bounds bounds = default; bool found = false;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            { if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds); }
            if (!found || bounds.max.z <= 0) throw new InvalidOperationException("Cannot measure supplied sector indicator.");
            model.transform.localScale *= TrevenantMoveRules.AuthoredHammerRange / bounds.max.z;
            model.transform.localPosition = Vector3.up * 0.04f;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            { renderer.sharedMaterial = material; renderer.receiveShadows = false; renderer.shadowCastingMode = ShadowCastingMode.Off; }
            // Preserve the user's import settings/material asset. The wrapper
            // uses its own transparent copy with the supplied matching texture.
            PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
    private static Mesh LogMesh()
    {
        string path = Art + "SpectralHammerLog.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>(); const int sides = 16;
        for (int section = 0; section < 3; section++)
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                float radius = (section == 1 ? 0.58f : 0.5f) * (1 + 0.08f * Mathf.Sin(a * 5));
                vertices.Add(new Vector3((section - 1) * 0.9f, Mathf.Sin(a) * radius, Mathf.Cos(a) * radius));
                if (section < 2 && i < sides) { int n = section * (sides + 1) + i; triangles.AddRange(new[] { n, n + 1, n + sides + 1, n + 1, n + sides + 2, n + sides + 1 }); }
            }
        var mesh = new Mesh { name = "SpectralHammerLog" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static Mesh LeafMesh()
    {
        string path = Art + "SpectralHammerLeaf.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var mesh = new Mesh { name = "SpectralHammerLeaf", vertices = new[] { Vector3.zero, new Vector3(0.35f, 0.2f, 0.08f), new Vector3(0.85f, 0.1f, 0), new Vector3(0.4f, -0.18f, 0), new Vector3(0.4f, 0, 0.15f) }, triangles = new[] { 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4 } };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static Material Material(string name, string source, Color color)
    {
        string path = Materials + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
        material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Materials + source + ".mat")) { name = name }; material.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(material, path); return material;
    }
    private static Material TrailMaterial()
    {
        string path = Materials + "HammerTrail.mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
        material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat")) { name = "HammerTrail", renderQueue = 3000 };
        material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetOverrideTag("RenderType", "Transparent"); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); AssetDatabase.CreateAsset(material, path); return material;
    }
    private static Material SectorMaterial()
    {
        string path = Materials + "WoodHammerSector.mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
        material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Materials + "AimMint.mat")) { name = "WoodHammerSector", renderQueue = 3000 };
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/AimIndicator/pref_Indicator_Sector_180_lv3/t_fx_indicator_Sector_02_sam.png"));
        material.SetColor("_BaseColor", new Color(1, 1, 1, 0.8f)); material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetOverrideTag("RenderType", "Transparent"); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.DisableKeyword("_ALPHATEST_ON");
        AssetDatabase.CreateAsset(material, path); return material;
    }
    private static void Burst(Transform parent, float radius, short count)
    {
        var node = Node("WoodAndSpiritBurst", parent); var system = node.AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.loop = false; main.playOnAwake = false; main.duration = 0.25f; main.startLifetime = 0.28f;
        main.startSpeed = 3; main.startSize = 0.12f; main.startColor = new Color(0.65f, 1, 0.16f); main.gravityModifier = 0.2f; main.maxParticles = 64;
        var emission = system.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, count) });
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = radius;
        var renderer = node.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = log; renderer.sharedMaterial = grain;
    }
    private static GameObject Node(string name, Transform parent = null)
    { var node = new GameObject(name) { layer = 8 }; SceneManager.MoveGameObjectToScene(node, scene); if (parent != null) node.transform.SetParent(parent, false); return node; }
    private static GameObject Primitive(string name, Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale)
    {
        var node = GameObject.CreatePrimitive(type); node.name = name; node.layer = 8; SceneManager.MoveGameObjectToScene(node, scene); node.transform.SetParent(parent, false);
        node.transform.localPosition = position; node.transform.localScale = scale; Object.DestroyImmediate(node.GetComponent<Collider>());
        var renderer = node.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return node;
    }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    { var node = Node(name, parent); node.transform.localPosition = position; node.transform.localScale = scale; node.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = node.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; return node; }
    private static void Set(Object target, string field, Object value) { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Array(SerializedObject data, string name, List<TrailRenderer> trails)
    { var list = data.FindProperty(name); list.arraySize = trails.Count; for (int i = 0; i < trails.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = trails[i]; }
}
