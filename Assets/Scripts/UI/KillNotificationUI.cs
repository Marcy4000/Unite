using DG.Tweening;
using JSAM;
using System.Collections;
using System.Collections.Generic;
using TMPro;
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
        if (killInfo != null)
            killQueue.Enqueue(killInfo);
    }

    private void ShowKill(KillInfo killInfo)
    {
        if (killInfo == null) return;
        ApplyPresentation(killInfo);
        DefaultAudioSounds? sound = ChooseSound(killInfo);
        if (sound.HasValue)
            AudioManager.PlaySound(sound.Value);

        StartCoroutine(KillAnimation());
    }

    private void ApplyPresentation(KillInfo killInfo)
    {
        background.sprite = ChooseBackground(killInfo);
        SetStreakText(killInfo);
        leftPreview.sprite = killInfo.AttackerPortrait;
        rightPreview.sprite = killInfo.VictimPortrait;
        notificationHolder.SetActive(!string.IsNullOrEmpty(killInfo.Notification));
        notificationText.text = killInfo.Notification;
    }

    private void SetStreakText(KillInfo killInfo)
    {
        if (killInfo.Streak < 2 || killInfo.AttackerType != PokemonType.Player || killInfo.VictimType != PokemonType.Player)
        {
            streakImage.gameObject.SetActive(false);
            return;
        }
        streakImage.gameObject.SetActive(true);
        int index = Mathf.Clamp(killInfo.Streak - 2, 0, 3);
        streakImage.sprite = killInfo.AttackerIsAlly ? blueKoSprites[index] : orangeKoSprites[index];
    }

    private Sprite ChooseBackground(KillInfo killInfo)
    {
        if (killInfo.AttackerIsPlayer && killInfo.VictimIsPlayer)
        {
            return killInfo.AttackerIsAlly ? backgrounds[1] : backgrounds[0];
        }
        else if (killInfo.AttackerIsPlayer && !killInfo.VictimIsPlayer)
        {
            return killInfo.AttackerIsAlly ? backgrounds[3] : backgrounds[2];
        }
        else if (!killInfo.AttackerIsPlayer && killInfo.VictimIsPlayer)
        {
            return killInfo.VictimIsAlly ? backgrounds[5] : backgrounds[4];
        }
        else
        {
            return backgrounds[2];
        }
    }

    private DefaultAudioSounds? ChooseSound(KillInfo killInfo)
    {
        if (killInfo.AttackerIsPlayer && killInfo.VictimIsPlayer)
        {
            if (killInfo.AttackerIsAlly)
            {
                switch (killInfo.Streak)
                {
                    case 2:
                        return DefaultAudioSounds.Play_UI_DoubleKillr;
                    case 3:
                        return DefaultAudioSounds.Play_UI_TripleKillr;
                    case 4:
                        return DefaultAudioSounds.Play_UI_QuadraKillr;
                    case 5:
                        return DefaultAudioSounds.Play_UI_PentaKillr;
                    default:
                        return killInfo.Streak > 5 ? DefaultAudioSounds.Play_UI_PentaKillr : DefaultAudioSounds.Play_UI_SingleKillr;
                }
            }
        }
        else if (killInfo.AttackerIsPlayer && !killInfo.VictimIsPlayer)
        {
            if (killInfo.VictimType == PokemonType.Objective)
            {
                return DefaultAudioSounds.Play_UI_Boss_Defeat;
            }
        }
        else if (!killInfo.AttackerIsPlayer && killInfo.VictimIsPlayer)
        {
            return DefaultAudioSounds.Play_UI_CheckPoint_CommonEvent;
        }
        return null;
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
    public Sprite AttackerPortrait { get; }
    public Sprite VictimPortrait { get; }
    public PokemonType AttackerType { get; }
    public PokemonType VictimType { get; }
    public bool AttackerIsPlayer { get; }
    public bool VictimIsPlayer { get; }
    public bool AttackerIsAlly { get; }
    public bool VictimIsAlly { get; }
    public int Streak { get; }
    public string Notification { get; }

    public KillInfo(Pokemon attacker, Pokemon killed, int streak, string killNotification, Team localTeam)
    {
        AttackerPortrait = attacker.Portrait;
        VictimPortrait = killed.Portrait;
        AttackerType = attacker.Type;
        VictimType = killed.Type;
        AttackerIsPlayer = attacker.TryGetComponent<PlayerManager>(out _);
        VictimIsPlayer = killed.TryGetComponent<PlayerManager>(out _);
        Team attackerTeam = attacker.TeamMember.Team;
        AttackerIsAlly = attackerTeam == localTeam;
        VictimIsAlly = killed.TeamMember.Team == localTeam;
        Streak = streak;
        string teamFriendlyName = AttackerIsAlly ? "Ally" : "Opposing";
        Notification = (killNotification ?? string.Empty)
            .Replace("{teamName}", attackerTeam.ToString())
            .Replace("{teamFriendlyName}", teamFriendlyName)
            .Replace("{!teamFriendlyName}", AttackerIsAlly ? "Opposing" : "Ally");
    }
}
