using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AvailableQuickChatMessages", menuName = "Available Quick Chat Messages")]
public class AvailableQuickChatMessages : ScriptableObject
{
    [TextArea(3, 10)]
    public string[] quickChatMessages;
}