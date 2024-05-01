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

    private bool isShowingKill;

    private Queue<KillInfo> killQueue = new Queue<KillInfo>();

    private void Start()
    {
        holder.SetActive(false);
    }

    public void EnqueueKill(KillInfo killInfo)
    {
        killQueue.Enqueue(killInfo);
    }

    private void ShowKill(KillInfo killInfo)
    {
        background.sprite = killInfo.orangeTeam ? orangeBG : blueBG;
        // Set the image to the killer's avatar
        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[killInfo.info.attackerId].GetComponent<Pokemon>();
        leftPreview.sprite = attacker.Portrait;
        rightPreview.sprite = killInfo.killed.Portrait;

        StartCoroutine(KillAnimation());
    }

    private void Update()
    {
        if (!isShowingKill)
        {
            if (killQueue.Count > 0)
            {
                ShowKill(killQueue.Dequeue());
            }
        }
    }

    private IEnumerator KillAnimation()
    {
        holder.SetActive(true);
        isShowingKill = true;
        yield return new WaitForSeconds(2f);
        holder.SetActive(false);
        isShowingKill = false;
    }
}

public class KillInfo
{
    public DamageInfo info;
    public bool orangeTeam;
    public Pokemon killed;

    public KillInfo(DamageInfo info, bool orangeTeam, Pokemon killed)
    {
        this.info = info;
        this.orangeTeam = orangeTeam;
        this.killed = killed;
    }
}