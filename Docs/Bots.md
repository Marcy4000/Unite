# Bot players

## Using bots

- Create a custom **Remoat Stadium** or **Theia Sky Ruins** blind-pick or draft lobby.
  Bots also support **Electrode Volleyball (3v3)**, **Psyduck Racing (six racers)**
  and **Unite Royale (four solo participants)** in custom lobbies.
- The host has **Add Bots** (fills empty slots) and **Clear Bots** buttons.
  Individual bot slots can also be kicked. Both buttons are authored and bound
  directly in `Assets/Scenes/LobbyScene.unity`, using the start-button styling;
  no buttons are generated at runtime.
- Bots are named Trainer 1, Trainer 2, etc. They do not join UGS, allocate Relay
  connections, submit matchmaking tickets, or receive human client IDs.
  Clearing/kicking and refilling reuses free display numbers, not participant IDs.
- A new human member clears the custom lobby's bots. Refill explicitly afterward.
  Changing to another map/type clears bots too. Human departure does not refill.
- Humans have priority over bot Pokemon choices, including after bots reveal
  their choices near the deadline. Final choices are committed before preview.
- Human lane choices remain editable during preparation. Bots fill gaps around
  those choices; assignments are frozen immediately before loading the match.
  Enemy lane declarations remain private.

## Supported gameplay

The configured pool is Absol, Cinderace, Cresselia, Duraludon, Emboar, Flareon,
Flygon, Glaceon, Jolteon, Marshadow, Meowstic, Sylveon and Vaporeon. Bots use normal
movement, combat, cooldowns, move learning, scoring, progression and respawn.
Their FSM covers travel, farming, fights, scoring, retreat/recovery, objectives,
death, and match end. Bot statistics are included in normal match results.

Navigation uses the existing baked NavMesh to plan corners, and PlayerMovement
to execute movement with the regular walkmesh and status restrictions. Farming
uses explicit scene-authored `BotMapContext` camp/team/lane bindings, including
opening Aipoms/Bunnelbys, not hierarchy-name inference at runtime or the lane
picker's UI coordinates. Living camps are reserved between allied bots; empty or
temporarily unreachable camps are not waiting destinations.
Perception queries visibility for the bot's team instead of borrowing the host's
rendered visibility.

Theia's two spawn barriers carve temporary NavMesh obstacles until their team's
main (tier-0) goal becomes active. The central barrier is present only while
Rayquaza is alive. `DynamicMapBarrier` keeps rendering, physical collision and
navigation in sync with existing replicated goal/spawner state; these walls are
not permanently baked into the navigation mesh.

Each bot replicates its own server-clock respawn deadline. Allied/enemy HUDs and
the scoreboard read the avatar's countdown rather than its shared host owner ID.
Respawn explicitly teleports to the team's spawn, cancels stale movement/tweens,
and resets the brain's navigation state, even if the parked death position drifts.

Junglers gank after their first clear (or a bounded opening window), and bots
periodically seek central farm/rotate rather than remain under a destroyed goal.
Below 30% HP they seek functioning allied goals, resuming tasks above 58%.
Recovery combat uses basic attacks with movement constrained inside the goal;
movement spells are withheld there. Below 14% they disengage and use the normal
three-second recall when no enemy is nearby. Damage/death interrupt recall.
At each final-objective spawn bots independently roll 97% attendance once; the
decision is retained for that spawn. Critical recovery still takes priority.

### Editing builds and behavior

- `Assets/Resources/Bots/Roster.asset`: weighted build catalog, optional trainer
  outfit strings, recovery/recall thresholds, final-objective attendance and task
  commitment. Invalid outfit strings fall back to the standard trainer.
- `Assets/Resources/Bots/Loadouts/`: per-Pokemon move branches, three held-item
  catalog indices, battle-item asset, enable flag and weight. `gameplayPokemon`
  validates that selected branches actually belong to the Pokemon's learnset.
- The server chooses a build once at final selection/confirmed draft pick;
  replicated participants carry the selected build and outfit. Move upgrades
  retain the chosen branch. Potion, Eject, Full Heal, X Speed, X Attack, Goal
  Getter and Brick have situational use rules; unimplemented items are excluded.
- **Tools > Bots > Create missing loadouts and map bindings** adds defaults
  without overwriting existing builds, roster tuning or camp bindings. Save any
  open scene edits first. Newly added Pokemon require an input/HUD compatibility
  audit before inclusion in the authoring allowlist.

### Draft

Bots do not choose autonomous bans. They wait through the ban window for legal
human teammate requests and otherwise confirm an empty ban. Bot picks wait for a
human teammate picking in the same turn, reject banned/confirmed picks, and never
negotiate switches. Human lane choices remain authoritative until match loading.
Timeout fallback is bounded rather than repeatedly rolling unavailable choices.
The snake-pick schedule is `1-2-2-2-2-1` for 5v5; confirmed slots are not revisited.
Draft does not enable blind-pick arbitration, so human confirmation and later
human-to-human swaps still persist through the existing session property flow.

**Pool limit:** there are 13 audited, implemented choices. Lapras and Sableye are
placeholders and are not used to inflate this pool. Ten unique picks plus four
compatible bans cannot be guaranteed; an exhausted pool aborts with a diagnostic
and content-load failure rather than hanging or selecting an illegal Pokemon.
More compatible Pokemon are needed to guarantee every possible full draft.

Bots still lack coordinated teamfight plans and dynamic role reassignment based
on undeclared human movement. Declared human roles are respected. Surrender votes
count humans only, preventing bots from blocking a human team's surrender.

### Ally Unite indicator

`Assets/Prefabs/AllyInfo.prefab` binds its existing `UniteChargeDot` directly. It
shows a living ally's full Unite charge (not temporary stun/action availability),
and hides on use/death. Owners report changes to the server; updates and initial
snapshots are sent only to allied human clients. Actor-owned aiming keeps bot
casts separate from the local human's input and HUD.

### Special-mode policies

Special modes instantiate `BotVolleyballBrain`, `BotRacingBrain` or
`BotRoyaleBrain`, never the lane/farming `BotBrain`. `BotModeBrain` shares only
normal movement/combat actuators and lifecycle handling. No lane scoring,
goal-recovery loop or final-boss rotation is inherited by these modes.

- **Volleyball:** one stable bot per team is the primary defender, independent
  of Pokemon class. It tracks the actual incoming landing position and fights
  without chasing. Defensive coverage withholds displacement spells/Unites;
  melee targets must remain within coverage. An available attacker temporarily
  covers a KO'd, immobilized-outside-zone or too-late defender. Other bots pressure
  the enemy court, with one handling the opening serve. Returns use the real
  landing-zone occupancy mechanic. Bots start their hallway approach during the
  first round countdown, without waiting at spawn; configured moves/upgrades are
  auto-selected without a movement pause. Combat waits for the rally to begin.
  Subsequent round countdowns still lock movement normally. Human controls and
  countdown timing are unchanged. Round movement locks/reset/respawn include
  server bots, and only the human's camera is panned.
- **Racing:** bots use the ready screen's forced racing character and empty
  loadout, with steering fed into `PsyduckRacePassive`. Ordered checkpoint route
  segments have editable intermediate guides; the checkpoint-9 approach explicitly
  enters the physical launchpad. Bots slow through bends and occasionally use
  dash/acquired item-moves. A stalled racer steers sideways toward the track
  interior to escape physical rotating obstacles absent from the baked NavMesh;
  it never teleports or advances a checkpoint as recovery. Checkpoint/lap progression is exclusively through
  normal trigger crossings. Finishing locks movement/actions without moving the
  host camera or replacing its lap HUD with a bot's. Launchpad callbacks are
  per-actor and item-spawn callbacks are one-shot.
- **Royale:** bots alternate committed patrol/objective tasks, engage visible
  opponents and disengage when critically low. `BotArenaContext` binds patrol
  positions and the four actual objective spawners. Rankings, statistics, results
  and trainer companions bind gameplay participants rather than requiring an
  extra human network coordinator for every bot.

**Tools > Bots > Create missing special-mode bindings** authors missing Royale
patrol/objective bindings and racing route segments. It preserves existing
bindings and rejects dirty scenes. Inspect/tune the authored route guides in
`Assets/Scenes/PsyduckRacing.unity` and patrols in `Assets/Scenes/UniteRoyale.unity`.

## Implementation boundaries

- `PlayerNetworkManager.Bots.cs`: host-owned persistent participant/selection
  lists, lobby membership invalidation, selection arbitration, avatar spawning,
  and server-only saved bot lane assignments. Uses the existing network prefab;
  no additional network-prefab registration is needed.
- `BotAllocationRules.cs`: pure deterministic lane and character allocation.
- `LaneSelectionNetwork.cs`: bot choices share the existing team-targeted lane
  snapshots. Human RPC identity still comes from the sender's owned coordinator.
- `PlayerManager.Bots.cs`: explicit bot identity, isolated movement input,
  initialization, statistics, scoring and respawn lifecycle.
- `BotBrain.cs`: server-only decisions. It requests normal actions rather than
  applying damage, bypassing cooldowns, or driving a second movement component.
- Supported move/attack implementations use their actor's Aim. `Aim.Instance`
  remains the local human's presentation instance.

Other maps/modes beyond those listed above, matchmaking fill policies, disconnect replacement and
dedicated-server support are not enabled. Extend the
supported map gate only alongside its navigation/farming policy. Extend the
Pokemon pool only after auditing its input, aiming and owner-only HUD usage.

## Validation

- `Unite.Bots.Tests`: allocation, human priority, identity serialization, input
  isolation, safe brain construction, authored lobby buttons and Theia barriers.
- `Unite.Bots.PlayMode.Tests`: teleport/tween cleanup and real NavMesh carving,
  including detours while a wall is closed and direct paths after opening.
- Existing multiplayer, lane-picker, network-resilience and supported-Pokemon
  tests exercise surrounding integration contracts.
- Manual host smoke coverage: lobby fill; selection and lane priority; navigation,
  combat and natural scoring; KO/respawn; distinct participant results; return
  to lobby and cleanup.

Follow-up validation: 84 EditMode checks and 161 PlayMode checks passed. A private
Theia host match with nine bots also verified both junglers leaving closed spawn
barriers, independent allied/enemy countdowns (temporarily extended for inspection),
and a Scorbunny returning inbounds and resuming movement after its parked death Y
was perturbed to -49.4. Activating each team's main goal opened its own wall only.
Manually spawning/defeating Rayquaza toggled the central wall's renderer, collision
and carving together; its closed navigation detour became a direct path on defeat.
The scene-authored Add/Clear buttons filled nine slots and removed all nine bots.
This was targeted smoke coverage, not another full ten-minute match.

Remote-client join races and team-private synchronization still need a separate
multi-instance integration pass; a host-only smoke test cannot verify those.

### Configurable-bot iteration

- 106 focused bot/draft/lane/network EditMode checks passed, including explicit
  map bindings, legal builds, existing-dot wiring, readiness RPC contracts,
  loadout/outfit serialization, persistent final attendance and full draft turns.
- 108 selected PlayMode checks passed around bots, melee targeting, Rayquaza,
  Meowstic, Flareon, Flygon, Emboar and Absol.
- A broader 544-check EditMode run passed 542. The two failures are Cresselia and
  Meowstic metadata tests dereferencing the absent serialized `description` field
  on `PokemonBase`, the same issue previously seen in Flygon/Vaporeon tests;
  these unrelated asset/test contracts were not changed here.
- Live host blind-pick testing exercised nine bots, opening farm, configured
  upgrades, jungle ganks, recovery/recall, final-objective attendance and the
  charged/used ally Unite dot. Boosted-Unite testing exposed a Jolteon human-input
  access, subsequently replaced with actor movement input.
- Live full-draft testing verified a requested Cinderace ban, an empty opposing
  bot ban, ten globally unique picks, the human Flygon's persisted selection,
  bot switch refusal, and a human central declaration displacing allied bots.
  The resulting ten-player match exercised boosted builds and battle-item
  cooldowns without gameplay exceptions. A controlled critical-HP check observed
  the normal recall channel followed by teleport to the team's spawn, not a walk
  home. Results returned to the lobby with zero remaining gameplay avatars.
- Results teardown exposed a Cresselia cleanup RPC targeting an already-despawned
  Pokemon; cleanup now checks the target's spawn state before sending that RPC.
  A fresh full draft with a human Cresselia then completed gameplay, results and
  lobby cleanup with no console errors after this fix.

These are targeted/accelerated checks, not a claim that every build or remote
client synchronization path has been fully exercised.

### Human Emboar startup with empty held-item slots

An unequipped human could throw in `HeldItemManager.Initialize`: the legacy
`EmptyHeldItem` asset omitted its stat-boost list. This interrupted the Pokemon
load callback before `HPBar.SetPokemon` and `LearnBasicAttack`, leaving a missing
HP bar and the empty attack implementation. Empty/null boost lists now mean no
bonuses in gameplay and selection UI, and the empty asset explicitly has none.
Eject Button release/cancel is also safe before its first activation.

The reported Theia blind-pick setup was reproduced with human Emboar, three empty
slots, Eject and nine bots: HP initialized at 3250/3250, the HP bar was bound, and
the regular attack-controller path dealt 178 damage (2860 to 2682) credited to the
human. Regression tests cover the actual all-empty held-item initialization path,
legacy null lists, descriptions and inactive Eject cancellation. A separate
stale queued kill-notification reference surfaced during this longer match;
notifications now skip already-despawned victims/attackers safely.
Validation for this follow-up: 142 selected EditMode and 32 selected PlayMode
checks passed (bot, draft loadout, HP-bar and Emboar coverage).

### Special-mode iteration

- Final focused EditMode run: **292/293 passed** across bots, lane picker,
  network resilience, draft and clothing. The sole failure is the unrelated
  `SharedUiTests.ErrorOverlayUsesOpaqueHighContrastUiAndSelectableDismissal`
  at `Assets/Editor/NetworkResilienceTests/SharedUiTests.cs:121` (expected
  `Backdrop/Dialog` hierarchy not found).
  Special-mode tests cover supported formats, backup eligibility, isolated brain
  construction, scene bindings, the required racing launchpad, physical-stall
  recovery direction and completion after finished/unfinished racer departures.
- **121/121 PlayMode checks passed** across racing passive/animations, bot
  navigation/lifecycle, HP bars, wild-Pokemon combat and trainer clothing.
- Live private Volleyball 3v3: stable defenders, enemy-court pressure and an
  attacker switching to temporary landing coverage after a defender KO were
  observed. Results returned to the lobby without console errors.
- Live private Royale: all four gameplay participants and trainer companions
  were present. Bots fought, died/respawned, wandered and pursued objectives.
  Kill/death rankings and results included all three bots plus the human;
  results returned to the lobby without console errors.
- Live six-racer match: all five bots crossed the required launchpad, used
  acquired item-moves and naturally finished three laps (135–148 seconds of game
  time). After the opening eight seconds, this smoke ran at 2x time scale.
  The idle human remained on lap 1; bot finishes neither panned its camera nor
  changed its lap HUD. Only the idle human's final completion was simulated to
  exercise results: six distinct participants ranked 1–6. Results returned to the
  lobby with zero remaining gameplay avatars and no console errors. The earlier
  probe exposed a rotating-table snag; recovery direction has a regression test,
  although the final random item/steering run did not require a recovery.
- Racing finish/disconnect handling uses remaining active counters, not a count
  comparison against historical results, and preserves confirmed finish places.
  Event subscriptions/coroutines are detached on despawn; the human lap-HUD
  binding is released on UI teardown.
- Private test sessions were closed, temporary observers/state removed, time
  scale restored to 1 and the Editor left out of Play mode on `BootScene`.
  After the clean match/results/lobby check, closing the private session logged
  an RTT poll against disposed transport storage from the unchanged
  `Assets/Scripts/UI/GameStatsUI.cs:63` and a Unity Services Wire WebSocket
  shutdown exception. These separate shutdown diagnostics were not changed here.

These are host-only checks. Remote-client synchronization and exhaustive coverage
of all configured Pokemon builds remain outside this validation pass.

### Volleyball opening hallway follow-up

The initial three-second `RoundStarting` gate previously stopped the bot brain
even though the opening hallways were available. Bots now approach immediately
during that first countdown, without enabling early combat. Later round resets
retain their normal movement lock; the human input/selection flow is untouched.
The existing automatic loadout selection already drains the bot's move queue
without a timed wait, so no extra move-selection delay is introduced.

Validation: **183/183 bot EditMode checks passed**, including seven opening versus
later-round gating cases. In a live 3v3 opening, all five bots had their A/B/Unite
moves selected and had travelled about one unit by 0.25 seconds, four units by
one second and 10–11 units by 2.5 seconds, while the first countdown was still
running. No bot selected a combat target during that approach.
After a controlled test point, all five bots correctly returned to zero input
and locked movement for the next round's countdown. No gameplay console errors
were reported during this check.
