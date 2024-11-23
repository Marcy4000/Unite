using System.Collections;
using UI.ThreeDimensional;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class TrainerCardUI : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private UIObject3D trainerObject;

    [SerializeField] private GameObject trainerPrefab;

    [SerializeField] private AssetReferenceSprite[] backgrounds;
    [SerializeField] private AssetReferenceSprite[] frames;

    [SerializeField] private string[] maleAnimations;
    [SerializeField] private string[] femaleAnimations;

    private AsyncOperationHandle<Sprite> backgroundHandle;
    private AsyncOperationHandle<Sprite> frameHandle;

    private byte currentAnimationIndex;

    private TrainerModel trainerModel;

    private void OnEnable()
    {
        if (trainerModel != null)
        {
            if (trainerModel.IsMale)
            {
                PlayModelAnimation(maleAnimations[currentAnimationIndex]);
            }
            else
            {
                PlayModelAnimation(femaleAnimations[currentAnimationIndex]);
            }
            StartCoroutine(RenderDelayed());
        }
    }

    private void OnDestroy()
    {
        if (backgroundHandle.IsValid())
        {
            Addressables.Release(backgroundHandle);
        }

        if (frameHandle.IsValid())
        {
            Addressables.Release(frameHandle);
        }

        if (trainerModel != null)
        {
            Destroy(trainerModel.gameObject);
        }
    }

    public void Initialize()
    {
        Initialize(PlayerClothesInfo.Deserialize(LobbyController.Instance.Player.Data["ClothingInfo"].Value));
    }

    public void Initialize(PlayerClothesInfo clothes)
    {
        if (trainerModel != null)
        {
            Destroy(trainerModel.gameObject);
        }

        trainerModel = Instantiate(trainerPrefab, new Vector3(0f, -100f, 0f), Quaternion.identity).GetComponent<TrainerModel>();
        trainerModel.InitializeClothes(clothes);

        StartCoroutine(LoadSprites(clothes.TrainerCardInfo.BackgroundIndex, clothes.TrainerCardInfo.FrameIndex));

        trainerObject.TargetRotation = new Vector3(0f, 180+clothes.TrainerCardInfo.RotationOffset, 0f);
        float xPos = (clothes.TrainerCardInfo.TrainerOffestX / (float)short.MaxValue) * 2f;
        float yPos = (clothes.TrainerCardInfo.TrainerOffestY / (float)short.MaxValue) * 2f;
        trainerObject.TargetOffset = new Vector2(xPos, yPos);

        float actualCameraDistance = -1f - (clothes.TrainerCardInfo.TrainerScale / 255f) * 9f;
        trainerObject.CameraDistance = actualCameraDistance;
        currentAnimationIndex = clothes.TrainerCardInfo.TrainerAnimation;

        trainerModel.onClothesInitialized += () =>
        {
            trainerObject.ObjectPrefab = trainerModel.transform;
            if (trainerModel.IsMale)
            {
                PlayModelAnimation(maleAnimations[clothes.TrainerCardInfo.TrainerAnimation]);
            }
            else
            {
                PlayModelAnimation(femaleAnimations[clothes.TrainerCardInfo.TrainerAnimation]);
            }
        };
    }

    public void UpdateCard(TrainerCardInfo cardInfo)
    {
        trainerObject.TargetRotation = new Vector3(0f, 180 + cardInfo.RotationOffset, 0f);
        float xPos = (cardInfo.TrainerOffestX / (float)short.MaxValue) * 2f;
        float yPos = (cardInfo.TrainerOffestY / (float)short.MaxValue) * 2f;
        trainerObject.TargetOffset = new Vector2(xPos, yPos);

        float actualCameraDistance = -1f - (cardInfo.TrainerScale / 255f) * 9f;
        trainerObject.CameraDistance = actualCameraDistance;
        currentAnimationIndex = cardInfo.TrainerAnimation;

        if (trainerModel.IsMale)
        {
            PlayModelAnimation(maleAnimations[cardInfo.TrainerAnimation]);
        }
        else
        {
            PlayModelAnimation(femaleAnimations[cardInfo.TrainerAnimation]);
        }
    }

    private IEnumerator LoadSprites(byte backgroundIndex, byte frameIndex)
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

        backgroundHandle = Addressables.LoadAssetAsync<Sprite>(backgrounds[backgroundIndex]);
        frameHandle = Addressables.LoadAssetAsync<Sprite>(frames[frameIndex]);

        yield return new WaitUntil(() => backgroundHandle.IsDone && frameHandle.IsDone);

        backgroundImage.sprite = backgroundHandle.Result;
        frameImage.sprite = frameHandle.Result;
    }

    private void PlayModelAnimation(string animationName)
    {
        if (trainerObject.TargetGameObject.TryGetComponent(out TrainerModel model))
        {
            model.ActiveAnimator.Play(animationName);
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(RenderDelayed());
            }
        }
    }

    private IEnumerator RenderDelayed()
    {
        yield return null;
        trainerObject.Render();
    }
}
