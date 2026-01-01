using UnityEngine;

/// <summary>
/// Interface for modular HP bar components.
/// Modules can be local-only or networked (synced with all players).
/// </summary>
public interface IHPBarModule
{
    /// <summary>
    /// The RectTransform of this module's UI element.
    /// </summary>
    RectTransform RectTransform { get; }

    /// <summary>
    /// Whether this module requires network synchronization.
    /// </summary>
    bool IsNetworked { get; }

    /// <summary>
    /// Whether this module is currently active and visible.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Unique identifier for this module instance.
    /// </summary>
    string ModuleId { get; }

    /// <summary>
    /// Initialize the module with the associated Pokemon.
    /// </summary>
    /// <param name="pokemon">The Pokemon this HP bar belongs to.</param>
    /// <param name="isOwner">Whether the local player owns this Pokemon.</param>
    void Initialize(Pokemon pokemon, bool isOwner);

    /// <summary>
    /// Called when the module should update its UI state.
    /// </summary>
    void UpdateUI();

    /// <summary>
    /// Set the visibility of this module.
    /// </summary>
    /// <param name="visible">Whether the module should be visible.</param>
    void SetVisibility(bool visible);

    /// <summary>
    /// Clean up resources when the module is removed.
    /// </summary>
    void Cleanup();
}
