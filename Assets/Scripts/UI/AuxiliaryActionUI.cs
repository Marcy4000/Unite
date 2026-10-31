using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reusable optional HUD/mobile action. Hidden when no provider is registered.</summary>
public class AuxiliaryActionUI : MonoBehaviour
{
    [SerializeField] private GameObject content;
    [SerializeField] private Button button;
    [SerializeField] private Image icon, cooldownFill;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private GameObject lockImage;

    private void Awake() { button.onClick.AddListener(Activate); }
    private AuxiliaryAction Current => AuxiliaryAction != null && !AuxiliaryAction.IsDisposed ? AuxiliaryAction : null;
    public static AuxiliaryAction AuxiliaryAction { get; set; }
    private void Activate() { Current?.TryActivate(); }
    private void Update()
    {
        var action = Current;
        content.SetActive(action != null);
        if (action == null) return;
        icon.sprite = action.Icon;
        button.interactable = action.Available;
        cooldownFill.gameObject.SetActive(action.Remaining > 0);
        cooldownText.gameObject.SetActive(action.Remaining > 0);
        if (lockImage != null) lockImage.SetActive(!action.Available && action.Remaining <= 0);
        cooldownFill.fillAmount = action.Cooldown > 0 ? Mathf.Clamp01(action.Remaining / action.Cooldown) : 0;
        cooldownText.text = action.Remaining > 0 ? Mathf.CeilToInt(action.Remaining).ToString() : "";
    }
    private void OnDestroy() { button.onClick.RemoveListener(Activate); }
}
