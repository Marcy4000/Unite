using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DeathScreenUI : MonoBehaviour
{
    [SerializeField] TMP_Text timerText;

    public void UpdateTimerText(int time)
    {
        timerText.text = time.ToString();
    }
}
