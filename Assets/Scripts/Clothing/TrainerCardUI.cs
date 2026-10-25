using System.Collections;
using System.Threading.Tasks;
using UI.ThreeDimensional;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class TrainerCardUI : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private UIObject3D trainerObject;

    [SerializeField] private GameObject trainerPrefab;

    [SerializeField] private TrainerCardItem[] backgrounds;
    [SerializeField] private TrainerCardItem[] frames;

    [SerializeField] private string[] maleAnimations;
    [SerializeField] private string[] femaleAnimations;

    private AsyncOperationHandle<Sprite> backgroundHandle;
    private AsyncOperationHandle<Sprite> frameHandle;

    private byte currentAnimationIndex;
    private TrainerModel trainerModel;
    
    private PlayerClothesInfo? currentClothes; 
    private Coroutine setupCoroutine;

    private void OnEnable()
    {
        if (currentClothes.HasValue)
        {
            if (setupCoroutine != null)
            {
                StopCoroutine(setupCoroutine);
            }
            setupCoroutine = StartCoroutine(SetupTrainerModelUI(currentClothes.Value));
        }
    }

    private void OnDisable()
    {
        if (setupCoroutine != null)
        {
            StopCoroutine(setupCoroutine);
            setupCoroutine = null;
        }
        DetachTrainerModel();
        SetImageAlpha(0f);
    }

    private void OnDestroy()
    {
        DetachTrainerModel();
        if (backgroundHandle.IsValid())
        {
            Addressables.Release(backgroundHandle);
        }

        if (frameHandle.IsValid())
        {
            Addressables.Release(frameHandle);
        }
    }

    public void Initialize()
    {
        Initialize(PlayerClothesInfo.Deserialize(LobbyController.Instance.Player.Data["ClothingInfo"].Value));
    }

    public void Initialize(PlayerClothesInfo clothes)
    {
        currentClothes = clothes;
        currentAnimationIndex = clothes.TrainerCardInfo.TrainerAnimation;

        trainerObject.ObjectPrefab = trainerPrefab.transform;

        if (gameObject.activeInHierarchy)
        {
            if (setupCoroutine != null)
            {
                StopCoroutine(setupCoroutine);
            }
            setupCoroutine = StartCoroutine(SetupTrainerModelUI(clothes));
        }

        _ = LoadSpritesAsync(clothes.TrainerCardInfo.BackgroundIndex, clothes.TrainerCardInfo.FrameIndex);

        trainerObject.TargetRotation = new Vector3(0f, 180 + clothes.TrainerCardInfo.RotationOffset, 0f);
        float xPos = (clothes.TrainerCardInfo.TrainerOffestX / (float)short.MaxValue) * 2f;
        float yPos = (clothes.TrainerCardInfo.TrainerOffestY / (float)short.MaxValue) * 2f;
        trainerObject.TargetOffset = new Vector2(xPos, yPos);

        float actualCameraDistance = -1f - (clothes.TrainerCardInfo.TrainerScale / 255f) * 9f;
        trainerObject.CameraDistance = actualCameraDistance;
    }

    private IEnumerator SetupTrainerModelUI(PlayerClothesInfo clothes)
    {
        SetImageAlpha(0f);

        while (trainerObject.TargetGameObject == null)
        {
            yield return null;
        }

        DetachTrainerModel();
        trainerModel = trainerObject.TargetGameObject.GetComponent<TrainerModel>();
        // The prefab can still be using the other gender's animator until the
        // asynchronous outfit commit. Never play the requested pose before that.
        trainerModel.onClothesInitialized += OnTrainerClothesInitialized;
        trainerModel.InitializeClothes(clothes);
    }

    private void DetachTrainerModel()
    {
        if (trainerModel != null) trainerModel.onClothesInitialized -= OnTrainerClothesInitialized;
        trainerModel = null;
    }

    private void OnTrainerClothesInitialized()
    {
        PlayCurrentModelAnimation();
    }

    private void PlayCurrentModelAnimation()
    {
        if (!isActiveAndEnabled || trainerModel == null || !trainerModel.IsInitialized
            || !currentClothes.HasValue || trainerModel.IsMale != currentClothes.Value.IsMale) return;

        // Resolve at completion, not when loading started: the pose may have been
        // edited while the new outfit was still loading.
        string animation = trainerModel.IsMale ? maleAnimations[currentAnimationIndex] : femaleAnimations[currentAnimationIndex];
        PlayModelAnimation(animation);
    }

    public void UpdateCardPlayer(TrainerCardInfo cardInfo)
    {
        trainerObject.TargetRotation = new Vector3(0f, 180 + cardInfo.RotationOffset, 0f);
        float xPos = (cardInfo.TrainerOffestX / (float)short.MaxValue) * 2f;
        float yPos = (cardInfo.TrainerOffestY / (float)short.MaxValue) * 2f;
        trainerObject.TargetOffset = new Vector2(xPos, yPos);

        float actualCameraDistance = -1f - (cardInfo.TrainerScale / 255f) * 9f;
        trainerObject.CameraDistance = actualCameraDistance;
        currentAnimationIndex = cardInfo.TrainerAnimation;

        if (currentClothes.HasValue)
        {
            var updatedClothes = currentClothes.Value;
            updatedClothes.TrainerCardInfo = cardInfo;
            currentClothes = updatedClothes;
        }

        // While loading, the completion callback will apply this pose to the
        // committed animator rather than the old visible outfit's animator.
        if (trainerModel != null && !trainerModel.IsLoading) PlayCurrentModelAnimation();
    }

    public async void UpdateCardFrame(TrainerCardInfo cardInfo)
    {
        await LoadSpritesAsync(cardInfo.BackgroundIndex, cardInfo.FrameIndex);
    }

    private async Task LoadSpritesAsync(byte backgroundIndex, byte frameIndex)
    {
        if (backgroundHandle.IsValid())
        {
            backgroundImage.sprite = null;
            Addressables.Release(backgroundHandle);
        }

        if (frameHandle.IsValid())
        {
            frameImage.sprite = null;
            Addressables.Release(frameHandle);
        }

        backgroundHandle = Addressables.LoadAssetAsync<Sprite>(backgrounds[backgroundIndex].itemSprite);
        frameHandle = Addressables.LoadAssetAsync<Sprite>(frames[frameIndex].itemSprite);

        await Task.WhenAll(backgroundHandle.Task, frameHandle.Task);

        backgroundImage.sprite = backgroundHandle.Result;
        frameImage.sprite = frameHandle.Result;
    }

    private void PlayModelAnimation(string animationName, bool triggerRender = true)
    {
        if (trainerObject.TargetGameObject == null || trainerModel == null)
        {
            return;
        }

        Animator animator = trainerModel.ActiveAnimator;
        if (animator != null)
        {
            animator.Play(animationName);
            animator.Update(0f); 

            if (triggerRender && gameObject.activeInHierarchy)
            {
                StartCoroutine(RenderDelayed());
            }
        }
    }

    private IEnumerator RenderDelayed()
    {
        yield return new WaitForEndOfFrame();
        if (!isActiveAndEnabled || trainerModel == null || !trainerModel.IsInitialized) yield break;
        
        trainerObject.Render();
        
        SetImageAlpha(1f);
    }

    private void SetImageAlpha(float alpha)
{
    if (trainerObject != null && trainerObject.imageComponent != null)
    {
        trainerObject.imageComponent.color = new Color(1f, 1f, 1f, alpha);
    }
}
}
