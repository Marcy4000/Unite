using UnityEngine;
using UnityEngine.UI;

public class PlayerHeadUI : MonoBehaviour
{
    [SerializeField] private Image headImage;
    [SerializeField] private Image hairImage;
    [SerializeField] private Image eyesImage;

    [SerializeField] private Sprite[] maleHeads;
    [SerializeField] private Sprite[] femaleHeads;

    [SerializeField] private Sprite[] maleHairs;
    [SerializeField] private Sprite[] femaleHairs;

    [SerializeField] private Sprite[] maleEyes;
    [SerializeField] private Sprite[] femaleEyes;

    public void InitializeHead(PlayerClothesInfo playerClothesInfo)
    {
        headImage.sprite = playerClothesInfo.IsMale ? maleHeads[playerClothesInfo.Face] : femaleHeads[playerClothesInfo.Face];
        hairImage.sprite = playerClothesInfo.IsMale ? maleHairs[playerClothesInfo.Hair] : femaleHairs[playerClothesInfo.Hair];
        eyesImage.sprite = playerClothesInfo.IsMale ? maleEyes[playerClothesInfo.Face] : femaleEyes[playerClothesInfo.Face];
    }
}
