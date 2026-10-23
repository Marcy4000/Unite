using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// Replaces category-folder bundles with model roots and shared dependency islands.
/// Never moves source files, changes GUIDs, or reorders clothing metadata.
/// Run again after importing clothing to refresh the dependency partition.
/// </summary>
public static class ClothingAddressablesOptimizer
{
    public const string ModelsGroup = "ClothingItems";
    public const string IconsGroup = "ClothingIcons";
    public const string SharedGroup = "ClothingShared";
    public const string ShadersGroup = "TrainerShaders";
    public const string CardArtGroup = "TrainerCardArt";
    private const string SharedLabelPrefix = "clothing-shared-";
    private const string IconLabelPrefix = "clothing-icons-";

    public static AddressableAssetGroup GetModelGroup(AddressableAssetSettings settings)
        => GetGroup(settings, ModelsGroup, BundledAssetGroupSchema.BundlePackingMode.PackSeparately);

    public static AddressableAssetGroup GetIconGroup(AddressableAssetSettings settings)
        => GetGroup(settings, IconsGroup, BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel);

    public static void SetIconLabel(AddressableAssetSettings settings, AddressableAssetEntry entry)
    {
        // Sixteen bounded icon bundles instead of one bundle per thumbnail.
        SetPartitionLabel(settings, entry, IconLabelPrefix, IconLabelPrefix + entry.guid.Substring(0, 1));
    }

    [MenuItem("Tools/Clothing/Optimize Addressables Packaging")]
    public static void OptimizeMenu()
    {
        Debug.Log(Optimize());
    }

    public static string Optimize()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Clothing packaging must be optimized outside Play mode.");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("Addressables settings not found.");

        var models = GetModelGroup(settings);
        var icons = GetIconGroup(settings);
        var shared = GetGroup(settings, SharedGroup, BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel);
        var shaders = GetGroup(settings, ShadersGroup, BundledAssetGroupSchema.BundlePackingMode.PackTogether);
        var cards = GetGroup(settings, CardArtGroup, BundledAssetGroupSchema.BundlePackingMode.PackSeparately);
        var folderEntries = models.entries.Where(e => AssetDatabase.IsValidFolder(e.AssetPath)).ToArray();
        var inheritedLabels = folderEntries.ToDictionary(e => e.AssetPath, e => e.labels.ToArray());

        // Promote runtime-referenced assets before removing their parent entries.
        // This also preserves references to any models which were implicit entries.
        foreach (string guid in AssetDatabase.FindAssets("t:ClothingItem"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ClothingItem>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var prefab in item.prefabs)
            {
                if (prefab != null && prefab.RuntimeKeyIsValid())
                    EnsureEntry(settings, prefab.AssetGUID, models);
            }
            if (item.sprite != null && item.sprite.RuntimeKeyIsValid())
            {
                var entry = EnsureEntry(settings, item.sprite.AssetGUID, icons);
                if (entry != null && entry.parentGroup == icons) SetIconLabel(settings, entry);
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:TrainerCardItem"))
        {
            var item = AssetDatabase.LoadAssetAtPath<TrainerCardItem>(AssetDatabase.GUIDToAssetPath(guid));
            if (item.itemSprite != null && item.itemSprite.RuntimeKeyIsValid())
                EnsureEntry(settings, item.itemSprite.AssetGUID, cards);
            if (item.itemIcon != null && item.itemIcon.RuntimeKeyIsValid())
                EnsureEntry(settings, item.itemIcon.AssetGUID, cards);
        }

        foreach (var folder in folderEntries)
        {
            // Trainer-card folders are small but still contain many unrelated images.
            if (folder.AssetPath.StartsWith("Assets/Sprites/Lobby/TrainerCards/", StringComparison.Ordinal))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder.AssetPath }))
                    EnsureEntry(settings, guid, cards);
            }
            settings.RemoveAssetEntry(folder.guid, false);
        }

        foreach (var entry in models.entries.ToArray())
        {
            if (AssetDatabase.GetMainAssetTypeAtPath(entry.AssetPath) != typeof(Texture2D)) continue;
            settings.CreateOrMoveEntry(entry.guid, icons, false, false);
            SetIconLabel(settings, entry);
        }

        var users = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var types = new Dictionary<string, Type>(StringComparer.Ordinal);
        var roots = models.entries.Where(e => !AssetDatabase.IsValidFolder(e.AssetPath))
            .OrderBy(e => e.guid, StringComparer.Ordinal).ToArray();
        foreach (var root in roots)
        {
            InheritLabels(settings, root, inheritedLabels);
            foreach (string path in AssetDatabase.GetDependencies(root.AssetPath, true))
            {
                if (path == root.AssetPath) continue;
                if (!types.TryGetValue(path, out var type))
                {
                    type = AssetDatabase.GetMainAssetTypeAtPath(path);
                    types[path] = type;
                }
                if (type == null || !(typeof(Material).IsAssignableFrom(type)
                    || typeof(Texture).IsAssignableFrom(type) || typeof(Shader).IsAssignableFrom(type))) continue;
                if (!users.TryGetValue(path, out var references)) users[path] = references = new List<string>();
                references.Add(root.guid);
            }
        }

        var managedDependencies = new HashSet<string>();
        foreach (var dependency in users.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            bool shader = typeof(Shader).IsAssignableFrom(types[dependency.Key]);
            if (!shader && dependency.Value.Count < 2) continue;
            string guid = AssetDatabase.AssetPathToGUID(dependency.Key);
            var entry = EnsureEntry(settings, guid, shader ? shaders : shared);
            if (entry == null || entry.parentGroup != (shader ? shaders : shared)) continue;
            managedDependencies.Add(guid);
            if (!shader)
                SetPartitionLabel(settings, entry, SharedLabelPrefix, GetSharedLabel(dependency.Value));
            InheritLabels(settings, entry, inheritedLabels);
        }

        // Prune stale generated entries, not their source assets. Unshared dependencies
        // then follow their model into its bundle rather than staying separate forever.
        foreach (var group in new[] { shared, shaders })
        {
            foreach (var entry in group.entries.ToArray())
            {
                if (!managedDependencies.Contains(entry.guid)) settings.RemoveAssetEntry(entry.guid, false);
            }
        }

        foreach (var group in new[] { models, icons, cards })
        {
            foreach (var entry in group.entries) InheritLabels(settings, entry, inheritedLabels);
        }
        foreach (var group in new[] { models, icons, shared, shaders, cards }) EditorUtility.SetDirty(group);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        return $"Clothing packaging optimized: {roots.Length} model roots, {icons.entries.Count} icons, "
            + $"{shared.entries.Count} shared assets, {shaders.entries.Count} shaders, {cards.entries.Count} card images; "
            + $"removed {folderEntries.Length} folder entries. Rebuild Addressables and the player before measuring.";
    }

    public static string GetSharedLabel(IEnumerable<string> modelGuids)
    {
        // Assets used by exactly the same models travel together. Global shaders
        // are excluded from these sets so they cannot connect every outfit together.
        string signature = string.Join("\n", modelGuids.Distinct().OrderBy(guid => guid, StringComparer.Ordinal));
        using (var sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(signature));
            return SharedLabelPrefix + BitConverter.ToString(hash, 0, 16).Replace("-", "").ToLowerInvariant();
        }
    }

    private static AddressableAssetEntry EnsureEntry(AddressableAssetSettings settings, string guid, AddressableAssetGroup group)
    {
        if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) return null;
        var existing = settings.FindAssetEntry(guid);
        // Respect assets explicitly owned by groups outside this migration.
        if (existing != null && !IsManagedGroup(existing.parentGroup.Name)) return existing;
        return settings.CreateOrMoveEntry(guid, group, false, false);
    }

    private static bool IsManagedGroup(string name)
        => name == ModelsGroup || name == IconsGroup || name == SharedGroup || name == ShadersGroup || name == CardArtGroup;

    private static void InheritLabels(AddressableAssetSettings settings, AddressableAssetEntry entry,
        Dictionary<string, string[]> inherited)
    {
        foreach (var folder in inherited)
        {
            if (!entry.AssetPath.StartsWith(folder.Key + "/", StringComparison.Ordinal)) continue;
            foreach (string label in folder.Value)
            {
                settings.AddLabel(label, false);
                entry.SetLabel(label, true, false, false);
            }
        }
    }

    private static void SetPartitionLabel(AddressableAssetSettings settings, AddressableAssetEntry entry, string prefix, string label)
    {
        foreach (string oldLabel in entry.labels.Where(value => value != label && value.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
            entry.SetLabel(oldLabel, false, false, false);
        settings.AddLabel(label, false);
        entry.SetLabel(label, true, false, false);
    }

    private static AddressableAssetGroup GetGroup(AddressableAssetSettings settings, string name,
        BundledAssetGroupSchema.BundlePackingMode packing)
    {
        var group = settings.FindGroup(name);
        if (group == null)
            group = settings.CreateGroup(name, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema == null) schema = group.AddSchema<BundledAssetGroupSchema>();
        schema.UseDefaultSchemaSettings = false;
        schema.BundleMode = packing;
        schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        schema.AssetLoadMode = UnityEngine.ResourceManagement.ResourceProviders.AssetLoadMode.RequestedAssetAndDependencies;
        schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
        schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
        // Deliberately retain CRC validation. Its integrity tradeoff needs a separate A/B test.
        EditorUtility.SetDirty(schema);
        return group;
    }
}
