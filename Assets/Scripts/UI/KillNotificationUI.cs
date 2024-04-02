using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KillNotificationUI : MonoBehaviour
{
    [SerializeField] GameObject holder;
    [SerializeField] Image background;
    [SerializeField] Sprite orangeBG, blueBG;

    private void Start()
    {
        holder.SetActive(false);
    }

    public void ShowKill(DamageInfo info, bool orangeTeam)
    {
        background.sprite = orangeTeam ? orangeBG : blueBG;
        // Set the image to the killer's avatar

        StartCoroutine(KillAnimation());
    }

    private IEnumerator KillAnimation()
    {
        holder.SetActive(true);
        yield return new WaitForSeconds(2f);
        holder.SetActive(false);
    }
}
