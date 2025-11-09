using UnityEngine;

// This attribute allows you to create the preset asset from the Assets > Create menu.
[CreateAssetMenu(fileName = "NewShaderConversionPreset", menuName = "Clothing/Shader Conversion Preset")]
public class ShaderConversionPreset : ScriptableObject
{
    [Header("Shader To Apply")]
    public Shader targetShader;

    [Header("Common Settings")]
    [ColorUsage(true, true)] public Color colorSkin = Color.white;
    [Range(-1, 1)] public float shadowOffset = 0;
    [Range(0.5f, 10)] public float shadowPow = 1;
    [Range(0, 1)] public float shadowScale = 0;
    public bool enableFaceShadowScale = false;
    [Range(0, 1)] public float faceShadowScaleValue = 0; // Value to use if enabled
    [Range(-1, 1)] public float roughNessOffset = 0;
    [ColorUsage(true, true)] public Color specularColor = Color.white;
    [Range(-1, 1)] public float metallicOffset = 0;
    [Range(-8, 8)] public float normalScale = 1;
    [Range(0.04f, 32)] public float mainlightAttenuation = 0.04f;

    public enum AttenuationVector { X = 0, Y = 1, Z = 2 }
    public AttenuationVector attenuationVector = AttenuationVector.Y;

    public enum SHType { Cla = 0, Lerp = 1 }
    [Header("SH Settings")]
    public SHType shType = SHType.Lerp;
    [Range(0, 10)] public float shScale = 1;
    [ColorUsage(true, true)] public Color shTopColor = new Color(2.5f, 2.5f, 2.5f, 1);
    [ColorUsage(true, true)] public Color shBotColor = new Color(2.5f, 2.5f, 2.5f, 1);
    public Color shColorScale = Color.white;


    [Header("Outline Settings")]
    public bool useVTex = false;
    public bool useVColor2N = false;
    public Color outlineColor = new Color(0.5f, 0.5f, 0.5f, 1);
    [Tooltip("This is a multiplier in the script. 0.015 is a decent starting point.")]
    public float outlineWidth = 0.015f;
    public float zOffset = -5;
    public Vector4 lightDirection = new Vector4(9.48f, 3.68f, 0, 0);

    [Header("PBR Additional Params")]
    [Range(0, 1)] public float emissiveFactor = 1.0f;
    public float emissionScale = 1.0f;
    [Range(0, 1)] public float alphaCutoff = 0.5f;

    [Header("Rim Light")]
    public Vector4 rimLightDirection = new Vector4(0, 1, 0, 0);
    [Range(0, 5)] public float rimlightScale = 1.0f;
    [Range(0, 5)] public float rimlightScale2 = 1.5f;
    [Range(0, 1)] public float rimlightShadowScale = 0.5f;
    [ColorUsage(true, true)] public Color rimlightColor = Color.white;
    [Range(0.04f, 32)] public float rimlightAttenuation = 0.04f;

    [Header("Add Light")]
    public Vector3 addLightDirection = new Vector3(0, 1, 0);
    [ColorUsage(true, true)] public Color addlightColor = Color.black;
    [Range(0, 1)] public float addlightLerp = 0.5f;
    [Range(0.04f, 32)] public float addlightAttenuation = 1.0f;

    [Header("Hair Settings")]
    [ColorUsage(true, true)] public Color color1 = Color.white;
    [ColorUsage(true, true)] public Color color2 = Color.white;
    [ColorUsage(true, true)] public Color color3 = new Color(0.5f, 0.5f, 0.5f, 1f);
    [ColorUsage(true, true)] public Color specularColor1 = Color.white;
    [ColorUsage(true, true)] public Color specularColor2 = Color.white;
    [Range(-1, 1)] public float glossiness_1X = 0.13f;
    [Range(0, 2)] public float glossiness_1Y = 0.55f;
    [Range(-1, 1)] public float glossiness_2X = 0.4f;
    [Range(0, 2)] public float glossiness_2Y = 1.0f;

    [Header("Render States")]
    public UnityEngine.Rendering.RenderQueue renderQueue = UnityEngine.Rendering.RenderQueue.Geometry;
    public UnityEngine.Rendering.CullMode cullMode = UnityEngine.Rendering.CullMode.Back;
    public bool zWrite = true;
    public UnityEngine.Rendering.CompareFunction zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
    public UnityEngine.Rendering.BlendMode srcBlend = UnityEngine.Rendering.BlendMode.One;
    public UnityEngine.Rendering.BlendMode dstBlend = UnityEngine.Rendering.BlendMode.Zero;
    public UnityEngine.Rendering.BlendOp blendOp = UnityEngine.Rendering.BlendOp.Add;
}
