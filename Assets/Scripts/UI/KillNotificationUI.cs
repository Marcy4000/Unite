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
    [SerializeField] Sprite[] backgrounds;

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
        // Set the image to the killer's avatar
        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[killInfo.info.attackerId].GetComponent<Pokemon>();

        background.sprite = ChooseBackground(attacker, killInfo.killed);

        leftPreview.sprite = attacker.Portrait;
        rightPreview.sprite = killInfo.killed.Portrait;

        StartCoroutine(KillAnimation());
    }

    private Sprite ChooseBackground(Pokemon attacker, Pokemon killed)
    {
        attacker.TryGetComponent(out PlayerManager attackerPlayer);
        killed.TryGetComponent(out PlayerManager killedPlayer);

        if (attackerPlayer != null && killedPlayer != null)
        {
            return attackerPlayer.OrangeTeam ? backgrounds[0] : backgrounds[1];
        }
        else if (attackerPlayer != null && killedPlayer == null)
        {
            return attackerPlayer.OrangeTeam ? backgrounds[2] : backgrounds[3];
        }
        else if (attackerPlayer == null && killedPlayer != null)
        {
            return killedPlayer.OrangeTeam ? backgrounds[4] : backgrounds[5];
        }
        else
        {
            return backgrounds[2];
        }
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
    public Pokemon killed;

    public KillInfo(DamageInfo info, Pokemon killed)
    {
        this.info = info;
        this.killed = killed;
    }
}