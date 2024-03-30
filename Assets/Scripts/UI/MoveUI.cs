using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoveUI : MonoBehaviour
{
    [SerializeField] private Image moveIcon;
    [SerializeField] private GameObject cdLine;
    [SerializeField] private TMP_Text cdText, moveName;

    private Coroutine cooldownCoroutine;

    private void Start()
    {
        cdLine.SetActive(false);
        cdText.gameObject.SetActive(false);
    }

    public void Initialize(MoveAsset move)
    {
        moveIcon.sprite = move.icon;
        moveName.text = MoveDatabase.GetMove(move.move).name;
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

        float timer = cooldownDuration;

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
    }
}
