using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UniteMoveUI : MonoBehaviour
{
    [SerializeField] private Image moveIcon, chargeIndicator, bg;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private TMP_Text cdText, moveName;
    [SerializeField] private Sprite notReady, ready;
    private MovesController movesController;
    private int uniteMaxCharge;

    private void Start()
    {
        lockIcon.SetActive(true);
        cdText.gameObject.SetActive(false);
        moveIcon.gameObject.SetActive(false);
        chargeIndicator.fillAmount = 0;
    }

    public void AssignController(MovesController controller)
    {
        movesController = controller;
    }

    public void Initialize(MoveAsset move)
    {
        moveIcon.gameObject.SetActive(true);
        moveIcon.sprite = move.icon;
        moveName.text = MoveDatabase.GetMove(move.move).name;
        uniteMaxCharge = move.uniteEnergyCost;
    }

    private void Update()
    {
        if (movesController == null) return;
        
        if (uniteMaxCharge > 0)
        {
            float chargePercentage = (float) movesController.UniteMoveCharge / uniteMaxCharge;
            cdText.text = $"{Mathf.FloorToInt(chargePercentage * 100)}%";
            chargeIndicator.fillAmount = chargePercentage;
        }

        if (movesController.UniteMoveCharge == uniteMaxCharge)
        {
            lockIcon.SetActive(false);
            cdText.gameObject.SetActive(false);
            bg.sprite = ready;
        }
        else
        {
            lockIcon.SetActive(true);
            cdText.gameObject.SetActive(true);
            bg.sprite = notReady;
        }
    }
}
