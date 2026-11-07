using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Owns a manually instantiated garment's prefab lease and customized materials.
/// The key is serialized so UIObject3D clones acquire independent prefab leases.
/// </summary>
[DisallowMultipleComponent]
public sealed class TrainerClothingInstance : MonoBehaviour
{
    [SerializeField, HideInInspector] private string runtimeKey;
    [SerializeField, HideInInspector] private Transform boundSkeleton;
    [SerializeField, HideInInspector] private bool usesSharedSkeleton;
    [SerializeField, HideInInspector] private Renderer[] ownedRenderers;
    [SerializeField, HideInInspector] private SkinnedMeshRenderer[] skinRenderers;
    [SerializeField, HideInInspector] private TrainerClothingAttachment[] attachments = Array.Empty<TrainerClothingAttachment>();
    private AsyncOperationHandle<GameObject> prefabHandle;
    private readonly List<Material> ownedMaterials = new List<Material>();
    private readonly List<(Material material, int kind)> colorMaterials = new List<(Material, int)>();
    private bool materialsCached;

    private static readonly int ColorSkin = Shader.PropertyToID("_colorSkin");
    private static readonly int SHTopColor = Shader.PropertyToID("_SHTopColor");
    private static readonly int SHBotColor = Shader.PropertyToID("_SHBotColor");
    private static readonly int AtVector = Shader.PropertyToID("_AtVector");
    private static readonly int SHScale = Shader.PropertyToID("_SHScale");
    private static readonly int Emissive = Shader.PropertyToID("_Emissive");
    private static readonly int Color1 = Shader.PropertyToID("_color1");
    private static readonly int Color2 = Shader.PropertyToID("_color2");
    private static readonly int Color3 = Shader.PropertyToID("_color3");
    private static readonly int Color4 = Shader.PropertyToID("_color4");
    private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");

    public string RuntimeKey => runtimeKey;
    public bool UsesSharedSkeleton => usesSharedSkeleton;

    internal TrainerClothingBinding PrepareBinding(BoneSync sync, Transform holder)
    {
        if (sync == null || sync.baseSkeletonRoot == null) return null;
        if (boundSkeleton != null)
        {
            if (boundSkeleton != sync.baseSkeletonRoot)
                throw new InvalidOperationException("A bound garment must be cloned with its trainer or loaded again before changing skeletons.");
            return null;
        }
        return new TrainerClothingBinding(this, sync, holder);
    }

    public void BindToSkeleton(BoneSync sync)
    {
        PrepareBinding(sync, transform.parent)?.Apply();
    }

    internal void CacheRenderers(Renderer[] renderers, SkinnedMeshRenderer[] skins)
    {
        ownedRenderers = renderers;
        skinRenderers = skins;
    }

    internal void AddAttachment(TrainerClothingAttachment attachment)
    {
        int count = attachments.Length;
        Array.Resize(ref attachments, count + 1);
        attachments[count] = attachment;
    }

    internal void SetSkeletonBinding(Transform skeleton, bool shared, TrainerClothingAttachment[] nodes)
    {
        boundSkeleton = skeleton;
        usesSharedSkeleton = shared;
        attachments = nodes;
        SetAttachmentsActive(isActiveAndEnabled);
    }

    internal void AddRequiredSyncTransforms(HashSet<Transform> required, Transform root)
    {
        if (ownedRenderers == null) ownedRenderers = GetComponentsInChildren<Renderer>(true);
        if (skinRenderers == null) skinRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in ownedRenderers)
            if (renderer != null) AddAncestors(renderer.transform, root, required);
        foreach (var skin in skinRenderers)
        {
            if (skin == null) continue;
            foreach (var bone in skin.bones) AddAncestors(bone, root, required);
            AddAncestors(skin.rootBone, root, required);
        }
    }

    private static void AddAncestors(Transform bone, Transform root, HashSet<Transform> required)
    {
        if (bone == null || !bone.IsChildOf(root)) return;
        while (bone != null)
        {
            required.Add(bone);
            if (bone == root) break;
            bone = bone.parent;
        }
    }

    private void OnEnable() => SetAttachmentsActive(true);
    private void OnDisable() => SetAttachmentsActive(false);

    private void SetAttachmentsActive(bool active)
    {
        foreach (var attachment in attachments)
            if (attachment != null && attachment.Owner == this) attachment.gameObject.SetActive(active);
    }

    private void Awake()
    {
        if (!string.IsNullOrEmpty(runtimeKey))
        {
            // Ordinary Instantiate does not increment Addressables reference counts.
            // A cloned trainer must not borrow its source trainer's asset lifetime.
            if (!prefabHandle.IsValid())
                prefabHandle = Addressables.LoadAssetAsync<GameObject>(runtimeKey);
            CacheMaterials();
        }
    }

    public void Initialize(string key, AsyncOperationHandle<GameObject> handle)
    {
        runtimeKey = key;
        prefabHandle = handle; // Transfer ownership from the pending load.
    }

    private void CacheMaterials()
    {
        if (materialsCached) return;
        materialsCached = true;
        if (skinRenderers == null) skinRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in skinRenderers)
        {
            if (renderer == null) continue;
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null) continue;
                string name = material.name.ToLowerInvariant();
                int kind = name.Contains("body") || name.Contains("head") || name.Contains("000hand") ? 1
                    : name.Contains("hair") || name.Contains("shadow") ? 2
                    : name.Contains("eye0") ? 3 : 0;
                if (kind == 0) continue;

                // Leave non-customizable materials shared, preserving SRP batching.
                var copy = new Material(material);
                materials[i] = copy;
                ownedMaterials.Add(copy);
                colorMaterials.Add((copy, kind));
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }
    }

    public void ApplyColors(PlayerClothesInfo info, Color skin, Color magic)
    {
        CacheMaterials();
        Color hair = info.HairColor;
        foreach (var binding in colorMaterials)
        {
            var material = binding.material;
            if (binding.kind == 1)
            {
                material.SetColor(ColorSkin, skin);
                material.SetColor(SHTopColor, skin);
                material.SetColor(SHBotColor, magic);
                material.SetFloat(AtVector, 0f);
                material.SetFloat(SHScale, 2.21f);
                material.SetFloat(Emissive, 0.157f);
            }
            else if (binding.kind == 2)
            {
                material.SetColor(Color1, hair);
                material.SetColor(Color2, hair);
                material.SetColor(Color3, hair);
                material.SetColor(Color4, hair);
                material.SetFloat(OutlineWidth, 0.5f);
            }
            else
            {
                material.SetColor(Color1, info.EyeColor);
                material.SetColor(Color2, info.EyeColor);
            }
        }
    }

    public void ReleaseResources()
    {
        foreach (var attachment in attachments)
        {
            if (attachment == null || attachment.Owner != this) continue;
            attachment.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(attachment.gameObject);
            else DestroyImmediate(attachment.gameObject);
        }
        attachments = Array.Empty<TrainerClothingAttachment>();
        foreach (var material in ownedMaterials)
        {
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
        ownedMaterials.Clear();
        colorMaterials.Clear();
        if (prefabHandle.IsValid()) Addressables.Release(prefabHandle);
        prefabHandle = default;
    }

    private void OnDestroy() => ReleaseResources();
}
