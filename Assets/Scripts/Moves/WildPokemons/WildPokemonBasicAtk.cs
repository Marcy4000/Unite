using UnityEngine;

public class WildPokemonBasicAtk : BasicAttackBase
{
    private DamageInfo normalDamage = new DamageInfo(0, 1f, 0, 0, DamageType.Physical, DamageProprieties.IsBasicAttack);

    private WildPokemonAI wildPokemonAI;

    public void Initialize(WildPokemonAI wildPokemonAI, float range)
    {
        this.wildPokemonAI = wildPokemonAI;
        this.range = range;
        normalDamage.attackerId = wildPokemonAI.NetworkObjectId;
    }

    public override void Perform(bool wildPriority)
    {
        PokemonType priority = wildPriority ? PokemonType.Wild : PokemonType.Player;

        Transform closest = GetClosestGameobject();

        if (closest != null)
        {
            wildPokemonAI.transform.LookAt(closest);
            wildPokemonAI.transform.eulerAngles = new Vector3(0, wildPokemonAI.transform.eulerAngles.y, 0);
        }

        float offsetMultiplier = range / 2.5f;
        Vector3 offsetPosition = wildPokemonAI.transform.position + (wildPokemonAI.transform.forward * 1.377f * offsetMultiplier);
        GameObject[] hitColliders = Aim.Instance.AimInCircleAtPosition(offsetPosition, 1f * offsetMultiplier, AimTarget.NonAlly, Team.Neutral);

        foreach (var hitCollider in hitColliders)
        {
            Pokemon targetPokemon = hitCollider.GetComponent<Pokemon>();

            if (targetPokemon == null)
            {
                continue;
            }

            targetPokemon.TakeDamageRPC(normalDamage);
        }

        wildPokemonAI.AnimationManager.SetTrigger("BasicAttack");
    }

    private Transform GetClosestGameobject()
    {
        GameObject[] gameObjects = Aim.Instance.AimInCircleAtPosition(wildPokemonAI.transform.position, range, AimTarget.NonAlly, Team.Neutral);

        GameObject closest = null;
        float distance = Mathf.Infinity;
        Vector3 position = wildPokemonAI.transform.position;
        foreach (GameObject go in gameObjects)
        {
            float curDistance = Vector3.Distance(go.transform.position, position);
            if (curDistance < distance)
            {
                closest = go;
                distance = curDistance;
            }
        }
        return closest.transform;
    }

    public override void Update()
    {

    }
}
