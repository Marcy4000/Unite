using JSAM;
using UnityEngine;
using UnityEngine.EventSystems;

public class UiSoundHandler : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] DefaultAudioSounds[] sounds;
    [SerializeField] private bool PlayOnClick;

    public void PlaySound(int id)
    {
        if (id < 0 || id >= sounds.Length)
        {
            Debug.LogError("Sound ID out of range");
            return;
        }

        if (AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.PlaySound(sounds[id]);
    }

    public void OnPointerClick(PointerEventData dt)
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        if (PlayOnClick)
            AudioManager.PlaySound(sounds[0]);
    }
}
