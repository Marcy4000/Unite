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

    private TransformPair[] bonePairs = new TransformPair[0];

    // Call this from your initialization script whenever clothingRoots changes.
    public void RebuildSyncData()
    {
        if (baseSkeletonRoot == null || clothingRoots == null || clothingRoots.Length == 0)
        {
            bonePairs = new TransformPair[0];
            return;
        }

        List<TransformPair> pairsList = new List<TransformPair>();

        foreach (var clothingRoot in clothingRoots)
        {
            if (clothingRoot != null)
            {
                MapBones(baseSkeletonRoot, clothingRoot, pairsList);
            }
        }

        bonePairs = pairsList.ToArray();
    }

    private void MapBones(Transform baseBone, Transform clothingBone, List<TransformPair> pairsList)
    {
        pairsList.Add(new TransformPair { source = baseBone, target = clothingBone });

        for (int i = 0; i < baseBone.childCount; i++)
        {
            Transform baseChild = baseBone.GetChild(i);
            Transform clothingChild = clothingBone.Find(baseChild.name);

            if (clothingChild != null)
            {
                MapBones(baseChild, clothingChild, pairsList);
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

            if (!float.IsNaN(source.localPosition.x) && !float.IsNaN(source.localPosition.y) && !float.IsNaN(source.localPosition.z))
            {
                target.localPosition = source.localPosition;
            }

            if (!float.IsNaN(source.localRotation.w) && !float.IsNaN(source.localRotation.x) && !float.IsNaN(source.localRotation.y) && !float.IsNaN(source.localRotation.z))
            {
                target.localRotation = source.localRotation;
            }

            if (!float.IsNaN(source.localScale.x) && !float.IsNaN(source.localScale.y) && !float.IsNaN(source.localScale.z))
            {
                target.localScale = source.localScale;
            }
        }
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