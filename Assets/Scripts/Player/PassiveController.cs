using Unity.Netcode;

public class PassiveController : NetworkBehaviour
{
    private PlayerManager playerManager;
    private Pokemon pokemon;

    private PassiveBase passive;

    public PassiveBase Passive => passive;

    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
        pokemon = GetComponent<Pokemon>();
    }

    public void LearnPassive()
    {
        if (!IsOwner)
        {
            return;
        }

        passive?.Dispose();
        passive = PassiveDatabase.GetPassive(pokemon.BaseStats.Passive);
        passive.Start(playerManager);
    }

    private void Update()
    {
        if (passive == null || !IsOwner)
        {
            return;
        }

        passive.Update();
    }

    public override void OnNetworkDespawn()
    {
        passive?.Dispose();
        passive = null;
        base.OnNetworkDespawn();
    }
}
