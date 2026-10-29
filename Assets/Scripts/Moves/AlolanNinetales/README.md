# Alolan Ninetales

Reference: https://unite-db.com/pokemon/ninetales, detailed RSB data retrieved
October 29, 2026, and `Assets/Moves/Dumps/vulpix_skills.json`.
Detailed notes take precedence over the older summary: Snow Warning is **4s**, not 8s.

## Tuning and agreed mechanics

`NinetalesMoveRules.cs` centralizes project-unit dimensions, animation timings,
damage formulas and status IDs. Levels in damage formulas are zero-based.

- Basic range 7, boosted splash radius 1.5; boosted attacks slow 30% for 0.75s.
- Snow Warning searches 7 units, prioritizes enemy players over wild Pokémon,
  and deliberately detects grass-hidden/invisible enemies. It does not hit
  invincible or untargetable targets.
  Acquisition places a fixed 1.5-unit-radius floor warning at the enemy's initial
  position. Its fill builds for 1s before damage, slow and snow are applied to all
  enemies still inside. Moving out dodges it, and other enemies can move into it.
  The 4s cooldown begins at warning placement; death/despawn cancel pending snow.
  Warning presentation is synchronized using the server start time on every peer.
- Boosted attacks and every damaging move/passive hit apply one source-specific
  snow stack. Both wind blasts, both Avalanche hits and all Blizzard field ticks
  count separately. Four stacks are consumed to freeze for 1s. Stacks refresh
  their four-second expiry and are cleansable (`SnowMarked`, a negative status).
  There is no additional post-freeze immunity window; shared unstoppable and
  hindrance-resistance handling still applies. Item damage never creates snow.
- Powder Snow and Dazzling Gleam use 7-unit, 60-degree cones.
- Avalanche casts up to 6.5 units, with a 3-unit initial corridor and a physical
  4-wide, 0.6-thick, 2.5-high wall. It blocks either team, uses a carving navigation
  obstacle, and permits wall-crossing traversal. After 2s it collapses in a
  2.5-unit radius. Blizzard does not destroy it early.
  Its collider explicitly includes Players/WildPokemons with a local layer
  override: the map's global Players/Wall collision pair is intentionally ignored
  for walkmesh movement, and must remain unchanged.
  Legacy `StartDash` temporarily overrides the controller's Wall mask at a higher
  priority, restoring its original mask/priority at dash end, teleport or despawn;
  authored wall-crossing traversals continue to bypass the collider directly.
- Icy Wind is 8 units long and 2.5 wide. Blizzard is 10 long and 4 wide. These are
  **full widths**, not radii. Blasts travel at 15 units/s with 0.25s separation.
  Shoves are 1.5 units, capped at the wind's maximum range. Blizzard's first blast
  penetrates walls, while its second stops at a physical Wall and leaves a
  2-unit frost field for 4s (eight ticks, 0.5s apart). Its initial blast also
  applies the agreed 35%/1s slow.
  Wild targets use the opt-in server-owned `WildPokemonAI.TryDisplace` path, not
  impulses on their normally kinematic Rigidbody. Shoves respect Farm-type
  navigation and physical walls, pause agent-driven movement/attacks, work through
  freezes, and restore the agent/body without returning stationary targets to
  their old standing point. Objectives and hindrance-immune targets stay immune.
- Aurora Veil dashes 4 units through walls and leaves a stationary 3.5-radius
  aurora at the landing point for 5s. Both Ninetales and allies receive 35%/40%
  damage reduction **only inside**. Only Ninetales inside its own veil receives
  10% movement speed and automatically boosted attacks. Independently, the caster
  receives 15% attack speed and healing on boosted hits for the full five seconds,
  including outside. A splash heals once per successful attack, not per victim.
  Leaving the field removes only area-dependent benefits. Upgraded cooldown is
  10s on both the owner and server (12s before upgrade).
- Snow Globe has an 8-unit radius. It checks the victim's existing Frozen status,
  deals the corresponding damage, **then** applies snow. A fourth-stack Unite hit
  cannot award itself the frozen-target damage bonus. Its cast is unstoppable;
  the documented speed/CDR/shield buffs last 6s.

## Integration and presentation

Learnset: 1/3 starters, evolution and first branch at 4, second branch at 6,
Unite at 9, upgrades at 11/13. Existing stats and evolution/model assets are
preserved. New move/passive/status enum values are appended, not reordered.

Normal moves use `PrefabMove` and authored server-owned `PrefabMoveEffect`s.
The opt-in `Ground` aim style retains a clamped placement offset for Avalanche;
existing directional moves still receive normalized directions. An opt-in
effective-cooldown method handles Aurora Veil's upgrade on the server.
`MovesController.Ninetales` owns passive ticking, independent homing-attack loads,
inside-veil replication and personal snow markers. Basic attack charge, launch
timing and damage are authoritative. Death/despawn cancel pending loads, clear
snow and fields, release modifiers/action locks, and destroy personal markers.

Vulpix uses its supplied `ani_atk1/atk2/spell1/spell2_bat_0037` states. Ninetales
uses the supplied `ani_atk1/atk3/spell1/spell1a/spell1b/spell2/spell2a/spell2b/spellg_bat_0038`
states through the shared animation manager. Timed playback and visual crossfades
do not require editing the imported controllers or enabling root motion.

Like Duraludon, effects consist of editable baked meshes, URP material copies and
particles in prefabs. No match-time meshes, primitives or LineRenderers are made.
Imported 60-degree cone and arrow aim meshes retain their child Y-face correction.
`Tools/Alolan Ninetales/Create Missing Move Assets` authors missing assets,
registers Addressables/network prefabs and fills an empty learnset, without
overwriting existing prefab layouts or model/controller/material work.

Rebuild Addressables content before testing built players or using an existing
content build. All peers must be rebuilt together because MovesController has
an additional replicated inside-veil flag. Presentation uses the project's
fixed-roster RPC lifecycle, not late-join restoration.

## Validation

- EditMode: `Unite.Ninetales.Tests` checks formulas, stack transitions, learnset,
  factories, icons/previews, registry/network hashes, wall dimensions, imported
  aim art and real animation state availability.
- PlayMode: `Unite.Ninetales.PlayMode.Tests` uses real NGO host and dedicated
  server fixtures for hidden-enemy acquisition, cleansing/expiration, snow/freeze
  ordering, cone targeting, Blizzard/Avalanche collision, independent Veil healing
  outside its area, one-heal-per-splash and death/despawn cleanup.
  Wall regressions use the actual Players layer with Players/Wall globally
  ignored, checking caster, ally, enemy, dash traversal and collapse. Wild shove
  regressions instantiate the real wild prefab on a Farm-agent NavMesh, including
  freezing second hits and recovery after thawing.
  Passive regressions verify zero early damage/snow, the one-second delay, fixed
  placement and dodging, multiple/entering enemies, ally exclusion, cooldown timing,
  and cancellation across death/respawn. EditMode checks the authored floor fill
  and its latency-adjusted buildup progress.
- Regress shared moves, Absol and Duraludon after changing shared adapters.

Manual host + remote-client checks still recommended: both move branches and
all learn levels; keyboard/controller/mobile aim and cancellation; attack-speed
animation scaling and buffered inputs; ally/enemy wall blocking and cross-wall
dashes; entering/leaving/expiring Veil; and death during async Addressables loads.
