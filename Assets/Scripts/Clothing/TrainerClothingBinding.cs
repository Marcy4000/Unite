using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>An equip-time plan; never evaluates animation or modifies shared meshes.</summary>
internal sealed class TrainerClothingBinding
{
    private readonly TrainerClothingInstance owner;
    private readonly Transform skeleton;
    private readonly Transform clothingRoot;
    private readonly Dictionary<Transform, Transform> mapped = new Dictionary<Transform, Transform>();
    private readonly Transform[] originalTransforms;
    private readonly Renderer[] renderers;
    private readonly SkinnedMeshRenderer[] skins;
    private readonly bool shared;

    internal TrainerClothingBinding(TrainerClothingInstance owner, BoneSync sync, Transform holder)
    {
        this.owner = owner;
        skeleton = sync.baseSkeletonRoot;
        clothingRoot = TrainerModel.FindClothingSkeleton(owner.transform);
        renderers = owner.GetComponentsInChildren<Renderer>(true);
        skins = owner.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        originalTransforms = clothingRoot.GetComponentsInChildren<Transform>(true);
        var lookup = sync.GetSkeletonLookup();
        Map(clothingRoot, "", lookup);

        // Legacy copying inherits this entire parent space. Direct bones can only
        // replace it when it is the same space as the animated reference's parent.
        Matrix4x4 space = Matrix4x4.TRS(owner.transform.localPosition, owner.transform.localRotation, owner.transform.localScale);
        var ancestor = holder;
        while (ancestor != null && ancestor != skeleton.parent)
        {
            space = Matrix4x4.TRS(ancestor.localPosition, ancestor.localRotation, ancestor.localScale) * space;
            ancestor = ancestor.parent;
        }
        shared = ancestor == skeleton.parent && IsIdentity(space);
        foreach (var renderer in renderers)
            if (!renderer.transform.IsChildOf(clothingRoot)) shared = false;
        foreach (var skin in skins)
        {
            foreach (var bone in skin.bones)
                if (bone == null || !bone.IsChildOf(clothingRoot)) shared = false;
            if (skin.rootBone != null && !skin.rootBone.IsChildOf(clothingRoot)) shared = false;
        }
        owner.CacheRenderers(renderers, skins);
    }

    private void Map(Transform target, string path, IReadOnlyDictionary<string, Transform> lookup)
    {
        if (!lookup.TryGetValue(path, out var source)) return;
        mapped[target] = source;
        foreach (Transform child in target)
            Map(child, path.Length == 0 ? child.name : path + "/" + child.name, lookup);
    }

    private static bool IsIdentity(Matrix4x4 value)
    {
        for (int i = 0; i < 16; i++)
            if (float.IsNaN(value[i]) || Mathf.Abs(value[i] - Matrix4x4.identity[i]) > 0.00001f) return false;
        return true;
    }

    internal void Apply()
    {
        // Establish the same initial pose, including renderer/slot placeholders.
        foreach (var pair in mapped) BoneSync.CopyLocalPose(pair.Value, pair.Key);
        if (!shared)
        {
            owner.SetSkeletonBinding(skeleton, false, Array.Empty<TrainerClothingAttachment>());
            return;
        }

        var branches = new HashSet<Transform>();
        foreach (var skin in skins)
        {
            foreach (var bone in skin.bones) RetainBranch(bone, branches);
            RetainBranch(skin.rootBone, branches);
        }
        foreach (var renderer in renderers)
            if (!(renderer is SkinnedMeshRenderer)) RetainBranch(renderer.transform, branches);

        var attachments = new Dictionary<Transform, TrainerClothingAttachment>();
        // Register ownership as nodes are created so a cancelled commit can clean up.
        foreach (var branch in branches)
        {
            bool isMapped = mapped.TryGetValue(branch, out var source);
            Transform parent = isMapped ? source : mapped[branch.parent];
            if (!attachments.TryGetValue(parent, out var attachment))
            {
                var node = new GameObject("__Clothing_" + owner.GetInstanceID());
                node.SetActive(false);
                node.layer = owner.gameObject.layer;
                node.transform.SetParent(parent, false);
                attachment = node.AddComponent<TrainerClothingAttachment>();
                attachment.Initialize(owner);
                attachments.Add(parent, attachment);
                owner.AddAttachment(attachment);
            }
            branch.SetParent(attachment.transform, false);
            if (isMapped)
            {
                branch.localPosition = Vector3.zero;
                branch.localRotation = Quaternion.identity;
                branch.localScale = Vector3.one;
            }
        }
        foreach (var skin in skins)
        {
            var bones = skin.bones;
            for (int i = 0; i < bones.Length; i++)
                if (mapped.TryGetValue(bones[i], out var source)) bones[i] = source;
            skin.bones = bones;
            if (skin.rootBone != null && mapped.TryGetValue(skin.rootBone, out var root)) skin.rootBone = root;
        }

        // Keep renderer ownership/preview bounds under the original slot wherever
        // possible. All skinning bones now belong to the base rig or owned branches.
        var keep = new HashSet<Transform> { clothingRoot };
        foreach (var renderer in renderers) KeepAncestors(renderer.transform, keep);
        foreach (var bone in originalTransforms)
            if (bone != null && bone.IsChildOf(owner.transform) && bone.GetComponents<Component>().Length > 1)
                KeepAncestors(bone, keep);
        foreach (var bone in originalTransforms)
        {
            if (bone == null || bone == clothingRoot || !bone.IsChildOf(owner.transform) || keep.Contains(bone)) continue;
            if (bone.parent != null && keep.Contains(bone.parent))
            {
                bone.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(bone.gameObject);
                else UnityEngine.Object.DestroyImmediate(bone.gameObject);
            }
        }
        owner.SetSkeletonBinding(skeleton, true, new List<TrainerClothingAttachment>(attachments.Values).ToArray());
    }

    private void RetainBranch(Transform bone, HashSet<Transform> branches)
    {
        if (bone == null) return;
        if (mapped.ContainsKey(bone))
        {
            // Rigid geometry on a matching transform must follow that transform.
            if (bone.GetComponent<Renderer>() != null) branches.Add(bone);
            return;
        }
        while (bone.parent != null && !mapped.ContainsKey(bone.parent)) bone = bone.parent;
        if (bone.parent == null) throw new InvalidOperationException("Clothing attachment is outside its skeleton.");
        branches.Add(bone);
    }

    private void KeepAncestors(Transform bone, HashSet<Transform> keep)
    {
        if (!bone.IsChildOf(owner.transform)) return;
        while (bone != null && bone != owner.transform)
        {
            keep.Add(bone);
            bone = bone.parent;
        }
    }
}
