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

    [Header("Stored uses")]
    [SerializeField] private GameObject storedUsesRoot;
    [SerializeField] private GameObject storedCdLine, storedCdBg;
    [SerializeField] private TMP_Text storedCdText, storedUsesText;

    private Coroutine cooldownCoroutine;
    private Coroutine secondaryCooldownCoroutine;
    private MoveBase boundMove;
    private float cooldownEndsAt, cooldownDuration;
    private float secondaryCooldownEndsAt, secondaryCooldownDuration;

    private void Awake()
    {
        cdLine.SetActive(false);
        cdText.gameObject.SetActive(false);
        cdBg.SetActive(false);
        lockImage.SetActive(false);
        secondaryCd.gameObject.SetActive(false);
        if (storedUsesRoot != null) storedUsesRoot.SetActive(false);
    }

    public void Initialize(MoveAsset move, MoveBase runtimeMove = null)
    {
        MoveBase definition = runtimeMove ?? MoveDatabase.GetMove(move.move);
        definition.IsUpgraded = move.isUpgraded;
        moveIcon.sprite = move.icon;
        moveName.text = definition.Name;
        upgradeFrame.SetActive(move.isUpgraded);
        BindMove(definition);

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

    public void BindMove(MoveBase move)
    {
        if (boundMove != move)
        {
            if (cooldownCoroutine != null) StopCoroutine(cooldownCoroutine);
            if (secondaryCooldownCoroutine != null) StopCoroutine(secondaryCooldownCoroutine);
            cooldownCoroutine = secondaryCooldownCoroutine = null;
            cooldownEndsAt = 0;
            secondaryCooldownEndsAt = 0;
            SetCooldownVisual(cdLine, cdBg, cdText, 0, 1);
            secondaryCd.gameObject.SetActive(false);
        }
        boundMove = move;
        RenderStoredUses();
    }

    private void LateUpdate()
    {
        RenderStoredUses();
    }

    private void OnEnable()
    {
        if (boundMove != null && boundMove.IsStoredMove) RenderStoredUses();
        else if (cooldownEndsAt > Time.time) StartCooldown(cooldownEndsAt - Time.time, cooldownDuration);
        else SetCooldownVisual(cdLine, cdBg, cdText, 0, 1);
        if (secondaryCooldownEndsAt > Time.time)
            secondaryCooldownCoroutine = StartCoroutine(SecondaryCooldownRoutine());
        else secondaryCd.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (cooldownCoroutine != null) StopCoroutine(cooldownCoroutine);
        if (secondaryCooldownCoroutine != null) StopCoroutine(secondaryCooldownCoroutine);
        cooldownCoroutine = secondaryCooldownCoroutine = null;
    }

    private void RenderStoredUses()
    {
        bool stored = boundMove != null && boundMove.IsStoredMove;
        if (storedUsesRoot != null && storedUsesRoot.activeSelf != stored) storedUsesRoot.SetActive(stored);
        if (!stored) return;

        if (storedUsesText != null) storedUsesText.text = boundMove.StoredUses.ToString();
        SetCooldownVisual(storedCdLine, storedCdBg, storedCdText,
            boundMove.StoredRechargeRemaining, boundMove.StoredRechargeDuration);
        float useDuration = boundMove.StoredUses == 0
            ? Mathf.Max(boundMove.StoredRechargeDuration, boundMove.StoredUseInterval)
            : boundMove.StoredUseInterval;
        SetCooldownVisual(cdLine, cdBg, cdText, boundMove.StoredUseCooldownRemaining, useDuration);
    }

    private static void SetCooldownVisual(GameObject line, GameObject background, TMP_Text text, float remaining, float duration)
    {
        bool visible = remaining > 0;
        if (line != null) line.SetActive(visible);
        if (background != null) background.SetActive(visible);
        if (text != null)
        {
            text.gameObject.SetActive(visible);
            if (visible) text.text = remaining < 1 ? remaining.ToString("F1") : Mathf.CeilToInt(remaining).ToString();
        }
        if (visible && line != null)
            line.transform.localRotation = Quaternion.Euler(0, 0, -360f * (1f - Mathf.Clamp01(remaining / Mathf.Max(0.01f, duration))));
    }

    public void SetLock(bool isLocked)
    {
        lockImage.SetActive(isLocked);
    }

    public void ShowSecondaryCooldown(float cdDuration)
    {
        if (secondaryCooldownCoroutine != null)
            StopCoroutine(secondaryCooldownCoroutine);

        secondaryCooldownEndsAt = Time.time + Mathf.Max(0, cdDuration);
        secondaryCooldownDuration = Mathf.Max(0.01f, cdDuration);
        if (cdDuration <= 0)
        {
            secondaryCd.gameObject.SetActive(false);
            secondaryCooldownCoroutine = null;
            return;
        }
        secondaryCooldownCoroutine = StartCoroutine(SecondaryCooldownRoutine());
    }

    private IEnumerator SecondaryCooldownRoutine()
    {
        secondaryCd.gameObject.SetActive(true);
        while (Time.time < secondaryCooldownEndsAt)
        {
            secondaryCd.fillAmount = (secondaryCooldownEndsAt - Time.time) / secondaryCooldownDuration;
            yield return null;
        }

        secondaryCd.gameObject.SetActive(false);
        secondaryCooldownCoroutine = null;
    }

    public void StartCooldown(float currentCooldown, float maxCooldownDuration)
    {
        if (boundMove != null && boundMove.IsStoredMove) return;
        if (cooldownCoroutine != null)
            StopCoroutine(cooldownCoroutine);

        cooldownEndsAt = Time.time + Mathf.Max(0, currentCooldown);
        cooldownDuration = Mathf.Max(0.01f, maxCooldownDuration);
        if (currentCooldown <= 0)
        {
            cooldownCoroutine = null;
            SetCooldownVisual(cdLine, cdBg, cdText, 0, cooldownDuration);
            return;
        }
        cooldownCoroutine = StartCoroutine(CooldownRoutine(cooldownDuration));
    }

    private IEnumerator CooldownRoutine(float maxCooldownDuration)
    {
        while (Time.time < cooldownEndsAt)
        {
            SetCooldownVisual(cdLine, cdBg, cdText, cooldownEndsAt - Time.time, maxCooldownDuration);
            yield return null;
        }

        SetCooldownVisual(cdLine, cdBg, cdText, 0, maxCooldownDuration);
        cooldownCoroutine = null;
    }
}
