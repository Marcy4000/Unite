using DG.Tweening;
using JSAM;
using TMPro;
using UnityEngine;

public class DraftTimerUI : MonoBehaviour
{
    [SerializeField] private GameObject blueBG, orangeBG, mixedBG;
    [SerializeField] private TMP_Text messageText, timerText;

    private int lastValue;

    public void DoFadeIn(byte bgID)
    {
        switch (bgID)
        {
            case 0:
                DoFadeIn(blueBG);
                break;
            case 1:
                DoFadeIn(orangeBG);
                break;
            case 2:
                DoFadeIn(mixedBG);
                break;
            default:
                break;
        }
    }

    private void DoFadeIn(GameObject selectedBG)
    {
        blueBG.SetActive(false);
        orangeBG.SetActive(false);
        mixedBG.SetActive(false);

        selectedBG.SetActive(true);

        messageText.gameObject.SetActive(false);

        selectedBG.GetComponent<RectTransform>().localScale = new Vector3(0f, 1, 1);
        selectedBG.GetComponent<RectTransform>().DOScaleX(1f, 0.25f).onComplete += () => { messageText.gameObject.SetActive(true); };
    }

    public void DoFadeOut()
    {
        blueBG.GetComponent<RectTransform>().DOScaleX(0f, 0.25f).onComplete += () => { blueBG.SetActive(false); };
        orangeBG.GetComponent<RectTransform>().DOScaleX(0f, 0.25f).onComplete += () => { orangeBG.SetActive(false); };
        mixedBG.GetComponent<RectTransform>().DOScaleX(0f, 0.25f).onComplete += () => { mixedBG.SetActive(false); };
    }

    public void UpdateTimer(float time)
    {
        timerText.text = time.ToString("F0");

        int timeInt = int.Parse(timerText.text);

        if (timeInt != lastValue && timeInt <= 15)
        {
            timerText.color = new Color(255 / 255f, 111 / 255f, 6 / 255f, 1f);
            AudioManager.PlaySound(DefaultAudioSounds.Play_UI_Countdown);
            PlayPopInAnimation();
            PlayShadowEffect();
        }
        else if (timeInt != lastValue)
        {
            timerText.color = Color.black;
        }

        lastValue = timeInt;
    }

    public void UpdateMessage(string message)
    {
        messageText.text = message;
    }

    void PlayPopInAnimation()
    {
        timerText.transform.localScale = Vector3.one;

        timerText.transform.DOScale(new Vector3(1.2f, 1.2f, 1.2f), 0.15f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => timerText.transform.DOScale(Vector3.one, 0.1f));
    }

    void PlayShadowEffect()
    {
        TMP_Text shadowText = Instantiate(timerText, timerText.transform.parent);

        shadowText.color = timerText.color;
        shadowText.transform.localScale = timerText.transform.localScale;

        shadowText.transform.DOScale(new Vector3(1.9f, 1.9f, 1.9f), 0.6f);
        shadowText.DOFade(0, 0.6f).OnComplete(() => Destroy(shadowText.gameObject));
    }
}
