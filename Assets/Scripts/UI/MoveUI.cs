using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoveUI : MonoBehaviour
{
    [SerializeField] private Image moveIcon, secondaryCd;
    [SerializeField] private GameObject cdLine, cdBg, lockImage;
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

    public void StartCooldown(float cooldownDuration)
    {
        if (cooldownCoroutine != null)
            StopCoroutine(cooldownCoroutine);

        cooldownCoroutine = StartCoroutine(CooldownRoutine(cooldownDuration));
    }

    private IEnumerator CooldownRoutine(float cooldownDuration)
    {
        cdLine.SetActive(true);
        cdText.gameObject.SetActive(true);
        cdBg.SetActive(true);

        float timer = cooldownDuration;

        cdLine.transform.rotation = Quaternion.identity;

        while (timer > 0)
        {
            timer -= Time.deltaTime;

            // Update cooldown text
            cdText.text = Mathf.CeilToInt(timer).ToString();

            // Rotate the cooldown line clockwise
            cdLine.transform.Rotate(Vector3.forward, -(360f / cooldownDuration) * Time.deltaTime);

            yield return null;
        }

        cdLine.SetActive(false);
        cdText.gameObject.SetActive(false);
        cdBg.SetActive(false);
        cooldownCoroutine = null;
    }
}
