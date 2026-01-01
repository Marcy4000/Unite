using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Base class for networked HP bar modules.
/// These modules sync their data across all clients.
/// </summary>
/// <typeparam name="T">The type of data to sync (must be unmanaged for NetworkVariable).</typeparam>
public abstract class NetworkedHPBarModule<T> : NetworkBehaviour, IHPBarModule where T : unmanaged
{
    [SerializeField] protected string moduleId;
    [SerializeField] protected RectTransform moduleRectTransform;

    protected Pokemon pokemon;
    protected bool isOwnerPlayer;
    protected bool isInitialized;

    /// <summary>
    /// The networked value that syncs across all clients.
    /// Override write permissions in derived classes if needed.
    /// </summary>
    protected NetworkVariable<T> syncedValue;

    public virtual RectTransform RectTransform => moduleRectTransform != null ? moduleRectTransform : GetComponent<RectTransform>();
    public virtual bool IsNetworked => true;
    public virtual bool IsActive => gameObject.activeSelf;
    public virtual string ModuleId => string.IsNullOrEmpty(moduleId) ? GetType().Name : moduleId;

    /// <summary>
    /// The current synced value.
    /// </summary>
    public T Value => syncedValue.Value;

    protected virtual void Awake()
    {
        // Default: Server can write, everyone can read
        syncedValue = new NetworkVariable<T>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        syncedValue.OnValueChanged += OnSyncedValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        syncedValue.OnValueChanged -= OnSyncedValueChanged;
        base.OnNetworkDespawn();
    }

    public virtual void Initialize(Pokemon pokemon, bool isOwner)
    {
        this.pokemon = pokemon;
        this.isOwnerPlayer = isOwner;
        isInitialized = true;
        SubscribeToEvents();
        UpdateUI();
    }

    /// <summary>
    /// Called when the networked value changes on any client.
    /// </summary>
    protected virtual void OnSyncedValueChanged(T previousValue, T newValue)
    {
        UpdateUI();
    }

    /// <summary>
    /// Set the synced value. Only works if this client has write permission.
    /// </summary>
    protected void SetValue(T value)
    {
        if (IsServer || (syncedValue.WritePerm == NetworkVariableWritePermission.Owner && IsOwner))
        {
            syncedValue.Value = value;
        }
        else
        {
            SetValueServerRpc(value);
        }
    }

    [Rpc(SendTo.Server)]
    private void SetValueServerRpc(T value)
    {
        syncedValue.Value = value;
    }

    /// <summary>
    /// Override to subscribe to Pokemon events.
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

    public override void OnDestroy()
    {
        if (isInitialized)
        {
            Cleanup();
        }
        base.OnDestroy();
    }
}

/// <summary>
/// Networked module with owner write permission.
/// Useful for modules that the owning player controls (e.g., ability charges).
/// </summary>
public abstract class OwnerWriteNetworkedModule<T> : NetworkedHPBarModule<T> where T : unmanaged
{
    protected override void Awake()
    {
        syncedValue = new NetworkVariable<T>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );
    }
}
