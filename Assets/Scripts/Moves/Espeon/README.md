# Espeon

Reference: https://unite-db.com/pokemon/espeon, including detailed RSB formulas,
`Assets/Moves/Dumps/espeon_skills.json`, `eevee_skills.json`, and the corresponding
`age_196_*` AGE declarations. Implementation follows the user's clarified rules.

## Integration

- Seven move factories/assets, passive and basic-attack factories, registry,
  Addressables and network-prefab registrations. Existing enum values are preserved.
- Learn levels 1/2, 4/6, Unite 8 and upgrades 10/12 (serialized zero-based).
  Existing model, animator, selection asset and stat arrays are preserved.
- `PrefabMove` / `PrefabMoveEffect` own aiming, spawning and cleanup. Espeon casts
  commit cooldowns on server acceptance, with a separate Future Sight bonus cast
  that neither overwrites nor restarts Move 1's normal cooldown.
- `EspeonRuntime` is server-owned through `MovesController`, even for remote-owned
  actors and bots. It tracks the passive, blast ammunition, mark interactions and
  the five-Eeveelution team's 1% Sp. Attack bonus. No replacement combat controller.
- Optional original-source metadata on `StatusEffect` and `StatChange` supports
  retaliation. Shared/new server effects populate it; owner-authored legacy RPCs
  can use sender attribution. Unattributed environmental conditions are negated
  but do not invent a retaliation target. Periodic damage carries its own source.
- Magic Bounce also blocks slows, burn, poison and freeze. It grants 0.45s
  immunity with a 35s cooldown and has no retaliation range limit. This is not
  general damage reflection: the incoming attack's damage can still apply.
- Evolved boosted beams are ready on evolution/respawn and recharge 5s after
  firing; the owner's generic gauge fills from empty to ready using the replicated
  server deadline. Espeon hides the discrete attack counter; Eevee retains it.
  Beams pierce targets and grant one Stored Power blast per enemy hit. Eevee
  uses the three-attack sequence and the authorized Sylveon damage fallback: the
  supplied configuration references effects but contains no boosted damage formula.
- Stored Power generates one blast every 2s, ignoring CDR, starts empty and
  requires ammunition for a normal cast. Target order is own Future Sight mark,
  enemy players, then wild Pokémon. The selected highest-priority pool absorbs
  the entire volley; it never spills onto wild Pokémon after a player dies.
  The bonus cast fires eight blasts without spending regular stocks and heals
  150 only per successful impact. Upgrade bonus damage checks pre-impact HP.
- Future Sight is cleansable, follows its target, and explodes after 2s. Qualifying
  hits reduce the move cooldown by 0.8s, never the explosion countdown. Own KOs of
  marked players grant the separate 5s bonus opportunity. Future Sight+ amplifies
  outgoing damage against its marked target by 15%. Wild damage is capped at 1800.
- Psyshock and Swift fly through their chosen focal point and onward to maximum
  distance, piercing enemies and hitting each victim once per individual projectile.
  Five Psyshock hits stun; repeat damage is tracked per victim across projectiles.
  Guided bolts stop steering once they pierce their marked target, continuing forward.
  Future Sight can guide a Psyshock volley when its aimed
  focal point overlaps the mark. Bonus Psyshock replaces direct hits with
  convergence explosions, without increasing damage coefficients.
- Psybeam splits once into the forward half-plane, up to 75% of normal range,
  copying full original slow/stun durations. Maximum-HP components use the
  documented wild/player caps. Psychic Solare follows Espeon through Eject Button,
  captures late entrants, restores lifted models on cleanse/death and releases
  action locks on interruption/despawn. Battle items remain available during it.

## Tuning and presentation

All provisional geometry/speed/timing values are centralized as mutable static
fields in `EspeonMoveRules`. Distances use dump units / 1000. Current values:
basic range matches Growl's target aim (7.5); boosted beam 7.5 x 1; aim range 9 / convergence radius 1.5;
projectile travel 10, spacing 1, radius 0.3, speed 18 (Psyshock+ 24);
targeted/Stored Power range 7.5; Psybeam range 9 / width 1.7; Future radius 2;
Unite radius 6, capture 0.2s, explosion 1.4s, recovery 1.7s and shove 2.
AGE timing declarations are evidence, not a verified original-game execution trace.

`Tools/Espeon/Create Missing Move Assets` authors missing editable prefabs, baked
meshes, purple/pink psychic materials and particles, following Duraludon. It
preserves subsequent prefab edits. Stock orbs, boosted-ready jewel, bonus-cast
halo and alternate bonus icons expose the resource states. Runtime code creates
no meshes, primitives or LineRenderers. The supplied model animator is used;
AGE S12/S21/S22 map to Psybeam/Future Sight/Stored Power respectively.

Rebuild Addressables before testing built players or existing-content play mode.
All peers must use the same build because status records gained source metadata.
Presentation follows the project's fixed-roster RPC model, not late-join recovery.
Host/dedicated automated fixtures do not replace a real remote-client visual pass.

## Validation

- `Unite.Espeon.Tests`: factories, formulas, progression, art, registration,
  ammunition generation/spending and passive trigger classification.
- `Unite.Espeon.PlayMode.Tests`: host and dedicated-server passive retaliation,
  piercing boosted beams, resource priority/healing, Future Sight cleanse/KO/CDR
  and damage amplification, volleys, beam propagation and Unite displacement/
  late entrants/death cleanup.
- Manual: all four move combinations on host + remote client; evolve/respawn,
  hit/cleanse marks, use the temporary bonus cast while main cooldown runs,
  Eject during Unite, and check fog-of-war stock visibility and animation polish.
