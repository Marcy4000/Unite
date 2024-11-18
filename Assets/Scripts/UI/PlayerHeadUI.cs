using UnityEngine;
using UnityEngine.UI;

public class PlayerHeadUI : MonoBehaviour
{
    [SerializeField] private Image headImage;
    [SerializeField] private Image hairImage;
    [SerializeField] private Image eyesImage;
    [SerializeField] private Image pupilImage;

    [SerializeField] private Sprite[] maleHeads;
    [SerializeField] private Sprite[] femaleHeads;

    [SerializeField] private Sprite[] maleHairs;
    [SerializeField] private Sprite[] femaleHairs;

    [SerializeField] private Sprite[] maleEyes;
    [SerializeField] private Sprite[] femaleEyes;

    [SerializeField] private Sprite[] malePupils;
    [SerializeField] private Sprite[] femalePupils;

    public void InitializeHead(PlayerClothesInfo playerClothesInfo)
    {
        headImage.sprite = playerClothesInfo.IsMale ? maleHeads[playerClothesInfo.SkinColor] : femaleHeads[playerClothesInfo.SkinColor];
        hairImage.sprite = playerClothesInfo.IsMale ? maleHairs[playerClothesInfo.Hair] : femaleHairs[playerClothesInfo.Hair];
        eyesImage.sprite = playerClothesInfo.IsMale ? maleEyes[playerClothesInfo.Face] : femaleEyes[playerClothesInfo.Face];
        pupilImage.sprite = playerClothesInfo.IsMale ? malePupils[playerClothesInfo.Face] : femalePupils[playerClothesInfo.Face];

        hairImage.color = playerClothesInfo.HairColor;
        pupilImage.color = playerClothesInfo.EyeColor;
    }
}
