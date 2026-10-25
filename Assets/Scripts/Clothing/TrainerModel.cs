using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Player = Unite.Multiplayer.GameSessionPlayer;
using Lobby = Unite.Multiplayer.GameSessionView;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class TrainerModel : MonoBehaviour
{
    [SerializeField] private Animator maleAnimator;
    [SerializeField] private Animator femaleAnimator;
    [SerializeField] private BoneSync maleBoneSync;
    [SerializeField] private BoneSync femaleBoneSync;
    [SerializeField] private GameObject loadingText;
    [SerializeField] private bool isMale;
    [SerializeField] private Transform[] clothingHoldersMale;
    [SerializeField] private Transform[] clothingHoldersFemale;
    [SerializeField, ColorUsage(false, true)] private Color[] skinColors;
    [SerializeField, ColorUsage(false, true)] private Color magicColor;

    private Transform[] clothingHolders => isMale ? clothingHoldersMale : clothingHoldersFemale;
    private Animator activeAnimator => isMale ? maleAnimator : femaleAnimator;
    private BoneSync activeBoneSync;
    private string assignedPlayer = "";
    private PlayerClothesInfo lastPlayerClothesInfo;
    private PlayerClothesInfo playerClothesInfo;
    private PlayerClothesInfo requestedInfo;
    private bool initialized;
    private bool initializing;
    private bool hasPendingRequest;
    private GameObject stagingRoot;
    // Copied with a runtime trainer by UIObject3D, but false in authored prefabs.
    [SerializeField, HideInInspector] private bool hasCommittedOutfit;

    private sealed class PendingClothing
    {
        public int Slot;
        public TrainerOutfitResolver.Selection Selection;
        public AsyncOperationHandle<GameObject> Handle;
        public TrainerClothingInstance Instance;
    }

    private readonly List<PendingClothing> pendingLoads = new List<PendingClothing>();
    private static readonly ProfilerMarker ResolveMarker = new ProfilerMarker("TrainerClothes.Resolve");
    private static readonly ProfilerMarker InstantiateMarker = new ProfilerMarker("TrainerClothes.Instantiate");
    private static readonly ProfilerMarker CommitMarker = new ProfilerMarker("TrainerClothes.Commit");
    private static readonly ProfilerMarker ColorsMarker = new ProfilerMarker("TrainerClothes.Materials");

    public bool IsMale => isMale;
    public Animator ActiveAnimator => activeAnimator;
    public System.Action onClothesInitialized;
    public PlayerClothesInfo PlayerClothesInfo => playerClothesInfo;
    public bool IsInitialized => initialized;
    public bool IsLoading => initializing || hasPendingRequest;

    private void Awake()
    {
        HideEditorPreview(clothingHoldersMale);
        HideEditorPreview(clothingHoldersFemale);
        if (loadingText != null) loadingText.SetActive(false);
    }

    private static void HideEditorPreview(Transform[] holders)
    {
        if (holders == null) return;
        foreach (var holder in holders)
        {
            if (holder == null) continue;
            foreach (Transform child in holder)
            {
                // Keep loaded garments when cloning a trainer. Disable renderers,
                // not GameObjects: UIObject3D still needs the preview mesh bounds.
                if (child.GetComponent<TrainerClothingInstance>() != null) continue;
                foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = false;
            }
        }
    }

    private void OnEnable()
    {
        if (LobbyController.Instance != null)
            LobbyController.Instance.onLobbyUpdate += OnLobbyUpdate;
        BeginPendingRequest();
    }

    private void OnDisable()
    {
        if (LobbyController.Instance != null)
            LobbyController.Instance.onLobbyUpdate -= OnLobbyUpdate;
        // A disabled GameObject stops its coroutines. Retain the latest request so
        // re-enabling resumes it instead of leaving the trainer permanently loading.
        if (initializing) hasPendingRequest = true;
        StopAllCoroutines();
        ReleasePendingLoads();
        initializing = false;
        if (loadingText != null) loadingText.SetActive(false);
    }

    public void InitializeClothes(PlayerClothesInfo info)
    {
        // Callers must not mistake the previous visible outfit for a completed
        // new request during the coroutine's initial coalescing frame.
        initialized = false;
        requestedInfo = info;
        playerClothesInfo = info;
        hasPendingRequest = true;
        BeginPendingRequest();
    }

    private void BeginPendingRequest()
    {
        if (initializing || !hasPendingRequest || !isActiveAndEnabled) return;
        initializing = true;
        if (loadingText != null) loadingText.SetActive(!hasCommittedOutfit);
        StartCoroutine(InitializeRoutine());
    }

    private IEnumerator InitializeRoutine()
    {
        // Coalesce multiple UI/lobby notifications received during the same frame.
        yield return null;
        while (hasPendingRequest)
        {
            var info = requestedInfo;
            hasPendingRequest = false;
            initialized = false;
            if (loadingText != null) loadingText.SetActive(!hasCommittedOutfit);

            var holders = info.IsMale ? clothingHoldersMale : clothingHoldersFemale;
            TrainerOutfitResolver.Selection[] outfit;
            using (ResolveMarker.Auto())
            {
                if (ClothesList.Instance == null || holders == null)
                {
                    Debug.LogError("Cannot initialize trainer clothes without a ClothesList and clothing holders.", this);
                    break;
                }
                outfit = TrainerOutfitResolver.Resolve(ClothesList.Instance, info, holders.Length);
            }

            if (!StartLoads(holders, outfit))
            {
                ReleasePendingLoads();
                continue;
            }

            // All requests have already started; these waits do not serialize I/O.
            foreach (var load in pendingLoads)
                yield return load.Handle;

            if (hasPendingRequest && !requestedInfo.Equals(info))
            {
                ReleasePendingLoads();
                continue;
            }
            hasPendingRequest = false;

            // Color-picker changes do not require another geometry request.
            // Equals intentionally ignores trainer-card metadata; keep that latest too.
            info = requestedInfo;
            bool succeeded = TryInstantiateAndCommit(holders, outfit, info);
            ReleasePendingLoads();
            if (succeeded)
            {
                initialized = true;
                hasCommittedOutfit = true;
                if (loadingText != null) loadingText.SetActive(false);
                try
                {
                    onClothesInitialized?.Invoke();
                }
                catch (System.Exception exception)
                {
                    // A consumer callback must not leave the request queue stuck.
                    Debug.LogException(exception, this);
                }
                if (!isActiveAndEnabled) yield break;
            }
        }

        initializing = false;
        if (loadingText != null) loadingText.SetActive(false);
    }

    private bool StartLoads(Transform[] holders, TrainerOutfitResolver.Selection[] outfit)
    {
        try
        {
            for (int i = 0; i < outfit.Length; i++)
            {
                var selection = outfit[i];
                if (selection.Item == null) continue;
                if (holders[i] == null)
                {
                    Debug.LogError($"Clothing holder at index {i} is null.", this);
                    return false;
                }
                if (FindMatchingInstance(holders[i], selection.RuntimeKey) != null) continue;

                pendingLoads.Add(new PendingClothing
                {
                    Slot = i,
                    Selection = selection,
                    Handle = Addressables.LoadAssetAsync<GameObject>(selection.RuntimeKey)
                });
            }
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }

    private bool TryInstantiateAndCommit(Transform[] holders, TrainerOutfitResolver.Selection[] outfit, PlayerClothesInfo info)
    {
        foreach (var load in pendingLoads)
        {
            if (load.Handle.Status != AsyncOperationStatus.Succeeded || load.Handle.Result == null)
            {
                Debug.LogError($"Failed to load clothing {load.Selection.Item.name}: {load.Handle.OperationException}", this);
                return false; // Leave the previous outfit intact on failure.
            }
        }

        try
        {
            using (InstantiateMarker.Auto())
            {
                if (pendingLoads.Count > 0)
                {
                    stagingRoot = new GameObject("Clothing staging");
                    stagingRoot.SetActive(false);
                    stagingRoot.transform.SetParent(transform, false);
                }
                foreach (var load in pendingLoads)
                {
                    var instance = Instantiate(load.Handle.Result, stagingRoot.transform, false);
                    instance.SetActive(false);
                    load.Instance = instance.AddComponent<TrainerClothingInstance>();
                    load.Instance.Initialize(load.Selection.RuntimeKey, load.Handle);
                    load.Handle = default; // The instance now owns this reference.
                    UpdateObjectLayer(instance, holders[load.Slot].gameObject.layer);
                }
            }

            using (CommitMarker.Auto())
            {
                // The old outfit stays alive until replacement assets and instances
                // exist, avoiding unload/reload churn for their shared dependencies.
                var instances = new TrainerClothingInstance[outfit.Length];
                for (int i = 0; i < outfit.Length; i++)
                {
                    if (outfit[i].Item != null)
                        instances[i] = FindMatchingInstance(holders[i], outfit[i].RuntimeKey);
                }
                foreach (var load in pendingLoads) instances[load.Slot] = load.Instance;

                ReleaseObsoleteInstances(clothingHoldersMale, info.IsMale ? instances : null);
                ReleaseObsoleteInstances(clothingHoldersFemale, info.IsMale ? null : instances);
                SetGender(info.IsMale);

                var bones = new List<Transform>(instances.Length);
                for (int i = 0; i < instances.Length; i++)
                {
                    var instance = instances[i];
                    if (instance == null) continue;
                    instance.transform.SetParent(holders[i], false);
                    bones.Add(GetChildToSync(instance.transform));
                }
                UpdateMaterialColors(info);
                if (activeBoneSync != null)
                {
                    var roots = activeBoneSync.clothingRoots;
                    bool changed = roots == null || roots.Length != bones.Count;
                    for (int i = 0; !changed && i < bones.Count; i++) changed = roots[i] != bones[i];
                    if (changed)
                    {
                        activeBoneSync.clothingRoots = bones.ToArray();
                        activeBoneSync.RebuildSyncData();
                    }
                    activeBoneSync.ForceSync();
                }
                foreach (var instance in instances)
                {
                    if (instance != null) instance.gameObject.SetActive(true);
                }
                foreach (var load in pendingLoads) load.Instance = null; // Commit ownership to the hierarchy.
            }
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }

    private static TrainerClothingInstance FindMatchingInstance(Transform holder, string key)
    {
        if (holder == null) return null;
        foreach (Transform child in holder)
        {
            var instance = child.GetComponent<TrainerClothingInstance>();
            if (instance != null && instance.RuntimeKey == key) return instance;
        }
        return null;
    }

    private void ReleaseObsoleteInstances(Transform[] holders, TrainerClothingInstance[] keep)
    {
        if (holders == null) return;
        for (int i = 0; i < holders.Length; i++)
        {
            var holder = holders[i];
            if (holder == null) continue;
            for (int j = holder.childCount - 1; j >= 0; j--)
            {
                var child = holder.GetChild(j);
                if (keep != null && i < keep.Length && keep[i] != null && child == keep[i].transform) continue;
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                var owned = child.GetComponent<TrainerClothingInstance>();
                if (owned != null)
                {
                    owned.ReleaseResources();
                    Destroy(child.gameObject);
                }
                else if (!Addressables.ReleaseInstance(child.gameObject))
                    Destroy(child.gameObject);
            }
        }
    }

    private void ReleasePendingLoads()
    {
        foreach (var load in pendingLoads)
        {
            if (load.Handle.IsValid()) Addressables.Release(load.Handle);
            if (load.Instance != null)
            {
                // Inactive staged objects may never receive OnDestroy.
                load.Instance.ReleaseResources();
                Destroy(load.Instance.gameObject);
            }
        }
        pendingLoads.Clear();
        if (stagingRoot != null) Destroy(stagingRoot);
        stagingRoot = null;
    }

    public void UpdateMaterialColors(PlayerClothesInfo info)
    {
        playerClothesInfo = info;
        if (initializing)
        {
            requestedInfo.HairColor = info.HairColor;
            requestedInfo.EyeColor = info.EyeColor;
            requestedInfo.SkinColor = info.SkinColor;
        }
        using (ColorsMarker.Auto())
        {
            Color skin = skinColors != null && skinColors.Length > 0 ? skinColors[info.SkinColor % skinColors.Length] : Color.white;
            var holders = clothingHolders;
            if (holders == null) return;
            foreach (var holder in holders)
            {
                if (holder == null) continue;
                foreach (Transform child in holder)
                {
                    var instance = child.GetComponent<TrainerClothingInstance>();
                    if (instance != null) instance.ApplyColors(info, skin, magicColor);
                }
            }
        }
    }

    public Transform GetChildToSync(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child.name == "reference") return child;
        }
        return parent.childCount > 0 ? parent.GetChild(0) : parent;
    }

    private void UpdateObjectLayer(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform) UpdateObjectLayer(child.gameObject, layer);
    }

    public void SetGender(bool isMale)
    {
        this.isMale = isMale;
        if (maleBoneSync != null) maleBoneSync.gameObject.SetActive(isMale);
        if (femaleBoneSync != null) femaleBoneSync.gameObject.SetActive(!isMale);
        activeBoneSync = isMale ? maleBoneSync : femaleBoneSync;
    }

    public void AssignPlayer(string playerID) => assignedPlayer = playerID;

    private void OnLobbyUpdate(Lobby lobby)
    {
        if (lobby == null || string.IsNullOrEmpty(assignedPlayer)) return;
        Player player = lobby.Players.FirstOrDefault(p => p.Id == assignedPlayer);
        if (player != null && player.Data.TryGetValue("ClothingInfo", out var clothingData))
        {
            var info = PlayerClothesInfo.Deserialize(clothingData.Value);
            if (!info.Equals(lastPlayerClothesInfo))
            {
                lastPlayerClothesInfo = info;
                InitializeClothes(info);
            }
        }
    }

    private void OnDestroy()
    {
        if (LobbyController.Instance != null)
            LobbyController.Instance.onLobbyUpdate -= OnLobbyUpdate;
        ReleasePendingLoads();
        // Each garment releases its own lease/materials when the hierarchy dies.
    }
}
