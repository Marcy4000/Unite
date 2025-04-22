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

    [SerializeField] private Color32[] skinColors; 

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

        yield return null;

        loadingText.SetActive(true);

        playerClothesInfo = info;
        SetGender(info.IsMale);

        Debug.Log($"Initializing clothes for player. Serialized info: {info.Serialize()}");

        // Clear existing clothes
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
                    Addressables.ReleaseInstance(child.gameObject);

                    if (child != null)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        // List to hold bones and instantiated clothes
        var bonesToSync = new List<Transform>(10);  // Pre-allocate for expected size
        var instantiatedClothes = new List<GameObject>(10);  // Pre-allocate for expected size

        // Load and instantiate new clothes
        for (int i = 0; i < clothingHolders.Length; i++)
        {
            var holder = clothingHolders[i];

            if (holder == null)
            {
                Debug.LogError($"Clothing holder at index {i} is null. Skipping.");
                continue;
            }

            int clothingIndex = (ClothingType)i == ClothingType.Eyes ? info.GetClothingIndex(ClothingType.Face) : info.GetClothingIndex((ClothingType)i);

            var item = ClothesList.Instance.GetClothingItem((ClothingType)i, clothingIndex, info.IsMale);

            int modelIndex = 0;

            if (item.clothingType == ClothingType.Shirt && item.prefabs.Count > 1)
            {
                modelIndex = info.GetClothingIndex(ClothingType.Overwear) != 0 ? 0 : 1;
            }
            else if (item.clothingType == ClothingType.Socks)
            {
                var pants = ClothesList.Instance.GetClothingItem(ClothingType.Pants, info.GetClothingIndex(ClothingType.Pants), info.IsMale);

                modelIndex = (pants != null && !ArePantsDisabled(info)) ? Mathf.Min(item.prefabs.Count - 1, pants.sockTypeToUse) : Mathf.Min(item.prefabs.Count - 1, 2);
            }

            if (item == null || item.prefabs.Count <= modelIndex || !item.prefabs[modelIndex].RuntimeKeyIsValid())
            {
                continue;
            }

            // Instantiate the clothing item asynchronously
            var handle = Addressables.InstantiateAsync(item.prefabs[modelIndex], holder);

            handle.Completed += (op) =>
            {
                var result = op.Result;
                result.SetActive(false);
            };

            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                var result = handle.Result;
                //Debug.Log($"Successfully loaded clothing item of type {(ClothingType)i}.");

                result.transform.SetParent(holder, false);
                UpdateObjectLayer(result, holder.gameObject.layer);
                bonesToSync.Add(GetChildToSync(result.transform));
                instantiatedClothes.Add(result);

                SkinnedMeshRenderer[] meshRenderers = result.GetComponentsInChildren<SkinnedMeshRenderer>();

                foreach (var meshRenderer in meshRenderers)
                {
                    foreach (var material in meshRenderer.materials)
                    {
                        if (material.name.Contains("body_yellow") || material.name.Contains("head_yellow") || material.name.Contains("ow_yellow") || material.name.Contains("0000hand"))
                        {
                            material.SetColor("_BaseColor", skinColors[info.SkinColor % skinColors.Length]);
                        }
                        else if (material.name.Contains("hair") || material.name.Contains("00000shadow"))
                        {
                            material.SetColor("_BaseColor", info.HairColor);
                        }
                        else if (material.name.Contains("eye00"))
                        {
                            material.SetColor("_BaseColor", info.EyeColor);
                        }

                        material.SetFloat("_Metallic", 0.0f);
                        material.SetFloat("_Smoothness", 0.1f);
                    }
                }

                result.SetActive(false);  // Start inactive until bones are synced
            }
            else
            {
                Debug.LogError($"Failed to load clothing item: {(ClothingType)i}");
            }
        }

        // Set synced bones
        if (activeBoneSync != null)
        {
            activeBoneSync.clothingRoots = bonesToSync.ToArray();
        }
        else
        {
            Debug.LogError("ActiveBoneSync is null. Cannot set clothing roots.");
        }

        // Activate instantiated clothes
        foreach (var go in instantiatedClothes)
        {
            if (go != null)
            {
                go.SetActive(true);
            }
            else
            {
                Debug.LogWarning("An instantiated clothing item is null.");
            }
        }

        // Disable clothing that are disabled by the current clothing
        for (int i = 0; i < instantiatedClothes.Count; i++)
        {
            var item = ClothesList.Instance.GetClothingItem((ClothingType)i, info.GetClothingIndex((ClothingType)i), info.IsMale);

            if (item == null)
            {
                continue;
            }

            foreach (var disableType in item.disablesClothingType)
            {
                int index = (int)disableType;
                if (index >= 0 && index < clothingHolders.Length)
                {
                    foreach (Transform child in clothingHolders[index])
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }
        }

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

                SkinnedMeshRenderer[] meshRenderers = child.GetComponentsInChildren<SkinnedMeshRenderer>();

                foreach (var meshRenderer in meshRenderers)
                {
                    foreach (var material in meshRenderer.materials)
                    {
                        if (material.name.Contains("body_yellow") || material.name.Contains("head_yellow") || material.name.Contains("ow_yellow") || material.name.Contains("0000hand"))
                        {
                            material.SetColor("_BaseColor", skinColors[info.SkinColor % skinColors.Length]);
                        }
                        else if (material.name.Contains("hair") || material.name.Contains("00000shadow"))
                        {
                            material.SetColor("_BaseColor", info.HairColor);
                        }
                        else if (material.name.Contains("eye00"))
                        {
                            material.SetColor("_BaseColor", info.EyeColor);
                        }

                        material.SetFloat("_Metallic", 0.0f);
                        material.SetFloat("_Smoothness", 0.1f);
                    }
                }
            }
        }
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
        foreach (var holder in clothingHolders)
        {
            if (holder == null)
            {
                continue;
            }

            foreach (Transform child in holder)
            {
                if (child != null)
                {
                    Addressables.ReleaseInstance(child.gameObject);
                    
                    if (child != null)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }
    }
}
