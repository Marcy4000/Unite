using UnityEngine;

public class StackMarkerView : MonoBehaviour
{
    [SerializeField] private GameObject[] markers;
    public void SetCount(int count)
    {
        for (int i = 0; i < markers.Length; i++) if (markers[i] != null) markers[i].SetActive(i < count);
    }
}
