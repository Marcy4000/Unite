using System;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GlaceonAssetAuthoring
{
    [MenuItem("Tools/Characters/Glaceon/Modernize Move Assets")]
    public static void Build()
    {
        string generic = GlaceonMoveRules.Folder + "GlaceMoveEffect.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(generic) == null)
        {
            var root = new GameObject("GlaceMoveEffect"); root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>(); root.AddComponent<GlaceMoveEffect>();
            PrefabUtility.SaveAsPrefabAsset(root, generic); Object.DestroyImmediate(root);
        }
        string projectile = GlaceonMoveRules.Folder + "GlaceProjectileEffect.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(projectile) == null)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/BasicAtk/GlaceonBasicAtk.prefab"));
            root.name = "GlaceProjectileEffect";
            foreach (var component in root.GetComponents<MonoBehaviour>())
                if (!(component is NetworkObject) && !(component is NetworkTransform)) Object.DestroyImmediate(component);
            root.AddComponent<GlaceProjectileEffect>();
            PrefabUtility.SaveAsPrefabAsset(root, projectile); Object.DestroyImmediate(root);
        }
        foreach (string path in new[] { generic, projectile, GlaceonMoveRules.Folder + "FreezeDryProjectile.prefab",
            GlaceonMoveRules.Folder + "IcicleSpear.prefab", GlaceonMoveRules.Folder + "GlaceonUnite.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var client = root.GetComponent<ClientNetworkTransform>();
                if (client != null) { Object.DestroyImmediate(client); root.AddComponent<NetworkTransform>(); }
                if (path.EndsWith("FreezeDryProjectile.prefab")) AuthorFreezeZone(root);
                var effect = root.GetComponent<PrefabMoveEffect>();
                var data = new SerializedObject(effect);
                data.FindProperty("maximumLifetime").floatValue = path.EndsWith("IcicleSpear.prefab") ? 30 : 15;
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (var body in root.GetComponentsInChildren<Rigidbody>()) { body.isKinematic = true; body.useGravity = false; }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Register(path);
        }
        const string holderPath = "Assets/Prefabs/Objects/Passives/Glaceon/IciclesHolder.prefab";
        var holder = PrefabUtility.LoadPrefabContents(holderPath);
        try
        {
            // Presentation follows the replicated actor on each observer; it needs no second transform authority.
            foreach (var networkTransform in holder.GetComponents<NetworkTransform>()) Object.DestroyImmediate(networkTransform);
            PrefabUtility.SaveAsPrefabAsset(holder, holderPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(holder); }
        // Current DB costs 100,000 energy (112s). Preserve existing move art and the authored Spear text.
        var unite = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Glaceon/GlaceonUnite.asset"));
        unite.FindProperty("uniteEnergyCost").intValue = 100000; unite.FindProperty("cooldown").floatValue = 112; unite.ApplyModifiedPropertiesWithoutUndo();
        var freeze = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Glaceon/GlaceFreezeDry.asset"));
        freeze.FindProperty("cooldown").floatValue = 7.5f;
        freeze.FindProperty("description").stringValue = freeze.FindProperty("description").stringValue.Replace("50% for 3s", "50% for 4s");
        freeze.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
    }
    private static void AuthorFreezeZone(GameObject root)
    {
        if (root.transform.Find("IceZone") != null) return;
        var graphics = new GameObject("ProjectileGraphics");
        graphics.transform.SetParent(root.transform, false);
        foreach (var child in root.transform.Cast<Transform>().Where(t => t != graphics.transform).ToArray()) child.SetParent(graphics.transform, false);
        var zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder); zone.name = "IceZone";
        Object.DestroyImmediate(zone.GetComponent<Collider>());
        zone.transform.SetParent(root.transform, false);
        zone.transform.localScale = new Vector3(2, 0.025f, 2);
        zone.transform.localPosition = new Vector3(0, -0.75f, 0);
        const string materialPath = "Assets/Materials/GlaceonIceZone.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(0.35f, 0.8f, 1, 0.45f));
            material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", 5); material.SetFloat("_DstBlend", 10);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            AssetDatabase.CreateAsset(material, materialPath);
        }
        zone.GetComponent<Renderer>().sharedMaterial = material; zone.SetActive(false);
    }
    private static void Register(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        PrefabUtility.SavePrefabAsset(prefab);
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
}
