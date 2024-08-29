using JSAM;
using System.Collections;
using TMPro;
using UI.ThreeDimensional;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class Dialogue
{
    public string Title;

    [TextArea]
    public string Body;
}

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

    private int currentDialogueIndex = 0; // Tracks the current dialogue index

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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

    public void SetDialogues(Dialogue[] dialogues)
    {
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

            StopAllCoroutines();
            StartCoroutine(WriteDialogue(dialogues[currentDialogueIndex].Body));
        }
    }

    IEnumerator WriteDialogue(string dialogue)
    {
        bodyText.text = "";
        foreach (char letter in dialogue)
        {
            bodyText.text += letter;
            yield return new WaitForSeconds(0.04f);
        }
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
        dialoguePanel.SetActive(false);
    }

    private void ShowDialoguePanel()
    {
        dialoguePanel.SetActive(true);
    }

    private void PlayProfessorAnimation()
    {
        if (professor.TargetGameObject != null)
        {
            professor.TargetGameObject.GetComponent<Animator>().SetTrigger("Talk");
        }
    }
}
