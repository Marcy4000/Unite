using DG.Tweening;
using JSAM;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class KillNotificationUI : MonoBehaviour
{
    [SerializeField] GameObject holder;
    [SerializeField] Image background;
    [SerializeField] Image leftPreview, rightPreview;
    [SerializeField] Image streakImage;
    [SerializeField] GameObject notificationHolder;
    [SerializeField] TMP_Text notificationText;
    [SerializeField] Sprite[] backgrounds;

    [SerializeField] Sprite[] orangeKoSprites;
    [SerializeField] Sprite[] blueKoSprites;

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
        SetStreakText(attacker, killInfo.killed);

        leftPreview.sprite = attacker.Portrait;
        rightPreview.sprite = killInfo.killed.Portrait;

        notificationHolder.SetActive(!string.IsNullOrEmpty(killInfo.killNotification));
        notificationText.text = FormatKillNotificationText(killInfo.killNotification, attacker);

        PlayCorrectSound(attacker, killInfo.killed);

        StartCoroutine(KillAnimation());
    }

    private string FormatKillNotificationText(string killNotification, Pokemon attacker)
    {
        if (string.IsNullOrEmpty(killNotification))
        {
            return "";
        }

        string newNotification = killNotification.Replace("{teamName}", attacker.TeamMember.Team.ToString());

        string teamFriendlyName = LobbyController.Instance.GetLocalPlayerTeam() == attacker.TeamMember.Team ? "Ally" : "Opposing";
        newNotification = newNotification.Replace("{teamFriendlyName}", teamFriendlyName);
        newNotification = newNotification.Replace("{!teamFriendlyName}", teamFriendlyName == "Ally" ? "Opposing" : "Ally");

        return newNotification;
    }

    private void SetStreakText(Pokemon attacker, Pokemon killed)
    {
        if (attacker.KillStreak < 2 || attacker.Type != PokemonType.Player || killed.Type != PokemonType.Player)
        {
            streakImage.gameObject.SetActive(false);
            return;
        }

        streakImage.gameObject.SetActive(true);

        attacker.TryGetComponent(out PlayerManager playerManager);

        int index = Mathf.Clamp(attacker.KillStreak - 2, 0, 3);

        streakImage.sprite = playerManager.CurrentTeam.Team == Team.Orange ? orangeKoSprites[index] : blueKoSprites[index];
    }

    private Sprite ChooseBackground(Pokemon attacker, Pokemon killed)
    {
        attacker.TryGetComponent(out PlayerManager attackerPlayer);
        killed.TryGetComponent(out PlayerManager killedPlayer);

        if (attackerPlayer != null && killedPlayer != null)
        {
            return attackerPlayer.CurrentTeam.Team == Team.Orange ? backgrounds[0] : backgrounds[1];
        }
        else if (attackerPlayer != null && killedPlayer == null)
        {
            return attackerPlayer.CurrentTeam.Team == Team.Orange ? backgrounds[2] : backgrounds[3];
        }
        else if (attackerPlayer == null && killedPlayer != null)
        {
            return killedPlayer.CurrentTeam.Team == Team.Orange ? backgrounds[4] : backgrounds[5];
        }
        else
        {
            return backgrounds[2];
        }
    }

    private void PlayCorrectSound(Pokemon attacker, Pokemon killed)
    {
        attacker.TryGetComponent(out PlayerManager attackerPlayer);
        killed.TryGetComponent(out PlayerManager killedPlayer);

        Team localPlayerTeam = LobbyController.Instance.GetLocalPlayerTeam();

        if (attackerPlayer != null && killedPlayer != null)
        {
            if (attackerPlayer.CurrentTeam.IsOnSameTeam(localPlayerTeam))
            {
                switch (attacker.KillStreak)
                {
                    case 2:
                        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_DoubleKillr);
                        break;
                    case 3:
                        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_TripleKillr);
                        break;
                    case 4:
                        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_QuadraKillr);
                        break;
                    case 5:
                        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_PentaKillr);
                        break;
                    default:
                        if (attacker.KillStreak > 5)
                        {
                            AudioManager.PlaySound(DefaultAudioSounds.Play_UI_PentaKillr);
                        }
                        else
                        {
                            AudioManager.PlaySound(DefaultAudioSounds.Play_UI_SingleKillr);
                        }
                        break;
                }
            }
        }
        else if (attackerPlayer != null && killedPlayer == null)
        {
            if (killed.Type == PokemonType.Objective)
            {
                AudioManager.PlaySound(DefaultAudioSounds.Play_UI_Boss_Defeat);
            }
        }
        else if (attackerPlayer == null && killedPlayer != null)
        {
            AudioManager.PlaySound(DefaultAudioSounds.Play_UI_CheckPoint_CommonEvent);
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
        isShowingKill = true;

        holder.SetActive(true);
        var rectTransform = holder.GetComponent<RectTransform>();

        rectTransform.localScale = Vector3.one * 1.7f;
        rectTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);

        yield return new WaitForSeconds(2f);

        yield return rectTransform.DOScale(Vector3.one * 1.7f, 0.5f).SetEase(Ease.InBack).WaitForCompletion();

        rectTransform.gameObject.SetActive(false);

        isShowingKill = false;
    }
}

public class KillInfo
{
    public DamageInfo info;
    public Pokemon killed;
    public string killNotification;

    public KillInfo(DamageInfo info, Pokemon killed, string killNotification)
    {
        this.info = info;
        this.killed = killed;
        this.killNotification = killNotification;
    }
}