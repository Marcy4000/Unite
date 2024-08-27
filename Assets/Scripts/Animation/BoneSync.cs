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
        clothingBone.localPosition = baseBone.localPosition;
        clothingBone.localRotation = baseBone.localRotation;
        clothingBone.localScale = baseBone.localScale;

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
