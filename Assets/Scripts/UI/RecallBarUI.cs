using UnityEngine;
using UnityEngine.UI;

public class RecallBarUI : MonoBehaviour
{
    [SerializeField] private GameObject holder;
    [SerializeField] private Image recallBar;

    private void Start()
    {
        SetRecallBarActive(false);
    }

    public void SetRecallBar(float value)
    {
        recallBar.fillAmount = value;
    }

    public void SetRecallBarActive(bool active)
    {
        holder.SetActive(active);
    }
}
