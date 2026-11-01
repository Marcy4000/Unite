using System;
using System.Collections.Generic;

public static class CombatKitFactory
{
    public readonly struct AssetContract
    {
        public string Path { get; }
        public Type Component { get; }
        public AssetContract(string path, Type component) { Path = path; Component = component; }
    }

    public static IEnumerable<AssetContract> RequiredAssets
    {
        get
        {
            yield return new AssetContract(GlaceonCharacterRuntime.BasicPath, typeof(HomingProjectile));
            yield return new AssetContract(GlaceonCharacterRuntime.StarPath, typeof(HomingProjectile));
            yield return new AssetContract(GlaceonCharacterRuntime.TailPath, typeof(HomingProjectile));
            yield return new AssetContract(GlaceonCharacterRuntime.HolderPath, typeof(IciclesHolder));
            yield return new AssetContract("Assets/Prefabs/Objects/Moves/Glaceon/FreezeDryProjectile.prefab", typeof(FreezeDryProjectile));
            yield return new AssetContract("Assets/Prefabs/Objects/Moves/Glaceon/IcicleSpear.prefab", typeof(IcicleSpearHitbox));
            yield return new AssetContract("Assets/Prefabs/Objects/Moves/Glaceon/GlaceonUnite.prefab", typeof(GlaceUniteArea));
            foreach (CinderCast kind in Enum.GetValues(typeof(CinderCast)))
                yield return new AssetContract(CinderMoveRules.Path(kind), typeof(CinderMoveObject));
        }
    }

    public static bool Supports(Pokemon pokemon)
    {
        if (pokemon == null || pokemon.BaseStats == null) return false;
        return pokemon.BaseStats.Passive == AvailablePassives.ZoroarkPassive
            || pokemon.BaseStats.Passive == AvailablePassives.GlaceonPassive
            || pokemon.BaseStats.Passive == AvailablePassives.CinderPassive;
    }

    public static CombatCharacterRuntime Create(PlayerManager actor)
    {
        switch (actor.Pokemon.BaseStats.Passive)
        {
            case AvailablePassives.ZoroarkPassive: return new ZoroarkCharacterRuntime(actor);
            case AvailablePassives.GlaceonPassive: return new GlaceonCharacterRuntime(actor);
            case AvailablePassives.CinderPassive: return new CinderaceCharacterRuntime(actor);
            default: return new CombatCharacterRuntime(actor);
        }
    }
}
