# Cresselia rework

## Identity and progression

Ranged Special Supporter. Existing damage formulas and normal-move cooldowns are
preserved; responsiveness and reliability change before additional balance tuning.
Displayed learning levels remain 1/3, 5, 7, 9, with upgrades added at 11/13.
Asset levels are zero-based. Offensive upgrades widen Psybeam / Moonblast's
explosion; support upgrades increase Moonlight's radius / Blessing's shield.

- **Lunar Escort:** 20% movement speed with another living ally within 6 units.
  Server scanning occurs every 0.2s. Death clears the bonus and its cached state.
- **Lunar Rhythm:** hitting an enemy player with a normal move, shielding another
  ally with Blessing, or actually healing another ally with Moonlight advances the
  6.5s boosted-attack timer by 1s, once per cast. No stored boosted attacks.
- Boosted basic attacks deal their original damage and apply 0.15s sleep **on
  impact**, not at launch. Their clock continues recovering while dead.
- Offensive moves release after 0.22s, with 0.18s recovery. Moonlight has a 0.15s
  placement windup and 0.18s recovery. Cleanses/shields apply immediately with
  0.2s recovery and do not root the user.
- A single input may be buffered during the last 0.18s of recovery. Cancellation,
  stun, death and move replacement clear/cancel pending actions. Committed casts
  spend cooldown once; interrupted windups do not spend it.

## Gameplay authority

`CresseliaMoveBase` owns local aiming and cast restrictions. Cresselia-specific
`MovesController` partials validate requests and run all hits/healing on the
server, without the shared `onObjectSpawned` event. Swept projectiles cannot
tunnel between frames; beam/explosion queries deduplicate root and child
colliders. The server guards cast intervals and derives upgrades from level.

Gameplay never waits for Addressables. Already committed projectiles, healing
fields and an ally's Blessing may finish after the caster dies. Dead recipients
cannot be healed or revived. Despawn cancels the character's remaining work.

Blessing leases a separate shield ID per recipient/cast. Conversion owns expiry:
at 3.5s it reads the remainder, removes the shield, then heals that amount. Shield
damage, recipient death, duplicate Cresselias and late VFX cannot duplicate or
redirect the conversion. Cleanses snapshot lists before removing timed
hindrances, burn/poison and removable timed stat debuffs; positive/permanent
effects are preserved.

Moonlight lasts 5s and has five explicit pulses at 0/1/2/3/4s, each healing 8% of
recipient max HP. Only actual healing to another ally grants its one Rhythm bonus.
Psybeam heals 5% of Cresselia's max HP per unique enemy **player**, not wilds.

Crescent Crash spends owner-side Unite energy at takeoff. A server watchdog owns
its 3s flight and 0.3s landing; recast uses the same landing path. The destination
locks at descent and is validated against the grounded walkmesh and terrain.
Invincibility lasts through touchdown. Damage/sleep happen there, not when the
landing animation begins. Client ascent/descent use server timestamps, without
uncancelled transform tweens. Recast availability overrides only the energy lock
in the HUD/input checks; it never ignores other action restrictions.

## Presentation and compatibility

Run **Tools > Cresselia > Create Missing Rework Prefabs** to author missing assets.
Like Duraludon, geometry, particles, materials and placeholder audio are baked
once in the editor and editable in Prefab Mode. The builder preserves existing
prefab layouts. Rework effects under `Cresselia/Rework` are **cosmetic local
Addressables**, not NetworkObjects; authoritative RPCs announce their phase and
start time. Missing/late visuals do not change gameplay. No runtime primitive,
mesh or LineRenderer construction occurs. Materials live in
`Assets/Materials/Cresselia`.

The old network effect prefabs/scripts remain for serialized compatibility, but
none of the rewritten Cresselia moves spawn them. Emboar Reversal has its own
`EmboarReversalProjectile` script and prefab reference; it no longer shares any
Cresselia type. Existing enum values, move asset GUIDs, and old network prefab
registrations are preserved. Rebuild Addressables for built-player testing.

Presentation follows the project's fixed-roster RPC model, not late-join state
restoration. Audio is editable placeholder sound, not a replacement for a final
sound-design pass. Do not retune damage/healing until host/client playtests.

## Validation

EditMode suite: `Unite.Cresselia.Tests` in `Assets/Editor/CresseliaTests`.

Manual host + remote-client checks (especially with latency/low FPS):

1. Learn all branches, Unite, and the 11/13 upgrades. Check cooldowns/metadata and
   that upgrading/replacing an aiming/executing move does not orphan locks.
2. Fire normal and boosted attacks at moving targets. Sleep must start at impact.
   Weave all four builds, hold/release a buffered move, then cancel/stun/die during
   windup and recovery. No ghost casts or permanent movement/action restrictions.
3. Hit several enemy players plus wilds with Psybeam. Damage/control happen once
   per target; healing counts players only; Rhythm advances once per cast.
4. Fire Aurora/Moonblast on a remote owner, through close/fast-moving targets and
   multi-collider targets. Check hit order, splash radius and source-specific
   refresh. Delay visual loading: the hit must still work.
5. Cleanse multiple hindrances, burn/poison and slows on a host and remote ally.
   Permanent effects/buffs must survive. No ally selected means self-only use.
6. Test full/partial/broken Blessing shields, recipient death, cleanse and two
   Cresselias shielding the same ally. Conversion removes only its own shield,
   heals only its remainder, and never revives. Removing VFX must change nothing.
7. Enter/leave Moonlight between pulses, heal full/low-HP allies and die as caster.
   There are exactly five pulses; no dead recipients heal or grant Rhythm.
8. Recast and auto-land Crescent Crash beside/on walls and uneven terrain. Energy
   is spent once, the HUD permits recast, the footprint freezes on descent and
   damage/sleep align with touchdown. Reset/death/despawn must clean protection,
   flying, ground snapping, input, scoring and movement state.
9. Kill/respawn beside the same allies: Escort must return. Repeated matches must
   not retain effects, callbacks, asset handles or shield ID leases.
10. Regression: Emboar Reversal retains its growing beam, damage/stun and upgrade
    shield, with no Cresselia component or healing dependency.
