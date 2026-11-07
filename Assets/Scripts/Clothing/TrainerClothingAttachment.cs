using UnityEngine;

/// <summary>Identifies an isolated branch and its owner, including after Instantiate.</summary>
[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class TrainerClothingAttachment : MonoBehaviour
{
    [SerializeField, HideInInspector] private TrainerClothingInstance owner;
    public TrainerClothingInstance Owner => owner;
    internal void Initialize(TrainerClothingInstance value) => owner = value;
}
