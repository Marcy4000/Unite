using UnityEngine;

/// <summary>
/// Base class for local-only HP bar modules.
/// These modules don't sync data across the network.
/// </summary>
public abstract class HPBarModuleBase : MonoBehaviour, IHPBarModule
{
    [SerializeField] protected string moduleId;
    [SerializeField] protected RectTransform moduleRectTransform;

    protected Pokemon pokemon;
    protected bool isOwner;
    protected bool isInitialized;

    public virtual RectTransform RectTransform => moduleRectTransform != null ? moduleRectTransform : GetComponent<RectTransform>();
    public virtual bool IsNetworked => false;
    public virtual bool IsActive => gameObject.activeSelf;
    public virtual string ModuleId => string.IsNullOrEmpty(moduleId) ? GetType().Name : moduleId;

    public virtual void Initialize(Pokemon pokemon, bool isOwner)
    {
        this.pokemon = pokemon;
        this.isOwner = isOwner;
        isInitialized = true;
        SubscribeToEvents();
        UpdateUI();
    }

    /// <summary>
    /// Override to subscribe to Pokemon events (OnHpChange, OnLevelChange, etc.)
    /// </summary>
    protected virtual void SubscribeToEvents() { }

    /// <summary>
    /// Override to unsubscribe from Pokemon events.
    /// </summary>
    protected virtual void UnsubscribeFromEvents() { }

    public abstract void UpdateUI();

    public virtual void SetVisibility(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public virtual void Cleanup()
    {
        UnsubscribeFromEvents();
        isInitialized = false;
    }

    protected virtual void OnDestroy()
    {
        if (isInitialized)
        {
            Cleanup();
        }
    }
}
