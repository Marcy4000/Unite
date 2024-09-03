using System.Collections;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using Unity.Services.Lobbies;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Linq;

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

    private Transform[] clothingHolders => isMale ? clothingHoldersMale : clothingHoldersFemale;
    private Animator activeAnimator => isMale ? maleAnimator : femaleAnimator;

    private BoneSync activeBoneSync;

    public bool IsMale => isMale;
    public Animator ActiveAnimator => activeAnimator;

    private string assignedPlayer = "";
    private PlayerClothesInfo lastPlayerClothesInfo = new PlayerClothesInfo();
    private PlayerClothesInfo playerClothesInfo = new PlayerClothesInfo();

    private void OnEnable()
    {
        LobbyController.Instance.onLobbyUpdate += OnLobbyUpdate;
    }

    private void OnDisable()
    {
        LobbyController.Instance.onLobbyUpdate -= OnLobbyUpdate;
    }

    public void InitializeClothes(PlayerClothesInfo info)
    {
        StartCoroutine(InitializeRoutine(info));
    }

    private IEnumerator InitializeRoutine(PlayerClothesInfo info)
    {
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

            var item = ClothesList.Instance.GetClothingItem((ClothingType)i, info.GetClothingIndex((ClothingType)i), info.IsMale);

            if (item == null || !item.prefab.RuntimeKeyIsValid())
            {
                Debug.LogWarning(item == null
                    ? $"Clothing item of type {(ClothingType)i} not found."
                    : $"Clothing item prefab is not set for type {(ClothingType)i}.");
                continue;
            }

            // Instantiate the clothing item asynchronously
            var handle = Addressables.InstantiateAsync(item.prefab, holder);

            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                var result = handle.Result;
                Debug.Log($"Successfully loaded clothing item of type {(ClothingType)i}.");

                result.transform.SetParent(holder, false);
                UpdateObjectLayer(result, holder.gameObject.layer);
                bonesToSync.Add(GetChildToSync(result.transform));
                instantiatedClothes.Add(result);
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

        loadingText.SetActive(false);
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
