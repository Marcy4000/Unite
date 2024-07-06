using UnityEngine;
using UnityEngine.UI;

public class MapGoalzoneIcon : MonoBehaviour
{
    [SerializeField] private Image goalzoneIcon, chargeSprite;
    [SerializeField] private Sprite blueCharge, orangeCharge;
    [SerializeField] private Sprite[] backgrounds;

    private GoalZone goalZone;

    private MinimapIcon minimapIcon;

    public MinimapIcon MinimapIcon => minimapIcon;

    public void Initialize(GoalZone goalZone)
    {
        minimapIcon = GetComponent<MinimapIcon>();
        this.goalZone = goalZone;
        UpdateGoalZoneIcon();
        chargeSprite.sprite = goalZone.OrangeTeam ? orangeCharge : blueCharge;
        chargeSprite.gameObject.SetActive(false);
        minimapIcon.SetTarget(goalZone.transform);

        goalZone.onGoalZoneDestroyed += (id, lane) => { HideIcon(); };
    }

    private void Update()
    {
        UpdateGoalZoneIcon();
    }

    private void UpdateGoalZoneIcon()
    {
        if (goalZone.IsActive)
        {
            goalzoneIcon.sprite = goalZone.OrangeTeam ? backgrounds[1] : backgrounds[0];
        }
        else
        {
            goalzoneIcon.sprite = goalZone.OrangeTeam ? backgrounds[3] : backgrounds[2];
        }
    }

    public void HideIcon()
    {
        goalzoneIcon.gameObject.SetActive(false);
        chargeSprite.gameObject.SetActive(false);
    }

    public void ShowIcon()
    {
        goalzoneIcon.gameObject.SetActive(true);
        chargeSprite.gameObject.SetActive(true);
    }

    public void SetVisibility(bool isVisible)
    {
        goalzoneIcon.gameObject.SetActive(isVisible);
        chargeSprite.gameObject.SetActive(isVisible);
    }
}
