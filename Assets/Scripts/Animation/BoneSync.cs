using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class BoneSync : MonoBehaviour
{
    [Tooltip("The root transform of the base skeleton.")]
    public Transform baseSkeletonRoot;

    [Tooltip("The root transforms of the clothing items that need to sync with the base skeleton.")]
    public Transform[] clothingRoots;

    private struct TransformPair
    {
        public Transform source;
        public Transform target;
    }

    private TransformPair[] bonePairs = System.Array.Empty<TransformPair>();
    private Dictionary<string, Transform> skeletonLookup;
    private Transform lookupRoot;

    public int SyncPairCount => bonePairs.Length;

    internal IReadOnlyDictionary<string, Transform> GetSkeletonLookup()
    {
        if (skeletonLookup != null && lookupRoot == baseSkeletonRoot) return skeletonLookup;
        skeletonLookup = new Dictionary<string, Transform>();
        lookupRoot = baseSkeletonRoot;
        var excluded = new HashSet<Transform>();
        if (clothingRoots != null)
            foreach (var root in clothingRoots)
                if (root != null && root != baseSkeletonRoot) excluded.Add(root.parent);
        if (baseSkeletonRoot != null) AddSkeletonTransform(baseSkeletonRoot, "", excluded);
        return skeletonLookup;
    }

    private void AddSkeletonTransform(Transform bone, string path, HashSet<Transform> excluded)
    {
        if (excluded.Contains(bone) || bone.GetComponent<TrainerClothingInstance>() != null ||
            bone.GetComponent<TrainerClothingAttachment>() != null) return;
        skeletonLookup[path] = bone;
        foreach (Transform child in bone)
            AddSkeletonTransform(child, path.Length == 0 ? child.name : path + "/" + child.name, excluded);
    }

    private void OnEnable()
    {
        // UIObject3D clones clothingRoots but not the runtime-only bonePairs cache.
        RebuildSyncData();
    }

    // Call this from your initialization script whenever clothingRoots changes.
    public void RebuildSyncData()
    {
        if (baseSkeletonRoot == null || clothingRoots == null || clothingRoots.Length == 0)
        {
            bonePairs = System.Array.Empty<TransformPair>();
            if (Application.isPlaying) enabled = false;
            return;
        }

        List<TransformPair> pairsList = new List<TransformPair>();

        foreach (var clothingRoot in clothingRoots)
        {
            if (clothingRoot != null)
            {
                var owner = clothingRoot.GetComponentInParent<TrainerClothingInstance>(true);
                if (owner != null && owner.UsesSharedSkeleton) continue;
                HashSet<Transform> required = null;
                if (owner != null)
                {
                    required = new HashSet<Transform>();
                    owner.AddRequiredSyncTransforms(required, clothingRoot);
                }
                MapBones(baseSkeletonRoot, clothingRoot, pairsList, required);
            }
        }

        bonePairs = pairsList.ToArray();
        if (Application.isPlaying) enabled = bonePairs.Length > 0;
    }

    private void MapBones(Transform baseBone, Transform clothingBone, List<TransformPair> pairsList, HashSet<Transform> required)
    {
        if (required != null && !required.Contains(clothingBone)) return;
        pairsList.Add(new TransformPair { source = baseBone, target = clothingBone });

        for (int i = 0; i < baseBone.childCount; i++)
        {
            Transform baseChild = baseBone.GetChild(i);
            Transform clothingChild = clothingBone.Find(baseChild.name);

            if (clothingChild != null)
            {
                MapBones(baseChild, clothingChild, pairsList, required);
            }
        }
    }

    public void ForceSync()
    {
        for (int i = 0; i < bonePairs.Length; i++)
        {
            Transform source = bonePairs[i].source;
            Transform target = bonePairs[i].target;

            if (source == null || target == null)
            {
                continue;
            }

            CopyLocalPose(source, target);
        }
    }

    internal static void CopyLocalPose(Transform source, Transform target)
    {
        Vector3 position = source.localPosition;
        Quaternion rotation = source.localRotation;
        Vector3 scale = source.localScale;
        if (!float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsNaN(position.z))
            target.localPosition = position;
        if (!float.IsNaN(rotation.w) && !float.IsNaN(rotation.x) && !float.IsNaN(rotation.y) && !float.IsNaN(rotation.z))
            target.localRotation = rotation;
        if (!float.IsNaN(scale.x) && !float.IsNaN(scale.y) && !float.IsNaN(scale.z))
            target.localScale = scale;
    }

    private void LateUpdate()
    {
        ForceSync();
    }
    
    #if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensures bones are mapped when changing fields in the editor inspector
        if (!Application.isPlaying)
        {
            RebuildSyncData();
        }
    }
    #endif
}
