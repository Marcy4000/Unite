using JSAM;
using System.Collections;
using TMPro;
using UI.ThreeDimensional;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[System.Serializable]
public class Dialogue
{
    public string Title;

    [TextArea]
    public string Body;

    public Sprite PreviewImage; // Reference to the preview image
}

public enum DialoguePortraitMode
{
    Default,
    Hidden,
    Custom
}

[DefaultExecutionOrder(-200)]
public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance { get; private set; }

    [SerializeField] private Dialogue[] dialogues; // Array of dialogues

    [SerializeField] private TextMeshProUGUI titleText; // Reference to the Title TextMeshPro UI
    [SerializeField] private TextMeshProUGUI bodyText;  // Reference to the Body TextMeshPro UI

    [SerializeField] private Button nextButton;      // Reference to the Next Button
    [SerializeField] private Button previousButton;  // Reference to the Previous Button

    [SerializeField] private GameObject dialoguePanel; // Reference to the Dialogue Panel

    [SerializeField] private UIObject3D professor;
    [SerializeField] private Image previewImage; // Reference to the Image component for the preview image

    private int currentDialogueIndex = 0; // Tracks the current dialogue index
    private Transform defaultPortrait;
    private bool isWriting;
    private bool hasFocus = true;
    private int openedFrame = -1;
    private int consumedInputFrame = -1;
    private int stickDirection;
    private float nextStickRepeat;
    private EventSystem navigationEventSystem;
    private bool previousNavigationEnabled;
    private GameObject previousSelection;

    public bool IsVisible => dialoguePanel != null && dialoguePanel.activeInHierarchy;
    public bool BlocksWorldInput => IsVisible || consumedInputFrame == Time.frameCount;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            defaultPortrait = professor != null ? professor.ObjectPrefab : null;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Initialize the UI and buttons
        nextButton.onClick.AddListener(NextDialogue);
        previousButton.onClick.AddListener(PreviousDialogue);
        UpdateButtonInteractivity();
        HideDialoguePanel();
    }

    private void Update()
    {
        if (!IsVisible)
        {
            if (Time.frameCount != consumedInputFrame) RestoreNavigation();
            return;
        }
        CaptureNavigation();
        if (Time.frameCount == openedFrame || !hasFocus) return;
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        bool cancel = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        bool submit = (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
            keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)) ||
            (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
        bool next = (keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)) ||
            (gamepad != null && (gamepad.dpad.right.wasPressedThisFrame || gamepad.dpad.down.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame));
        bool previous = (keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)) ||
            (gamepad != null && (gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.up.wasPressedThisFrame || gamepad.leftShoulder.wasPressedThisFrame));
        int direction = 0;
        if (gamepad != null)
        {
            var stick = gamepad.leftStick.ReadValue();
            float axis = Mathf.Abs(stick.x) >= Mathf.Abs(stick.y) ? stick.x : -stick.y;
            if (Mathf.Abs(axis) >= 0.6f) direction = axis > 0f ? 1 : -1;
        }
        if (direction != 0 && (direction != stickDirection || Time.unscaledTime >= nextStickRepeat))
        {
            next |= direction > 0;
            previous |= direction < 0;
            nextStickRepeat = Time.unscaledTime + (direction != stickDirection ? 0.4f : 0.15f);
        }
        stickDirection = direction;
        if (cancel)
        {
            HideDialoguePanel();
            return;
        }
        if (!submit && !next && !previous) return;
        consumedInputFrame = Time.frameCount;
        if (submit) AdvanceDialogue();
        else if (next) NextDialogue();
        else PreviousDialogue();
    }

    public void AdvanceDialogue()
    {
        if (!IsVisible || dialogues == null || dialogues.Length == 0) return;
        consumedInputFrame = Time.frameCount;
        if (isWriting)
        {
            StopAllCoroutines();
            isWriting = false;
            bodyText.text = dialogues[currentDialogueIndex].Body ?? string.Empty;
        }
        else if (currentDialogueIndex < dialogues.Length - 1) NextDialogue();
        else HideDialoguePanel();
    }

    public void SetDialogues(Dialogue[] dialogues)
    {
        SetDialogues(dialogues, DialoguePortraitMode.Default, null);
    }

    public void SetDialogues(Dialogue[] dialogues, DialoguePortraitMode portraitMode, GameObject portraitPrefab)
    {
        if (dialogues == null || dialogues.Length == 0) return;
        ConfigurePortrait(portraitMode, portraitPrefab);
        AudioManager.PlaySound(DefaultAudioSounds.Game_Ui_Rookie_Tip_3);
        this.dialogues = dialogues;
        currentDialogueIndex = 0;
        ShowDialoguePanel();
        UpdateDialogue();
        UpdateButtonInteractivity();
    }

    // Method to update the dialogue display
    void UpdateDialogue()
    {
        if (currentDialogueIndex >= 0 && currentDialogueIndex < dialogues.Length)
        {
            PlayProfessorAnimation();
            titleText.text = dialogues[currentDialogueIndex].Title;

            if (dialogues[currentDialogueIndex].PreviewImage != null)
            {
                previewImage.sprite = dialogues[currentDialogueIndex].PreviewImage;
                previewImage.gameObject.SetActive(true);
            }
            else
            {
                previewImage.gameObject.SetActive(false);
            }

            StopAllCoroutines();
            StartCoroutine(WriteDialogue(dialogues[currentDialogueIndex].Body));
        }
    }

    IEnumerator WriteDialogue(string dialogue)
    {
        bodyText.text = "";
        isWriting = true;
        foreach (char letter in dialogue ?? string.Empty)
        {
            bodyText.text += letter;
            yield return new WaitForSecondsRealtime(0.04f);
        }
        isWriting = false;
    }

    // Go to the next dialogue
    void NextDialogue()
    {
        if (currentDialogueIndex < dialogues.Length - 1)
        {
            AudioManager.PlaySound(DefaultAudioSounds.Game_Ui_Rookie_Tip_2);
            currentDialogueIndex++;
            UpdateDialogue();
            UpdateButtonInteractivity();
        }
    }

    // Go to the previous dialogue
    void PreviousDialogue()
    {
        if (currentDialogueIndex > 0)
        {
            AudioManager.PlaySound(DefaultAudioSounds.Game_Ui_Rookie_Tip_2);
            currentDialogueIndex--;
            UpdateDialogue();
            UpdateButtonInteractivity();
        }
    }

    // Enable or disable buttons based on the current dialogue index
    void UpdateButtonInteractivity()
    {
        nextButton.interactable = currentDialogueIndex < dialogues.Length - 1;
        previousButton.interactable = currentDialogueIndex > 0;
    }

    public void HideDialoguePanel()
    {
        if (IsVisible) consumedInputFrame = Time.frameCount;
        StopAllCoroutines();
        isWriting = false;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    private void CaptureNavigation()
    {
        if (navigationEventSystem == EventSystem.current) return;
        RestoreNavigation();
        navigationEventSystem = EventSystem.current;
        if (navigationEventSystem == null) return;
        previousNavigationEnabled = navigationEventSystem.sendNavigationEvents;
        previousSelection = navigationEventSystem.currentSelectedGameObject;
        navigationEventSystem.sendNavigationEvents = false;
        navigationEventSystem.SetSelectedGameObject(null);
    }

    private void RestoreNavigation()
    {
        if (navigationEventSystem == null) return;
        navigationEventSystem.sendNavigationEvents = previousNavigationEnabled;
        if (navigationEventSystem.currentSelectedGameObject == null && previousSelection != null && previousSelection.activeInHierarchy)
            navigationEventSystem.SetSelectedGameObject(previousSelection);
        navigationEventSystem = null;
        previousSelection = null;
    }

    private void ConfigurePortrait(DialoguePortraitMode mode, GameObject prefab)
    {
        if (professor == null) return;
        var target = mode == DialoguePortraitMode.Custom ? (prefab != null ? prefab.transform : null) : defaultPortrait;
        bool visible = mode != DialoguePortraitMode.Hidden && target != null;
        professor.gameObject.SetActive(visible);
        if (visible && professor.ObjectPrefab != target) professor.ObjectPrefab = target;
    }

    private void ShowDialoguePanel()
    {
        openedFrame = Time.frameCount;
        stickDirection = 0;
        nextStickRepeat = 0f;
        dialoguePanel.SetActive(true);
        CaptureNavigation();
    }

    private void PlayProfessorAnimation()
    {
        if (professor != null && professor.isActiveAndEnabled && professor.TargetGameObject != null)
        {
            var animator = professor.TargetGameObject.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var parameter in animator.parameters)
                if (parameter.name == "Talk" && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger("Talk");
                    break;
                }
        }
    }

    private void OnDestroy()
    {
        RestoreNavigation();
        if (Instance == this) Instance = null;
    }

    private void OnDisable()
    {
        HideDialoguePanel();
        RestoreNavigation();
    }

    private void OnApplicationFocus(bool focused) => hasFocus = focused;
}
