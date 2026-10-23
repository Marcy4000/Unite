# Flygon gameplay pass

Flygon remains a physical melee Defender. Learnset (player-facing): Bite/Dig at
1/2, Supersonic/Boomburst and Vibrava at 4, Earthquake/Sandstorm and Flygon at 7,
Cyclonic Sand Surge at 9, upgrades at 11/13. Serialized enum values and existing
prefab/script GUIDs are preserved. Basic attacks and their animations are untouched.

Move slots follow the existing assets, not upgrade order: MoveA is Bite then
Earthquake/Sandstorm; MoveB is Dig then Supersonic/Boomburst. The sound branch
upgrades at 11 and the ground branch at 13 despite their reversed slot order.

## Runtime

- `FlygonMoveRules` centralizes timings, damage, ranges and upgrades.
- `FlygonMoveBase` owns aiming, accepted-cast cooldowns, counted action leases,
  recovery and Dig/Unite recasts. A 0.2s input buffer applies only near ordinary
  cast recovery; stuns, cancellation, death and replacement clear it.
- `MovesController.Flygon` validates the learned branch, cooldown and cast state
  on the server. Every asynchronous request captures its own parameters, retains
  its Addressables handle, and is invalidated on death, cancellation or replacement.
  It never uses `onObjectSpawned`. Energy is spent on acceptance, not on expiry.
- Existing hitbox components now inherit `FlygonMoveObject`. Only the server
  applies gameplay. Target sets deduplicate colliders. Source-specific stat IDs
  refresh instead of accumulating every tick and remain reserved until lingering
  effects expire. Effect death/despawn releases owner controls and asset handles.
- Dig changes only model height, uses its own bounded wall-crossing traversal,
  tracks valid grounded exits and restores movement without changing `IsFlying`.
- Supersonic keeps its original traveling particle system (7 units/s, 1s life).
  Server queries sweep its leading edge; the full forward box is only an authored
  size reference, never an instant full-lane trigger. Each target is controlled once.
- Earthquake's first wave occurs on acknowledged landing, then at +0.6/+1.2s.
  Its independent field continues after the short landing recovery.
- Boomburst separately reduces Defense and Sp. Defense and uses bounded, wall-safe
  displacement. Earthquake slows refresh instead of stacking across waves.
- Upgrades: Supersonic 25% slow; Boomburst 4s shred; Earthquake one 8%-max-HP
  shield on final-wave player contact; Sandstorm 15% speed only inside the field.
- Unite forms in 0.65s, grants a 15%-max-HP shield and hindrance resistance instead
  of six seconds of invincibility, and decays at 3/4/5s. Each enemy is lifted once
  per cast. Early ending immediately restores controls; captured landing damage
  resolves afterward. Finishing grants 25% speed for 4s. These are tuning changes,
  not a claim of final multiplayer balance.
- The authored `Tornado` animation shrinks the model's Origin bone and has no
  trigger-based exit. Completion explicitly plays the normal idle animation;
  phase/despawn/death cleanup also exits the hidden pose locally on every peer,
  without enabling renderers hidden by fog or interrupting a later attack.

## Presentation

`Tools/Flygon/Upgrade Move Prefabs` migrates each prefab once, retaining original
art under `Graphics/OriginalArt`. It adds serialized query shapes and authored
boundaries, impact rings, sand ribbons and debris. Materials live in
`Assets/Materials/Flygon`; meshes in the move prefab folder's `Art` subfolder.
The old Sandstorm sphere and static debug indicators are retained but disabled.
The builder preserves subsequent prefab edits and registers only missing entries.
No geometry is constructed during matches. Refresh/rebuild Addressables content
before testing players that use an existing content build.

## Validation

EditMode: `Unite.Flygon.Tests` covers factories, upgrades, phase boundaries, damage
progression, authored/registered prefabs, original Supersonic particles, data
completeness, Dig's grounded fallback, model restoration and runtime art rules.
Slot and upgrade tests read the move assets and character learnset directly, so
they cannot pass by repeating an incorrect runtime slot assumption.

PlayMode: `Unite.Flygon.PlayMode.Tests` exercises the real Flygon animator over
frames, covering final-phase restoration, despawn without a final phase, direct
destruction, and cleanup after a subsequent attack has already started. These
isolated lifecycle tests do not replace host/remote-client combat validation.

Host + remote-client combat checklist (still required for balance/feel):

1. Test all four branches, rapid casts, queued press/release and cancel.
2. Evolve/replace Dig while underground or emerging; confirm model/root height,
   aim, movement, scoring and battle-item locks recover.
3. Use Dig near thick walls and map edges; emerge on valid ground. Repeat after
   interrupting a cast, death, respawn and delayed/failed Addressables loads.
4. Check Supersonic from several directions on the remote client: hit timing must
   follow the visible wave, not instantly hit the full telegraph.
5. Stand in each area before spawning it; use multi-collider targets and overlapping
   Flygons. Confirm once-per-target effects and nonstacking slows.
6. Test each upgrade, cleanses, Unstoppable/hindrance resistance and objectives.
7. End Unite during every power phase; die after formation; confirm one lift per
   enemy, captured landing damage and no remaining shield/action restrictions.

Distinctive move audio and final host/client balance tuning remain a separate pass.
