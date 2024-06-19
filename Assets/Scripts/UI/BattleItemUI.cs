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

    public void StartCooldown(float cooldownDuration)
    {
        if (cooldownCoroutine != null)
            StopCoroutine(cooldownCoroutine);

        cooldownCoroutine = StartCoroutine(CooldownRoutine(cooldownDuration));
    }

    private IEnumerator CooldownRoutine(float cooldownDuration)
    {
        cdHolder.SetActive(true);

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

        cdHolder.SetActive(false);
        cooldownCoroutine = null;
    }
}
