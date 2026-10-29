using UnityEngine;

// Explicit scene bindings, authored once; no hierarchy searches during decisions.
public class BotArenaContext : MonoBehaviour
{
    public Vector3[] patrolPoints = new Vector3[0];
    public WildPokemonSpawner[] objectives = new WildPokemonSpawner[0];
    public static BotArenaContext Instance { get; private set; }
    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
