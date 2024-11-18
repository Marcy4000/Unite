using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoveUI : MonoBehaviour
{
    [SerializeField] private Image moveIcon, secondaryCd, moveLabel;
    [SerializeField] private GameObject cdLine, cdBg, lockImage;
    [SerializeField] private GameObject upgradeFrame;
    [SerializeField] private TMP_Text cdText, moveName;

    private Coroutine cooldownCoroutine;
    private Coroutine secondaryCooldownCoroutine;

    private void Start()
    {
        cdLine.SetActive(false);
        cdText.gameObject.SetActive(false);
        cdBg.SetActive(false);
        lockImage.SetActive(false);
        secondaryCd.gameObject.SetActive(false);
    }

    public void Initialize(MoveAsset move)
    {
        moveIcon.sprite = move.icon;
        moveName.text = MoveDatabase.GetMove(move.move).Name;
        upgradeFrame.SetActive(move.isUpgraded);

        if (move.moveLabel == MoveLabels.None)
        {
            moveLabel.gameObject.SetActive(false);
        }
        else
        {
            moveLabel.gameObject.SetActive(true);
            moveLabel.sprite = CharactersList.Instance.GetMoveLabel(move.moveLabel);
        }
    }

    public void SetLock(bool isLocked)
    {
        lockImage.SetActive(isLocked);
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

    public void StartCooldown(float currentCooldown, float maxCooldownDuration)
    {
        if (cooldownCoroutine != null)
            StopCoroutine(cooldownCoroutine);

        cooldownCoroutine = StartCoroutine(CooldownRoutine(currentCooldown, maxCooldownDuration));
    }

    private IEnumerator CooldownRoutine(float currentCooldown, float maxCooldownDuration)
    {
        cdLine.SetActive(true);
        cdText.gameObject.SetActive(true);
        cdBg.SetActive(true);

        float timer = currentCooldown;

        // Calculate the initial rotation based on the current cooldown
        float initialRotation = -(360f * (1 - (currentCooldown / maxCooldownDuration)));
        cdLine.transform.rotation = Quaternion.Euler(0f, 0f, initialRotation);

        while (timer > 0)
        {
            timer -= Time.deltaTime;

            // Update cooldown text
            cdText.text = timer < 1 ? timer.ToString("F1") : Mathf.CeilToInt(timer).ToString();

            // Rotate the cooldown line clockwise
            cdLine.transform.Rotate(Vector3.forward, -(360f / maxCooldownDuration) * Time.deltaTime);

            yield return null;
        }

        cdLine.SetActive(false);
        cdText.gameObject.SetActive(false);
        cdBg.SetActive(false);
        cooldownCoroutine = null;
    }
}
