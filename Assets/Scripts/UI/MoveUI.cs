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
            cooldownEndsAt = 0;
            secondaryCooldownEndsAt = 0;
        }
        boundMove = move;
        RenderCooldowns();
    }

    private void LateUpdate()
    {
        RenderCooldowns();
    }

    private void OnEnable()
    {
        RenderCooldowns();
    }

    private void RenderCooldowns()
    {
        float secondaryRemaining = Mathf.Max(0, secondaryCooldownEndsAt - Time.time);
        secondaryCd.gameObject.SetActive(secondaryRemaining > 0);
        secondaryCd.fillAmount = Mathf.Clamp01(secondaryRemaining / Mathf.Max(0.01f, secondaryCooldownDuration));
        RenderStoredUses();
        if (boundMove != null && boundMove.IsStoredMove) return;
        SetCooldownVisual(cdLine, cdBg, cdText,
            secondaryRemaining > 0 ? 0 : cooldownEndsAt - Time.time, cooldownDuration);
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
        SetCooldownVisual(cdLine, cdBg, cdText,
            secondaryCooldownEndsAt > Time.time ? 0 : boundMove.StoredUseCooldownRemaining, useDuration);
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
        secondaryCooldownEndsAt = Time.time + Mathf.Max(0, cdDuration);
        secondaryCooldownDuration = Mathf.Max(0.01f, cdDuration);
        RenderCooldowns();
    }

    public void StartCooldown(float currentCooldown, float maxCooldownDuration)
    {
        if (boundMove != null && boundMove.IsStoredMove) return;
        cooldownEndsAt = Time.time + Mathf.Max(0, currentCooldown);
        cooldownDuration = Mathf.Max(0.01f, maxCooldownDuration);
        RenderCooldowns();
    }
}
