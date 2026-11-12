# Lucario

Reference: https://unite-db.com/pokemon/lucario, detailed RSB descriptions/formulas
retrieved November 11, 2026. Geometry also references `Assets/Moves/Dumps/lucario_skills.json`
and `age-named-v2/3c44b23/age_448_*`. The dump contains alternate/older kits; AGE
declarations are not verified runtime schedules. No Aura Cannon AGE script was present.

## Integration and tuning

- Seven server-accepted `PrefabMove` effects, server-resolved basic attacks and a
  per-caster `LucarioRuntime`. Remote owners and server-owned bots use the same combat.
- Learnset: starting moves at 1/3, first branch 5, second branch 7, Aura Cannon 9,
  upgrades 11/13. Serialized levels are zero-based. Existing enum values are preserved.
- `LucarioMoveRules.cs` centralizes tunable geometry/timing and damage/healing formulas.
  `LucarioRuntime` owns Steadfast, markers, temporary attacks and Aura empowerment.
- `Tools/Characters/Lucario/Create Missing Move Assets` authors editable prefabs,
  materials, icons/previews, learnset and network/Addressables registrations. It creates
  missing assets, preserving later artist edits and the supplied model/controller/stats.
- Duraludon-style saved geometry/particles live in `Assets/Prefabs/Objects/Moves/Lucario`
  and `Assets/Resources/Effects/Lucario`. No match-time primitives/meshes/lines are built.
  Bones and fist aura follow the imported hand transforms. The supplied arrow FBX is
  normalized to actual gameplay dimensions with Lucario-owned additive materials.

## Agreed mechanics

- Steadfast triggers at 50% HP or below: `250% Attack + 50 × (level − 1) + 50`
  shield, +20% speed for 5s, 45s cooldown. Its cooldown survives death.
- Every third ordinary basic deals a 100% Attack hit and a separate 30% Attack
  area hit with a 0.5s throw. The follow-up suppresses duplicate on-hit item dispatch.
  Only basic attacks can crit. Close Combat's kick preserves the normal charge cycle;
  the temporary Extreme Speed spin likewise does not consume ordinary charge.
- Extreme Speed travels 5.5 units with a 2.5-unit-wide swept hitbox. It heals per
  distinct enemy, including wild Pokémon. Only the first eligible marked victim per
  dash supplies a reset; its marker is unavailable for 10s. Other markers remain ready.
  Bone Rush's initial strikes and Aura Cannon reapply markers. Availability is source-
  specific; owner-only ready/cooling rings respect team vision. After the dash, a
  circular 75% Attack basic is available for 1s. Upgrade: +7.5% Attack for 2s, refreshed
  rather than stacked.
- Power-Up Punch begins charging on press, reaches full charge after 1s, and cancels
  after 4s. During charge: 30% damage reduction, −15% speed, and upgraded unstoppable.
  Its indicator/dash grow linearly from 3 to 6 units, remaining 2.5 units wide.
  Release pierces, damaging each enemy once, preparing a boosted attack and shoving
  each victim 1 unit for 0.5s. Missing-HP bonus is 12%, capped at 1000 only for wild
  Pokémon. Cancel/timeout costs 1.4s cooldown without consuming Aura empowerment.
  Each damaging direct move tick, per enemy, reduces cooldown by 1s; basics/items/
  secondary damage do not. The piercing punch's own hits count. Charge input is
  request-scoped, including release/cancellation while its prefab loads.
- Bone Rush's three strikes have independent close/forward hit areas; overlap can
  deal two hits per strike. Its thrown bone shoves 2 units with a 0.4s stun. Recast
  during flight/after landing within 3s to visibly dash to and catch the bone; the
  catch damages nearby enemies. Upgrade resets move one's cooldown only on successful
  catch. The 9s cooldown begins on first activation and is not restarted by recast or
  expiry. The secondary HUD ring shows the recast window.
- Close Combat locks movement/facing and is unstoppable for 1.4s. Eleven punches
  land from 0.4 to 1.2s. Each punch heals `(11.5% Attack + 23) × distinct enemies hit`.
  Its following kick is available for 5s, deals a normal basic hit, and preserves
  basic charge. Upgrade increases each punch's damage.
- Meteor Mash has a 0.4s windup and 2-unit shove with 0.4s stun.
- Aura Cannon channels for 0.6s, then travels 40 units with radius 2, accelerating
  from 4 to 40 units/s. Collision ticks are spaced 0.2s per target and capped at four;
  close enemies can take four while a maximum-speed pass normally hits once.
  It reapplies Extreme Speed markers or empowers the next Power-Up Punch release
  (including a miss) by 20%, with a 1s shove. Grants six-second +30% speed/CDR and
  20% max-HP shield buffs. The authoritative Unite pool costs 90000 energy.
- Dashes use bounded wall crossing; enemy shoves respect walls. Death/despawn clears
  active effects, markers, charge, attack replacements, input, visuals and action
  restrictions, while keeping Steadfast's cooldown.

Estimated geometry/timing not specified by Unite-DB remains editable. Aura's velocity
curve and the special-basic windows are agreed approximations, not measured AGE data.
Presentation follows the project's fixed-roster RPC model, not late-join reconstruction.

## Validation

- `Unite.Lucario.Tests`: 25 EditMode cases for factories, assets, learnset,
  registrations, supplied animation states, indicators, formulas and tunability.
- `Unite.Lucario.PlayMode.Tests`: 30 real NGO host/dedicated-server cases for passive
  cooldown/death, per-caster markers, healing, piercing, overlap, temporary basics,
  actual cast dispatch/cooldowns and stale charge requests.
- November 11 regression run: 269/277 selected EditMode cases passed. The eight
  failures match the previously documented untouched GlaceUnite lifecycle reflection
  tests (six) and duplicated reserve-icon prefab expectations (two).
- Focused character regressions passed 208/208 EditMode and 96/96 host/server
  PlayMode cases (Lucario, Trevenant and Umbreon for PlayMode). Lucario's pooled
  effect IDs use a separate 5000–6799 range, avoiding other licenses' reservations.

Rebuild Addressables content before testing a built player or an editor using existing
content builds, and deploy matching client/server builds after the network changes.

Manual connected-client checks still needed: all input devices' hold/cancel flow;
wall landings/shoves; hand-following bone/fist timing in each animation; owner-only
marker visibility in bushes; charge/recast HUD; Aura aim-camera pan; interruptions,
death and disconnect during pending Addressables loads. No bot roster/loadouts were
changed; bots already assigned Lucario now wait for a fully charged punch.
