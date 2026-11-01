using System;
using System.Collections.Generic;

public static class CombatKitFactory
{
    public readonly struct AssetContract
    {
        public string Path { get; }
        public Type Component { get; }
        public bool Networked { get; }
        public AssetContract(string path, Type component, bool networked = true) { Path = path; Component = component; Networked = networked; }
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
            foreach (CresseliaEffect effect in Enum.GetValues(typeof(CresseliaEffect)))
                yield return new AssetContract(CresseliaMoveRules.EffectPath(effect), typeof(CresseliaVisual), false);
            yield return new AssetContract(SylvMoveRules.BasicAttackPath, typeof(HomingProjectile));
            yield return new AssetContract(SylvMoveRules.BoostedAttackPath, typeof(HomingProjectile));
            yield return new AssetContract(SylvMoveRules.SwiftPath, typeof(SylvSwiftProjectile));
            yield return new AssetContract(SylvMoveRules.BabyDollEyesPath, typeof(HomingMoveEffect));
            yield return new AssetContract(SylvMoveRules.HyperVoicePath, typeof(HypervoiceHitbox));
            yield return new AssetContract(SylvMoveRules.MysticalFirePath, typeof(SylvMysticalFireEffect));
            yield return new AssetContract(SylvMoveRules.DrainingKissPath, typeof(SylvDrainingKissEffect));
            yield return new AssetContract(SylvMoveRules.CalmMindPath, typeof(SylvCalmMindEffect));
            yield return new AssetContract(SylvMoveRules.UnitePath, typeof(SylvFairyFrolicEffect));
            foreach (VaporEffect effect in Enum.GetValues(typeof(VaporEffect)))
                yield return new AssetContract(VaporMoveRules.EffectPath(effect), typeof(VaporVisual), false);
            yield return new AssetContract("Assets/Prefabs/Objects/Moves/Jolteon/JoltSwift.prefab", typeof(JoltSwiftProjectile));
            foreach (JoltEffect effect in Enum.GetValues(typeof(JoltEffect)))
                yield return new AssetContract(JoltMoveRules.EffectPath(effect), typeof(JoltEffectObject));
            foreach (FlareonCast kind in Enum.GetValues(typeof(FlareonCast)))
                if (kind != FlareonCast.Basic)
                    yield return new AssetContract(FlareonMoveRules.Path(kind), typeof(FlareonMoveObject));
        }
    }

    public static bool Supports(Pokemon pokemon)
    {
        return pokemon != null && SupportsDefinition(pokemon.BaseStats);
    }

    public static bool SupportsDefinition(PokemonBase definition) => definition != null
        && (definition.Passive == AvailablePassives.ZoroarkPassive
            || definition.Passive == AvailablePassives.GlaceonPassive
            || definition.Passive == AvailablePassives.CinderPassive
            || definition.Passive == AvailablePassives.CresseliaPassive
            || definition.Passive == AvailablePassives.SylveonPassive
            || definition.Passive == AvailablePassives.VaporeonPassive
            || definition.Passive == AvailablePassives.JolteonPassive
            || definition.Passive == AvailablePassives.FlareonPassive);

    public static CombatCharacterRuntime Create(PlayerManager actor)
    {
        switch (actor.Pokemon.BaseStats.Passive)
        {
            case AvailablePassives.ZoroarkPassive: return new ZoroarkCharacterRuntime(actor);
            case AvailablePassives.GlaceonPassive: return new GlaceonCharacterRuntime(actor);
            case AvailablePassives.CinderPassive: return new CinderaceCharacterRuntime(actor);
            case AvailablePassives.CresseliaPassive: return new CresseliaCharacterRuntime(actor);
            case AvailablePassives.SylveonPassive: return new SylveonCharacterRuntime(actor);
            case AvailablePassives.VaporeonPassive: return new VaporeonCharacterRuntime(actor);
            case AvailablePassives.JolteonPassive: return new JolteonCharacterRuntime(actor);
            case AvailablePassives.FlareonPassive: return new FlareonCharacterRuntime(actor);
            default: return new CombatCharacterRuntime(actor);
        }
    }
}
