using JSAM;
using Unity.Netcode;
using UnityEngine;

public enum BerryType : byte
{
    HealBerry,
    SpeedBerry
}

public class MapBerry : NetworkBehaviour
{
    [SerializeField] private GameObject berryModel;
    [SerializeField] private BerryType berryType;
    [SerializeField] private Vision vision;

    [Space]

    [SerializeField] private float firstSpawnTime = 600f;
    [SerializeField] private float respawnCooldown;
    [SerializeField] private float despawnTime;

    private float timer;
    private bool spawnedFirstTime = false;

    private NetworkVariable<bool> isCollected = new NetworkVariable<bool>(false);

    private int healAmount = 1500;
    private StatChange speedBoost = new StatChange(30, Stat.Speed, 3f, true, true, true, 0);

    public override void OnNetworkSpawn()
    {
        isCollected.OnValueChanged += OnIsCollectedChanged;
        vision.IsVisible = true;
        vision.SetVisibility(false);
        if (IsServer)
        {
            isCollected.Value = true;
        }
    }

    private void OnIsCollectedChanged(bool previousValue, bool newValue)
    {
        berryModel.SetActive(!newValue);

        if (IsServer && newValue)
        {
            timer = respawnCooldown;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || isCollected.Value) return;

        if (other.TryGetComponent(out PlayerManager player))
        {
            switch (berryType)
            {
                case BerryType.HealBerry:
                    if (player.Pokemon.IsHPFull()) return;

                    player.Pokemon.HealDamage(healAmount);
                    break;
                case BerryType.SpeedBerry:
                    player.Pokemon.AddStatChange(speedBoost);
                    break;
                default:
                    break;
            }
            PlaySoundRPC();
            isCollected.Value = true;
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PlaySoundRPC()
    {
        switch (berryType)
        {
            case BerryType.HealBerry:
                AudioManager.PlaySound(DefaultAudioSounds.Play_UI_InGame_Fruit_Hp, transform);
                break;
            case BerryType.SpeedBerry:
                AudioManager.PlaySound(DefaultAudioSounds.Play_UI_InGame_Fruit_Speed, transform);
                break;
            default:
                break;
        }
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        if (despawnTime > 0f && GameManager.Instance.GameTime <= despawnTime)
        {
            respawnCooldown = Mathf.Infinity;
            return;
        }

        if (GameManager.Instance.GameTime <= firstSpawnTime && firstSpawnTime != Mathf.NegativeInfinity)
        {
            isCollected.Value = false;
            spawnedFirstTime = true;
            firstSpawnTime = Mathf.NegativeInfinity;
        }

        if (!spawnedFirstTime || !isCollected.Value)
        {
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            isCollected.Value = false;
        }
    }

    public void DespawnBerry(bool canRespawn)
    {
        if (!IsServer)
        {
            return;
        }

        isCollected.Value = true;
        if (!canRespawn)
        {
            timer = Mathf.Infinity;
        }
    }
}
