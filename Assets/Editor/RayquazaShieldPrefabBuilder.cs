using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class RayquazaShieldPrefabBuilder
{
    public const string PrefabPath = "Assets/Resources/Effects/RayquazaShield.prefab";

    [MenuItem("Tools/Rayquaza/Create Missing Shield Visual")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        EnsureFolder("Assets/Resources/Effects");
        EnsureFolder("Assets/Materials/Rayquaza");
        Material emerald = CreateMaterial("Emerald", new Color(0.12f, 1f, 0.48f));
        Material gold = CreateMaterial("Gold", new Color(1f, 0.82f, 0.18f));
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("RayquazaShield");
            SceneManager.MoveGameObjectToScene(root, preview);
            Ring(root.transform, "EmeraldAura", emerald, new Vector3(0, 0.1f, 0), Quaternion.identity, 0.95f, 0.06f);
            Ring(root.transform, "GoldAura", gold, new Vector3(0, 0.13f, 0), Quaternion.identity, 1.08f, 0.025f);
            for (int i = 0; i < 3; i++)
            {
                Ring(root.transform, "ProtectiveOrbit" + i, i == 1 ? gold : emerald, Vector3.up * 0.85f,
                    Quaternion.Euler(70, i * 60, 0), 1f, 0.025f);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void Ring(Transform parent, string name, Material material, Vector3 center, Quaternion rotation, float radius, float width)
    {
        var node = new GameObject(name);
        node.transform.SetParent(parent, false);
        node.transform.localPosition = center;
        node.transform.localRotation = rotation;
        var line = node.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.sharedMaterial = material;
        line.widthMultiplier = width;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        const int segments = 64;
        var points = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            points[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
        }
        line.positionCount = segments;
        line.SetPositions(points);
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = "Assets/Materials/Rayquaza/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Unlit.shader");
        material = new Material(shader) { name = "Rayquaza" + name };
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }
}
