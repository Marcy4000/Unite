using UnityEngine;

[ExecuteInEditMode]
public class BoneSync : MonoBehaviour
{
    [Tooltip("The root transform of the base skeleton.")]
    public Transform baseSkeletonRoot;

    [Tooltip("The root transforms of the clothing items that need to sync with the base skeleton.")]
    public Transform[] clothingRoots;

    private void LateUpdate()
    {
        if (baseSkeletonRoot == null || clothingRoots == null || clothingRoots.Length == 0)
        {
            return;
        }

        foreach (var clothingRoot in clothingRoots)
        {
            if (clothingRoot != null)
            {
                SyncBoneTransforms(baseSkeletonRoot, clothingRoot);
            }
        }
    }

    private void SyncBoneTransforms(Transform baseBone, Transform clothingBone)
    {
        // Check for NaN in localPosition
        if (!float.IsNaN(baseBone.localPosition.x) && !float.IsNaN(baseBone.localPosition.y) && !float.IsNaN(baseBone.localPosition.z))
        {
            clothingBone.localPosition = baseBone.localPosition;
        }

        // Check for NaN in localRotation components
        if (!float.IsNaN(baseBone.localRotation.w) && !float.IsNaN(baseBone.localRotation.x) && !float.IsNaN(baseBone.localRotation.y) && !float.IsNaN(baseBone.localRotation.z))
        {
            clothingBone.localRotation = baseBone.localRotation;
        }

        // Check for NaN in localScale
        if (!float.IsNaN(baseBone.localScale.x) && !float.IsNaN(baseBone.localScale.y) && !float.IsNaN(baseBone.localScale.z))
        {
            clothingBone.localScale = baseBone.localScale;
        }

        for (int i = 0; i < baseBone.childCount; i++)
        {
            var baseChild = baseBone.GetChild(i);
            var clothingChild = clothingBone.Find(baseChild.name);
            if (clothingChild != null)
            {
                SyncBoneTransforms(baseChild, clothingChild);
            }
        }
    }
}
