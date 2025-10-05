using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HeldItemsWarning : MonoBehaviour
{
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private Button cancelButton, confirmButton;
    [SerializeField] private TMP_Text confirmButtonText;

    public event System.Action<bool> OnDecisionMade;

    private Coroutine confirmCooldownCoroutine;

    private void Awake()
    {
        cancelButton.onClick.AddListener(() => OnDecisionMade?.Invoke(false));
        confirmButton.onClick.AddListener(() => OnDecisionMade?.Invoke(true));

        OnDecisionMade += (decision) =>
        {
            HideWarning();
        };

        HideWarning();
    }

    public void ShowWarning(HeldItemInfo item, CharacterInfo character)
    {
        gameObject.SetActive(true);
        warningText.text = $"You are about to equip {item.heldItemName} (a {item.damageType} attack item) on {character.pokemonName} (a {character.DamageType} attacker)\nIt's strongly recommended to use an alternative {character.DamageType} atk. based item.";
        if (confirmCooldownCoroutine != null)
            StopCoroutine(confirmCooldownCoroutine);
        confirmCooldownCoroutine = StartCoroutine(ConfirmCooldown());
    }

    private IEnumerator ConfirmCooldown()
    {
        confirmButton.interactable = false;
        for (int i = 5; i > 0; i--)
        {
            confirmButtonText.text = $"Confirm ({i})";
            yield return new WaitForSeconds(1f);
        }
        confirmButtonText.text = "Confirm";
        confirmButton.interactable = true;
    }

    public void HideWarning()
    {
        gameObject.SetActive(false);
    }
}
