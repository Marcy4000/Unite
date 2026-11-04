using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class OpenWorldNpcAuthoring
{
    public const string TrainerPath = "Assets/Prefabs/Lobby/OpenWorldTrainerNpc.prefab";
    public const string PokemonPath = "Assets/Prefabs/Lobby/OpenWorldPokemonNpc.prefab";

    public const string SeatPath = "Assets/Prefabs/Lobby/OpenWorldSeat.prefab";
    public const string GenericPath = "Assets/Prefabs/Lobby/OpenWorldGenericNpc.prefab";

    [MenuItem("Tools/Open World Lobby/Create NPC Prefabs")]
    public static void CreatePrefabs()
    {
        Create(TrainerPath, OpenWorldNpcKind.Trainer);
        Create(PokemonPath, OpenWorldNpcKind.Pokemon);
        Create(GenericPath, OpenWorldNpcKind.Generic);
        Create(SeatPath, OpenWorldNpcKind.Generic, true);
        AssetDatabase.SaveAssets();
    }

    private static AnimationClip Clip(string guid)
    {
        return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<AnimationClip>().First();
    }

    private static void Create(string path, OpenWorldNpcKind kind, bool seat = false)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        bool pokemon = kind == OpenWorldNpcKind.Pokemon;
        bool generic = kind == OpenWorldNpcKind.Generic;
        var root = new GameObject(seat ? "OpenWorldSeat" : $"OpenWorld{kind}Npc");
        root.SetActive(false);
        try
        {
            string modelPath = pokemon ? "Assets/Prefabs/PokemoModels/Eevee/Eevee.prefab"
                : "Assets/Prefabs/TrainerModels/TrainerModel.prefab";
            GameObject model = null;
            if (!generic)
            {
                model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
                model.name = "Visual";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
            }
            var collider = root.AddComponent<CapsuleCollider>();
            collider.enabled = !generic;
            collider.height = pokemon ? 1f : 1.8f;
            collider.radius = pokemon ? 0.4f : 0.3f;
            collider.center = Vector3.up * collider.height * 0.5f;

            var canvasRoot = new GameObject("InteractPrompt", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(root.transform, false);
            canvasRoot.transform.localPosition = Vector3.up * (pokemon ? 1.3f : 2.2f);
            canvasRoot.transform.localScale = Vector3.one * 0.006f;
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            ((RectTransform)canvasRoot.transform).sizeDelta = new Vector2(260f, 64f);

            var buttonRoot = new GameObject("Interact", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonRoot.transform.SetParent(canvasRoot.transform, false);
            Stretch((RectTransform)buttonRoot.transform);
            var background = buttonRoot.GetComponent<Image>();
            background.color = new Color(0.08f, 0.12f, 0.2f, 0.94f);
            var button = buttonRoot.GetComponent<Button>();
            button.targetGraphic = background;

            var labelRoot = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelRoot.transform.SetParent(buttonRoot.transform, false);
            Stretch((RectTransform)labelRoot.transform);
            var label = labelRoot.GetComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("70b34b6cb272ea04e8d96ee043c39c76"));
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = seat ? "Sit  [E / A]" : pokemon ? "Pet  [E / A]" : "Talk  [E / A]";

            var data = new SerializedObject(root.AddComponent<OpenWorldNpc>());
            data.FindProperty("kind").enumValueIndex = (int)kind;
            data.FindProperty("visual").objectReferenceValue = model;
            data.FindProperty("interaction").enumValueIndex = seat ? 2 : pokemon ? 0 : 1;
            data.FindProperty("interactionLabel").stringValue = seat ? "Sit" : pokemon ? "Pet" : "Talk";
            data.FindProperty("idleAnimation").objectReferenceValue = generic ? null : Clip(pokemon
                ? "7d0826a2d4b446d41b9f31ed6d5a65c0" : "98dfef30533ae0942a0a5bf389850c32");
            data.FindProperty("interactAnimation").objectReferenceValue = generic ? null : Clip(pokemon
                ? "b3ea2b802efcb9944a9b8aa5889300ef" : "15afd52789516f041ac358bc1cbf44c6");
            data.FindProperty("maleTrainerAnimation").objectReferenceValue = Clip("15afd52789516f041ac358bc1cbf44c6");
            data.FindProperty("femaleTrainerAnimation").objectReferenceValue = Clip("90960248db95f474fa1d966a4c1a97a0");
            data.FindProperty("clothes").stringValue = new PlayerClothesInfo
            {
                IsMale = true,
                HairColor = new Color32(80, 60, 40, 255),
                EyeColor = new Color32(80, 100, 120, 255)
            }.Serialize();
            data.FindProperty("prompt").objectReferenceValue = canvas;
            data.FindProperty("interactButton").objectReferenceValue = button;
            data.FindProperty("promptText").objectReferenceValue = label;
            if (!pokemon)
            {
                var dialogues = data.FindProperty("dialogues");
                dialogues.arraySize = 1;
                dialogues.GetArrayElementAtIndex(0).FindPropertyRelative("Title").stringValue = generic ? "Generic" : "Trainer";
                dialogues.GetArrayElementAtIndex(0).FindPropertyRelative("Body").stringValue = "Welcome to the city!";
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}

[CustomEditor(typeof(OpenWorldNpc))]
public sealed class OpenWorldNpcEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Field("kind");
        Field("visual");
        bool pokemon = serializedObject.FindProperty("kind").enumValueIndex == 1;
        bool generic = serializedObject.FindProperty("kind").enumValueIndex == 2;
        if (!pokemon && !generic) Field("clothes");
        Field("idleAnimation");
        Field("interactAnimation");
        Field("animationBlendDuration");
        Field("playTrainerAnimation");
        if (serializedObject.FindProperty("playTrainerAnimation").boolValue)
        {
            Field("maleTrainerAnimation");
            Field("femaleTrainerAnimation");
        }
        if (!pokemon) Field("interaction");
        if (!pokemon && serializedObject.FindProperty("interaction").enumValueIndex == 2)
        {
            Field("seatAnchor");
            Field("seatFeetOffset");
            EditorGUILayout.HelpBox("The yellow gizmo marks the trainer's feet, not the cushion. Position it on the floor in front of the seat; the hips sit behind it. Anchor forward sets the facing direction.", MessageType.Info);
        }
        Field("interactionDistance");
        Field("interactionLabel");
        if (!pokemon && serializedObject.FindProperty("interaction").enumValueIndex == 1)
        {
            Field("dialogues");
            Field("portraitMode");
            if (serializedObject.FindProperty("portraitMode").enumValueIndex == 2) Field("portraitPrefab");
        }
        Field("prompt");
        Field("interactButton");
        Field("promptText");
        EditorGUILayout.HelpBox("Place in the city scene. Approach and press E / gamepad A, or tap the prompt. " +
            "Generic NPCs support dialogue and optional player animations with no model, a static model, or a custom animated model. " +
            "Assign a child object as Visual to animate it. Animation clips must match its rig. Pokémon only support animation interactions.", MessageType.Info);
        serializedObject.ApplyModifiedProperties();
    }

    private void Field(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
}
