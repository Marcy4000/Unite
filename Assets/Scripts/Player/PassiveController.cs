using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PassiveController : MonoBehaviour
{
    private PlayerManager playerManager;
    private Pokemon pokemon;

    private PassiveBase passive;

    public PassiveBase Passive => passive;

    private void Start()
    {
        playerManager = GetComponent<PlayerManager>();
        pokemon = GetComponent<Pokemon>();

        LearnPassive();
    }

    private void LearnPassive()
    {
        passive = PassiveDatabase.GetPassive(pokemon.BaseStats.Passive);
        passive.Start(playerManager);
    }

    private void Update()
    {
        passive.Update();
    }
}
