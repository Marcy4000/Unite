using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleItemUI : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private GameObject cdHolder, lockImage, cdLine;
    [SerializeField] private TMP_Text cdText;

    private Coroutine cooldownCoroutine;

    private void Start()
    {
        cdHolder.SetActive(false);
        lockImage.SetActive(false);
    }

    public void Initialize(BattleItemAsset move)
    {
        itemIcon.sprite = move.icon;
    }

    public void SetLock(bool isLocked)
    {
        lockImage.SetActive(isLocked);
    }

    public void StartCooldown(float cooldownDuration, float maxCdDuration)
    {
        if (cooldownCoroutine != null)
            StopCoroutine(cooldownCoroutine);

        cooldownCoroutine = StartCoroutine(CooldownRoutine(cooldownDuration, maxCdDuration));
    }

    private IEnumerator CooldownRoutine(float currentCooldown, float maxCooldownDuration)
    {
        cdHolder.SetActive(true);

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

        cdHolder.SetActive(false);
        cooldownCoroutine = null;
    }
}
