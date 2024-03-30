using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AeosEnergy : MonoBehaviour
{
    [SerializeField] private bool isBigEnergy;
    private GameObject model;

    public bool IsBigEnergy { get => isBigEnergy; set => isBigEnergy = value; }

    private void Start()
    {
        model = transform.GetChild(0).gameObject;

        if (isBigEnergy)
        {
            model.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            playerManager.GainEnergy(isBigEnergy ? 5 : 1);
            Destroy(gameObject);
        }
    }
}
