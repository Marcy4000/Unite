using UnityEngine;
using UnityEngine.UI;

public class PlayerHeadUI : MonoBehaviour
{
    private static readonly int TrainerColor = Shader.PropertyToID("_TrainerColor");
    private Material hairMaterial;
    private Material pupilMaterial;

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

        ApplyColor(hairImage, ref hairMaterial, "UI/TrainerHeadHair", playerClothesInfo.HairColor);
        ApplyColor(pupilImage, ref pupilMaterial, "UI/TrainerHeadEyes", playerClothesInfo.EyeColor);
    }

    private static void ApplyColor(Image image, ref Material material, string resource, Color color)
    {
        if (material == null)
        {
            material = new Material(Resources.Load<Material>(resource));
            image.material = material;
        }

        image.color = Color.white;
        material.SetColor(TrainerColor, color);
        image.SetMaterialDirty();
        image.materialForRendering.SetColor(TrainerColor, color);
    }

    private void OnDestroy()
    {
        ReleaseMaterial(hairMaterial);
        ReleaseMaterial(pupilMaterial);
    }

    private static void ReleaseMaterial(Material material)
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material);
        else DestroyImmediate(material);
    }
}
