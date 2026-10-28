# Issue fixes and validation — October 28, 2026

Four implementation subagents worked sequentially in the shared checkout. Existing
uncommitted work was preserved; no branches or commits were created.

## Gameplay lifecycle

- Hidden animators retain the latest recovery/trigger state rather than replaying
  superseded casts; timed animation speed is restored across visibility changes.
- Jump-pad selection accepts only the local human, cleans up owned restrictions,
  and uses a prioritized camera override. Bot Duraludon Flash Cannon cannot take
  the host camera; cannon cleanup cannot release a jump-pad override.
- Flux zones deduplicate collider entries and remove their own applied stat changes
  when deactivated, disabled, or despawned.

## Draft and team information

- Draft phase timers retain their original durations: bans 20 seconds, picks 25
  seconds, preparation 30 seconds. Bot picks take a varied 6–9 seconds per Pokemon,
  reducing to 3–5 seconds once all humans on both teams have picked. Paired bot
  picks are staggered rather than confirming on consecutive frames. Bot bans allow
  6–8 seconds for teammate requests (3–5 without humans); requests act immediately.
- Team rows reconcile roster registration, team readiness, and portrait loading
  through events. Rebinding removes old subscriptions. Enemy death presentation
  uses player state rather than an initially unpopulated HP value.

## Input and lobby UX

- Idle controller gameplay hides the mouse pointer after two seconds; mouse or
  keyboard activity, menus, errors, and focus changes restore it.
- Nonblank player names are saved in PlayerPrefs and restored into the boot input.
- Connection errors use a high-contrast, controller-selectable uGUI/TMP modal.
- Lobby discovery and quick join filter `unite-app:<Application.version>`;
  acquired sessions and matchmaking rosters are checked for compatibility.
  See `SessionCompatibility.md` for the required Matchmaker Dashboard rules.

## Rendering and mobile display

- Zoom-out interpolates URP render scale down to 80% of the selected baseline,
  restoring it at normal zoom and on camera/pipeline teardown. This reduces scene
  pixel count, not gameplay visibility; overlay HUD rendering stays native.
- Android requests the highest advertised refresh rate at the current physical
  resolution on startup/resume/focus regain. Settings callbacks preserve that
  policy instead of restoring the previous fixed 75 FPS cap. iOS ProMotion is enabled.
- See `Assets/Scripts/Mobile/AndroidDisplay.md` for native-display limitations and
  device checks. URP render scale is pipeline-wide; FPS improvement is not measured.

## Final automated validation

- Full Unity EditMode sweep: **1,034 passed / 1,048 total; 14 failed**.
- Full Unity PlayMode sweep: **214 passed / 214 total**.
- All new regression fixtures passed in the broad sweeps. Implementation agents
  also ran their focused suites and reported successful compilation.
- Final script compilation completed; the Unity console reports zero error entries.
- The display agent compiled Android Java against SDK 35 and passed five mocked
  native mode-selection checks.
- Scoped script whitespace checks pass. Existing scene/prefab whitespace was not
  reformatted as part of this task.

The 14 EditMode failures are outside the changed feature areas and were not repaired:

- Eight `LobbyChatNotificationTests` fail setup: reflection requests the old
  single-argument `GameSessionPlayer` constructor, while the existing bot work
  provides a two-argument constructor with an optional bot flag.
- Four Cresselia/Flygon/Meowstic/Vaporeon asset tests read a missing serialized
  `description` property and throw NullReferenceException.
- Two `MoveUseTests` find the stored-move duplicate icon enabled in `MoveUI.prefab`
  and `MoveUIMobile.prefab`.

These results do not establish a clean whole-project test baseline. No baseline
checkout was substituted for the user's dirty workspace.

After the draft-pacing clarification, restored the original phase timers and
increased/staggered bot pick delays. All **93/93 Unite.Bots.Tests EditMode tests**
pass, including original timer durations, bounded delay variation, cross-team
human completion, and per-pick scheduling. The full-suite counts above describe
the earlier validation pass, not a second whole-project run after this adjustment.

## Remaining integration checks

- Rebuilt Android/iOS players on physical high-refresh devices: actual refresh/FPS,
  thermal/power/OEM caps, rotation, cutouts, background/resume, and settings changes.
- Separate host and remote client: late-loading ally/enemy portraits, death/respawn
  outside vision, and jump-pad selection during a host bot's full Flash Cannon cast.
- Two different application-version builds against live UGS/Relay, including
  lobby listing, code-based joins, quick join, and configured Matchmaker queues.
  The SDK can briefly acquire a direct-join session before version validation.
- Real GPU profiling at normal and jump-pad zoom, plus visual/controller checks of
  the new connection-error modal. Automated tests do not replace these checks.
