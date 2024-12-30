using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class MoveInfoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text moveNameText, moveTypeText, moveCooldownText;
    [SerializeField] private Image moveIcon, movePreview;
    [SerializeField] private GameObject normalFrame, uniteFrame;
    [SerializeField] private Button moreInfoButton;

    private MoveAsset move;

    private AsyncOperationHandle<Sprite> previewHandle;

    public event System.Action<MoveAsset> OnMoreInfoClicked;

    public void Initialize(MoveAsset move, System.Action<MoveAsset> onMoreInfoClicked)
    {
        this.move = move;
        moveNameText.text = move.moveName;
        moveTypeText.text = AddSpacesToCamelCase(move.moveType.ToString());
        moveCooldownText.text = $"{move.cooldown} s";
        moveIcon.sprite = move.icon;

        normalFrame.SetActive(move.moveType != MoveType.UniteMove);
        uniteFrame.SetActive(move.moveType == MoveType.UniteMove);

        moreInfoButton.onClick.AddListener(() => OnMoreInfoClicked?.Invoke(move));
        OnMoreInfoClicked += onMoreInfoClicked;

        LoadPreview();
    }

    private string AddSpacesToCamelCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var newText = new StringBuilder(text.Length * 2);
        newText.Append(text[0]);
        for (int i = 1; i < text.Length; i++)
        {
            if (char.IsUpper(text[i]) && text[i - 1] != ' ')
                newText.Append(' ');
            newText.Append(text[i]);
        }
        return newText.ToString();
    }

    private void LoadPreview()
    {
        if (previewHandle.IsValid())
            Addressables.Release(previewHandle);

        if (string.IsNullOrWhiteSpace(move.preview.AssetGUID))
            return;

        previewHandle = Addressables.LoadAssetAsync<Sprite>(move.preview);
        previewHandle.Completed += OnPreviewLoaded;
    }

    private void OnPreviewLoaded(AsyncOperationHandle<Sprite> handle)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            if (movePreview == null)
            {
                return;
            }
            movePreview.sprite = handle.Result;
        }
    }


    private void OnDestroy()
    {
        if (previewHandle.IsValid())
            Addressables.Release(previewHandle);
    }
}
