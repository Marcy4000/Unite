using UnityEngine;

public class BushCollider : MonoBehaviour
{
    [SerializeField] private GameObject bushRoot; // Optional field to set in the inspector
    public GameObject BushRoot => bushRoot;

    private void Awake()
    {
        bushRoot = bushRoot != null ? bushRoot : transform.parent?.gameObject;

        if (bushRoot == null)
            Debug.LogWarning($"BushCollider on {name} has no parent bush root or assigned bush root!");
    }
}
