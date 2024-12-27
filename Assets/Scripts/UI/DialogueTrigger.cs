using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [SerializeField] private Dialogue[] dialogue;
    [SerializeField] private bool triggerOnlyOnce = true;

    private string prefsKey;

    private void Awake()
    {
        prefsKey = $"{gameObject.name}_{dialogue.Length}_DialogueTriggered";
    }

    public void TriggerDialogue()
    {
        if (triggerOnlyOnce && PlayerPrefs.GetInt(prefsKey, 0) == 1)
        {
            return; // Dialogue has already been triggered
        }

        DialogueSystem.Instance.SetDialogues(dialogue);

        if (triggerOnlyOnce)
        {
            PlayerPrefs.SetInt(prefsKey, 1); // Mark dialogue as triggered
            PlayerPrefs.Save(); // Save changes to disk
        }
    }
}
