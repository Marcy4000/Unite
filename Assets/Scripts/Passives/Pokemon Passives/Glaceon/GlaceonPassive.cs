using UnityEngine;

public class GlaceonPassive : PassiveBase
{
    private byte iciclesCount;
    private bool hasEvolved;

    private string resourcePath = "Assets/Prefabs/Objects/Passives/Glaceon/IciclesHolder.prefab";

    private float iciclesTimer = 5f;

    private IciclesHolder iciclesHolder;

    public byte IciclesCount => iciclesCount;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        iciclesCount = 0;
        hasEvolved = false;
        controller.Pokemon.OnEvolution += OnPokemonEvolution;
        iciclesTimer = 0f;
    }

    public void ResetTimer()
    {
        iciclesTimer = 5f;
    }   

    public void UpdateIciclesCount(byte iciclesCount)
    {
        if (!hasEvolved || iciclesCount > 8)
        {
            return;
        }

        byte oldValue = this.iciclesCount;
        this.iciclesCount = iciclesCount;

        if (oldValue < iciclesCount)
        {
            for (byte i = oldValue; i < iciclesCount; i++)
            {
                iciclesHolder.AddIcicleRPC();
            }
            ResetTimer();
        }
        else if (oldValue > iciclesCount)
        {
            for (byte i = oldValue; i > iciclesCount; i--)
            {
                iciclesHolder.RemoveIcicleRPC();
            }
        }
        else if (oldValue == iciclesCount)
        {
            ResetTimer();
        }
    }

    private void OnPokemonEvolution()
    {
        hasEvolved = true;
        playerManager.MovesController.onObjectSpawned += (iciclesHolder) =>
        {
            this.iciclesHolder = iciclesHolder.GetComponent<IciclesHolder>();
            Debug.Log("Icicles Holder spawned!");
        };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(resourcePath, playerManager.OwnerClientId);

        playerManager.HPBar.ShowGenericGuage(true);
        playerManager.HPBar.UpdateGenericGuageValue(iciclesTimer / 5f);
    }

    public override void Update()
    {
        if (!hasEvolved || iciclesHolder == null)
        {
            return;
        }

        iciclesHolder.transform.position = playerManager.transform.position;

        if (iciclesTimer > 0)
        {
            iciclesTimer -= Time.deltaTime;
        }

        if (iciclesTimer <= 0 && iciclesCount > 0)
        {
            UpdateIciclesCount(0);
        }

        playerManager.HPBar.UpdateGenericGuageValue(iciclesTimer / 5f);
    }
}
