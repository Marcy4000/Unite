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
    [SerializeField] private bool isMale;

    [SerializeField] private Transform[] clothingHoldersMale;
    [SerializeField] private Transform[] clothingHoldersFemale;

    private Transform[] clothingHolders { get { return isMale ? clothingHoldersMale : clothingHoldersFemale; } }
    private Animator activeAnimator => isMale ? maleAnimator : femaleAnimator;

    private BoneSync activeBoneSync;

    public bool IsMale => isMale;
    public Animator ActiveAnimator => activeAnimator;

    private string assignedPlayer = "";
    private PlayerClothesInfo lastPlayerClothesInfo = new PlayerClothesInfo();
    private PlayerClothesInfo playerClothesInfo = new PlayerClothesInfo();

    private void Start()
    {
        maleBoneSync.gameObject.SetActive(IsMale);
        femaleBoneSync.gameObject.SetActive(!IsMale);

        activeBoneSync = isMale ? maleBoneSync : femaleBoneSync;
    }

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
        playerClothesInfo = info;
        SetGender(info.IsMale);

        // Clear existing clothes
        for (int i = 0; i < clothingHolders.Length; i++)
        {
            foreach (Transform child in clothingHolders[i])
            {
                Destroy(child.gameObject);
            }
        }

        // List to hold bones and instantiated clothes
        var bonesToSync = new List<Transform>(10);  // Pre-allocate for expected size
        var instantiatedClothes = new List<GameObject>(10);  // Pre-allocate for expected size

        // Load and instantiate new clothes
        for (int i = 0; i < clothingHolders.Length; i++)
        {
            var item = ClothesList.Instance.GetClothingItem((ClothingType)i, info.GetClothingIndex((ClothingType)i), info.IsMale);

            if (item == null || !item.prefab.RuntimeKeyIsValid())
            {
                if (item == null)
                    Debug.LogWarning($"Clothing item of type {(ClothingType)i} not found.");
                else
                    Debug.LogWarning("Clothing item prefab is not set.");

                continue;
            }

            var handle = Addressables.InstantiateAsync(item.prefab, clothingHolders[i]);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var result = handle.Result;
                result.transform.SetParent(clothingHolders[i], false);
                UpdateObjectLayer(result, clothingHolders[i].gameObject.layer);
                bonesToSync.Add(GetChildToSync(result.transform));
                instantiatedClothes.Add(result);
                result.SetActive(false);
            }
            else
            {
                Debug.LogError($"Failed to load clothing item: {(ClothingType)i}");
            }
        }

        activeBoneSync.clothingRoots = bonesToSync.ToArray();

        foreach (var go in instantiatedClothes)
        {
            go.SetActive(true);
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

        return parent.GetChild(0);
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

        Player player = lobby.Players.Where(p => p.Id == assignedPlayer).FirstOrDefault();

        PlayerClothesInfo playerClothesInfo = PlayerClothesInfo.Deserialize(player.Data["ClothingInfo"].Value);

        if (!playerClothesInfo.Equals(lastPlayerClothesInfo))
        {
            lastPlayerClothesInfo = playerClothesInfo;
            InitializeClothes(playerClothesInfo);
        }
    }
}
