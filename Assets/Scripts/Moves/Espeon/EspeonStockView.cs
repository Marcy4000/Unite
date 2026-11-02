using UnityEngine;

/// <summary>Authored orbiting blast stocks and a timed-boost jewel. Never constructs geometry.</summary>
public class EspeonStockView : MonoBehaviour
{
    [SerializeField] private GameObject[] stocks;
    [SerializeField] private GameObject readyJewel, empoweredRing;
    public void Present(int count, bool active, bool boostReady, bool empowered)
    {
        if (stocks != null) for (int i = 0; i < stocks.Length; i++) stocks[i].SetActive(active && i < count);
        if (readyJewel != null) readyJewel.SetActive(active && boostReady);
        if (empoweredRing != null) empoweredRing.SetActive(active && empowered);
        transform.localRotation = Quaternion.Euler(0, Time.time * 45, 0);
    }
}
