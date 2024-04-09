using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class KillNotificationUI : MonoBehaviour
{
    [SerializeField] GameObject holder;
    [SerializeField] Image background;
    [SerializeField] Image leftPreview, rightPreview;
    [SerializeField] Sprite orangeBG, blueBG;

    private void Start()
    {
        holder.SetActive(false);
    }

    public void ShowKill(DamageInfo info, bool orangeTeam, Pokemon killed)
    {
        background.sprite = orangeTeam ? orangeBG : blueBG;
        // Set the image to the killer's avatar
        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].GetComponent<Pokemon>();
        leftPreview.sprite = attacker.Portrait;
        rightPreview.sprite = killed.Portrait;

        StartCoroutine(KillAnimation());
    }

    private IEnumerator KillAnimation()
    {
        holder.SetActive(true);
        yield return new WaitForSeconds(2f);
        holder.SetActive(false);
    }
}
