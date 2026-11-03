using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [SerializeField] private Dialogue[] dialogue;
    [SerializeField] private bool triggerOnlyOnce = true;
    [SerializeField] private DialoguePortraitMode portraitMode;
    [SerializeField] private GameObject portraitPrefab;

    private string prefsKey;

    private void Awake()
    {
        prefsKey = $"{gameObject.name}_{dialogue.Length}_DialogueTriggered";
    }

    public void TriggerDialogue()
    {
        TriggerDialogue(false);
    }

    public void TriggerDialogue(bool skipRepeatingCheck)
    {
        if (triggerOnlyOnce && PlayerPrefs.GetInt(prefsKey, 0) == 1 && !skipRepeatingCheck)
        {
            return; // Dialogue has already been triggered
        }

        if (DialogueSystem.Instance == null || dialogue == null || dialogue.Length == 0) return;
        DialogueSystem.Instance.SetDialogues(dialogue, portraitMode, portraitPrefab);

        if (triggerOnlyOnce)
        {
            PlayerPrefs.SetInt(prefsKey, 1); // Mark dialogue as triggered
            PlayerPrefs.Save(); // Save changes to disk
        }
    }
}
