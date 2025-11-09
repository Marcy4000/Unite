using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Lobbies.Models;
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

    
    [SerializeField][ColorUsage(false, true)] private Color[] skinColors;
    [SerializeField][ColorUsage(false, true)] private Color magicColor;

    private Transform[] clothingHolders => isMale ? clothingHoldersMale : clothingHoldersFemale;
    private Animator activeAnimator => isMale ? maleAnimator : femaleAnimator;

    private BoneSync activeBoneSync;

    private string assignedPlayer = "";
    private PlayerClothesInfo lastPlayerClothesInfo = new PlayerClothesInfo();
    private PlayerClothesInfo playerClothesInfo = new PlayerClothesInfo();

    private bool initialized = false;
    private bool initializing = false;

    public bool IsMale => isMale;
    public Animator ActiveAnimator => activeAnimator;

    public System.Action onClothesInitialized;

    public PlayerClothesInfo PlayerClothesInfo => playerClothesInfo;

    public bool IsInitialized => initialized;

    private void OnEnable()
    {
        if (LobbyController.Instance != null)
        {
            LobbyController.Instance.onLobbyUpdate += OnLobbyUpdate;
        }
    }

    private void OnDisable()
    {
        if (LobbyController.Instance != null)
        {
            LobbyController.Instance.onLobbyUpdate -= OnLobbyUpdate;
        }
        initializing = false;
    }

    public void InitializeClothes(PlayerClothesInfo info)
    {
        if (initializing)
        {
            return;
        }

        StartCoroutine(InitializeRoutine(info));
    }

    private IEnumerator InitializeRoutine(PlayerClothesInfo info)
    {
        initialized = false;
        initializing = true;

        yield return null; // Allow one frame for setup
        if (this == null) yield break;

        loadingText.SetActive(true);

        playerClothesInfo = info;
        SetGender(info.IsMale);
        if (this == null) yield break;

        Debug.Log($"Initializing clothes for player. Serialized info: {info.Serialize()}");

        // --- 1. Clear existing clothes ---
        List<GameObject> clothesToDestroy = new List<GameObject>();
        foreach (var holder in clothingHolders)
        {
            if (holder == null)
            {
                Debug.LogError("Clothing holder is null. Check inspector assignments.");
                continue;
            }
            foreach (Transform child in holder)
            {
                if (child != null)
                {
                    clothesToDestroy.Add(child.gameObject);
                }
            }
        }

        foreach (var go in clothesToDestroy)
        {
            Addressables.ReleaseInstance(go);
            Destroy(go);
        }
        yield return null;
        if (this == null) yield break;

        // --- 2. Start loading new clothes concurrently ---
        var loadHandles = new List<AsyncOperationHandle<GameObject>>();
        var loadInfoList = new List<(int holderIndex, ClothingItem item, int modelIndex)>();

        for (int i = 0; i < clothingHolders.Length; i++)
        {
            var holder = clothingHolders[i];
            if (holder == null)
            {
                Debug.LogError($"Clothing holder at index {i} is null. Skipping.");
                continue;
            }

            ClothingType currentType = (ClothingType)i;
            int clothingIndex = currentType == ClothingType.Eyes ? info.GetClothingIndex(ClothingType.Face) : info.GetClothingIndex(currentType);
            var item = ClothesList.Instance.GetClothingItem(currentType, clothingIndex, info.IsMale);

            if (item == null) continue;

            int modelIndex = 0;
            if (item.clothingType == ClothingType.Shirt && item.prefabs.Count > 1)
            {
                modelIndex = info.GetClothingIndex(ClothingType.Overwear) != 0 ? 0 : (item.prefabs.Count > 1 ? 1 : 0);
            }
            else if (item.clothingType == ClothingType.Socks)
            {
                var pantsItem = ClothesList.Instance.GetClothingItem(ClothingType.Pants, info.GetClothingIndex(ClothingType.Pants), info.IsMale);
                int sockTypeBasedOnPants = (pantsItem != null && !ArePantsDisabled(info)) ? pantsItem.sockTypeToUse : 2;
                modelIndex = Mathf.Min(item.prefabs.Count - 1, sockTypeBasedOnPants);
            }

            if (item.prefabs.Count <= modelIndex || !item.prefabs[modelIndex].RuntimeKeyIsValid())
            {
                continue;
            }

            var handle = Addressables.InstantiateAsync(item.prefabs[modelIndex], holder);
            
            // Add callback to disable the object immediately upon instantiation
            handle.Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
                {
                    op.Result.SetActive(false); // Deactivate as soon as it's loaded
                }
            };

            loadHandles.Add(handle);
            loadInfoList.Add((i, item, modelIndex));
            if (this == null) yield break;
        }

        // --- 3. Wait for all loading operations to complete ---
        foreach (var handle in loadHandles)
        {
            yield return handle;
            if (this == null)
            {
                foreach (var completedHandle in loadHandles)
                {
                    if (completedHandle.IsValid() && completedHandle.Status == AsyncOperationStatus.Succeeded && completedHandle.Result != null)
                    {
                        Addressables.ReleaseInstance(completedHandle.Result);
                    }
                }
                yield break;
            }
        }

        // --- 4. Process loaded clothes ---
        var bonesToSync = new List<Transform>(loadHandles.Count);
        var instantiatedInfo = new List<(GameObject instance, ClothingItem item)>(loadHandles.Count);

        for (int j = 0; j < loadHandles.Count; j++)
        {
            if (this == null)
            {
                foreach (var infoPair in instantiatedInfo)
                {
                    if (infoPair.instance != null) Addressables.ReleaseInstance(infoPair.instance);
                }
                for (int k = j; k < loadHandles.Count; k++)
                {
                    var handle1 = loadHandles[k];
                    if (handle1.IsValid() && handle1.Status == AsyncOperationStatus.Succeeded && handle1.Result != null)
                    {
                        Addressables.ReleaseInstance(handle1.Result);
                    }
                }
                yield break;
            }

            var handle = loadHandles[j];
            var loadInfo = loadInfoList[j];
            var holder = clothingHolders[loadInfo.holderIndex];

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                var result = handle.Result;

                result.transform.SetParent(holder, false);
                UpdateObjectLayer(result, holder.gameObject.layer);

                bonesToSync.Add(GetChildToSync(result.transform));
                instantiatedInfo.Add((result, loadInfo.item));
            }
            else
            {
                Debug.LogError($"Failed to load clothing item for holder index {loadInfo.holderIndex}: {loadInfo.item?.name ?? "Unknown"}. Error: {handle.OperationException}");
            }
        }

        if (this == null)
        {
            foreach (var infoPair in instantiatedInfo)
            {
                if (infoPair.instance != null) Addressables.ReleaseInstance(infoPair.instance);
            }
            yield break;
        }

        // --- Call UpdateMaterialColors after processing all items ---
        UpdateMaterialColors(info);
        if (this == null) yield break;

        // --- 5. Sync bones ---
        if (activeBoneSync != null)
        {
            activeBoneSync.clothingRoots = bonesToSync.ToArray();
        }
        else
        {
            if (this != null) Debug.LogError("ActiveBoneSync is null. Cannot set clothing roots.");
        }
        if (this == null) yield break;

        // --- 6. Determine which clothes should be active ---
        HashSet<GameObject> activeInstances = new HashSet<GameObject>(instantiatedInfo.Select(info => info.instance));

        foreach (var infoPair in instantiatedInfo)
        {
            if (infoPair.instance == null || infoPair.item == null) continue;

            foreach (var disableType in infoPair.item.disablesClothingType)
            {
                foreach (var otherInfo in instantiatedInfo)
                {
                    if (otherInfo.instance != null && otherInfo.item?.clothingType == disableType)
                    {
                        activeInstances.Remove(otherInfo.instance);
                    }
                }
            }
        }
        if (this == null) yield break;

        // --- 7. Activate only the necessary clothes ---
        foreach (var instance in activeInstances)
        {
            if (instance != null)
            {
                instance.SetActive(true);
            }
        }
        if (this == null) yield break;

        // --- 8. Finalize ---
        if (this == null) yield break;

        loadingText.SetActive(false);
        initialized = true;
        initializing = false;
        onClothesInitialized?.Invoke();
    }

    private bool ArePantsDisabled(PlayerClothesInfo info)
    {
        for (int i = 0; i < clothingHolders.Length; i++)
        {
            var item = ClothesList.Instance.GetClothingItem((ClothingType)i, info.GetClothingIndex((ClothingType)i), info.IsMale);

            if (item == null)
            {
                continue;
            }

            foreach (var disableType in item.disablesClothingType)
            {
                if (disableType == ClothingType.Pants)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void UpdateMaterialColors(PlayerClothesInfo info)
    {
        playerClothesInfo = info;

        for (int i = 0; i < clothingHolders.Length; i++)
        {
            var holder = clothingHolders[i];

            if (holder == null)
            {
                Debug.LogError($"Clothing holder at index {i} is null. Skipping.");
                continue;
            }

            foreach (Transform child in holder)
            {
                if (child == null)
                {
                    Debug.LogError("Child transform is null. Skipping.");
                    continue;
                }

                SkinnedMeshRenderer[] meshRenderers = child.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                foreach (var meshRenderer in meshRenderers)
                {
                    Material[] materials = meshRenderer.materials;
                    for (int matIndex = 0; matIndex < materials.Length; matIndex++)
                    {
                        var material = materials[matIndex];
                        if (material == null) continue;

                        string matName = material.name.ToLowerInvariant();

                        if (matName.Contains("body") || matName.Contains("head") || matName.Contains("000hand"))
                        {
                            material.SetColor("_colorSkin", skinColors[info.SkinColor % skinColors.Length]);
                            material.SetColor("_SHTopColor", skinColors[info.SkinColor % skinColors.Length]);
                            material.SetColor("_SHBotColor", magicColor);

                            material.SetFloat("_AtVector", 0f);
                            material.SetFloat("_SHScale", 2.21f);
                            material.SetFloat("_Emissive", 0.157f);
                        }
                        else if (matName.Contains("hair") || matName.Contains("shadow"))
                        {
                            Color hairCol = ConvertColor32ToHDR(info.HairColor, 1f);
                            material.SetColor("_color1", hairCol);
                            material.SetColor("_color2", hairCol);
                        }
                        else if (matName.Contains("eye0"))
                        {
                            material.SetColor("_color1", info.EyeColor);
                            material.SetColor("_color2", info.EyeColor);
                        }
                    }
                }
            }
        }
    }

    private Color ConvertColor32ToHDR(Color32 c, float intensity = 1f)
    {
        float r = c.r / 255f;
        float g = c.g / 255f;
        float b = c.b / 255f;
        float a = c.a / 255f;

        return new Color(r * intensity, g * intensity, b * intensity, a);
    }

    public Transform GetChildToSync(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child.name == "reference")
            {
                return child;
            }
        }
        return parent.childCount > 0 ? parent.GetChild(0) : parent;
    }

    private void UpdateObjectLayer(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            UpdateObjectLayer(child.gameObject, layer);
        }
    }

    public void SetGender(bool isMale)
    {
        this.isMale = isMale;
        maleBoneSync.gameObject.SetActive(IsMale);
        femaleBoneSync.gameObject.SetActive(!IsMale);
        activeBoneSync = isMale ? maleBoneSync : femaleBoneSync;
    }

    public void AssignPlayer(string playerID)
    {
        assignedPlayer = playerID;
    }

    private void OnLobbyUpdate(Lobby lobby)
    {
        if (lobby == null || string.IsNullOrEmpty(assignedPlayer))
        {
            return;
        }

        Player player = lobby.Players.FirstOrDefault(p => p.Id == assignedPlayer);

        if (player != null && player.Data.TryGetValue("ClothingInfo", out var clothingData))
        {
            var playerClothesInfo = PlayerClothesInfo.Deserialize(clothingData.Value);

            if (!playerClothesInfo.Equals(lastPlayerClothesInfo))
            {
                lastPlayerClothesInfo = playerClothesInfo;
                InitializeClothes(playerClothesInfo);
            }
        }
    }

    private void OnDestroy()
    {
        initializing = false;

        if (LobbyController.Instance != null)
        {
            LobbyController.Instance.onLobbyUpdate -= OnLobbyUpdate;
        }

        if (clothingHoldersMale != null)
        {
            foreach (var holder in clothingHoldersMale)
            {
                ReleaseHolderInstances(holder);
            }
        }
        if (clothingHoldersFemale != null)
        {
            foreach (var holder in clothingHoldersFemale)
            {
                ReleaseHolderInstances(holder);
            }
        }
    }

    private void ReleaseHolderInstances(Transform holder)
    {
        if (holder == null) return;

        for (int i = holder.childCount - 1; i >= 0; i--)
        {
            Transform child = holder.GetChild(i);
            if (child != null)
            {
                Addressables.ReleaseInstance(child.gameObject);
                Destroy(child.gameObject);
            }
        }
    }
}
