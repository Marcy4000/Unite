using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UniteMoveUI : MonoBehaviour
{
    [SerializeField] private Image moveIcon, chargeIndicator, bg, secondaryCd;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private GameObject disabledLock;
    [SerializeField] private TMP_Text cdText, moveName;
    [SerializeField] private Sprite notReady, ready;

    private Coroutine secondaryCooldownCoroutine;

    private void Start()
    {
        lockIcon.SetActive(true);
        cdText.gameObject.SetActive(false);
        moveIcon.gameObject.SetActive(false);
        disabledLock.SetActive(false);
        secondaryCd.gameObject.SetActive(false);
        chargeIndicator.fillAmount = 0;
    }

    public void Initialize(MoveAsset move)
    {
        moveIcon.gameObject.SetActive(true);
        moveIcon.sprite = move.icon;
        moveName.text = MoveDatabase.GetMove(move.move).Name;
    }

    public void SetDisabledLock(bool visible)
    {
        disabledLock.SetActive(visible);
    }

    public void UpdateUI(int uniteMoveCharge, int uniteMaxCharge)
    {
        if (uniteMaxCharge > 0)
        {
            float chargePercentage = (float)uniteMoveCharge / uniteMaxCharge;
            cdText.text = $"{Mathf.FloorToInt(chargePercentage * 100)}%";
            chargeIndicator.fillAmount = chargePercentage;
        }

        if (uniteMoveCharge == uniteMaxCharge)
        {
            lockIcon.SetActive(false);
            cdText.gameObject.SetActive(false);
            bg.sprite = ready;
        }
        else
        {
            lockIcon.SetActive(true);
            cdText.gameObject.SetActive(true);
            bg.sprite = notReady;
        }
    }

    public void ShowSecondaryCooldown(float cdDuration)
    {
        if (secondaryCooldownCoroutine != null)
            StopCoroutine(secondaryCooldownCoroutine);

        secondaryCooldownCoroutine = StartCoroutine(SecondaryCooldownRoutine(cdDuration));
    }

    private IEnumerator SecondaryCooldownRoutine(float cooldownDuration)
    {
        secondaryCd.gameObject.SetActive(true);
        secondaryCd.fillAmount = 1f;

        float timer = cooldownDuration;

        while (timer > 0)
        {
            timer -= Time.deltaTime;

            secondaryCd.fillAmount = timer / cooldownDuration;

            yield return null;
        }

        secondaryCd.gameObject.SetActive(false);
        secondaryCooldownCoroutine = null;
    }
}
