# Open world main lobby

Start from `BootScene`. **Settings > Auto-join open world lobby on Start** is
persisted in `PlayerPrefs` under the existing `OpenWorldLobbyEnabled` key and
defaults to enabled. It only chooses where pressing **Start** in `BootScene`
lands: the open-world city when on, or the static `LobbyScene` when off.
The city remains available through **Enter city** regardless of this setting.
Changing the toggle does not leave, cancel, or join a city session, affect trainer
movement, or change subsequent home navigation after a party/match.

## Graphics and mobile display settings

Settings retains the three quality presets and adds saved overrides:

- **Render scale**: Use preset, 50%, 67%, 75%, 85%, or native 100%. This scales
  the 3D render resolution across scenes; the screen-space settings UI stays sharp.
  Preset defaults are 75% for Performant, 90% for Balanced, and 100% for High Fidelity.
- **Anti-aliasing**: Use preset, Off, FXAA, or SMAA. Presets use Off, FXAA,
  and SMAA respectively. Game cameras disable MSAA to avoid stacking AA methods.
- **Post-processing**: Use preset, Off, or On for volume post-processing across scenes. Performant
  defaults to Off; the other presets default to On. AA can remain enabled with
  effects off: URP still runs its AA passes but game cameras exclude effect volumes.

Overrides apply immediately across screen-rendering game cameras, including cameras
created on scene changes. UI-only, URP overlay, and render-texture preview cameras
retain their authored presentation. Existing city-specific preferences migrate to
the global options. Overrides persist across quality changes and restarts.
Choose **Use preset** on a control to restore that preset's behavior. Runtime render
scale changes use a cloned pipeline asset; authored URP assets remain unchanged.
The existing gameplay zoom policy can further reduce the chosen render scale.

Mobile **Target framerate** is editable and defaults to 60 FPS. Android also has
**Refresh rate**, defaulting to **System default**, independent of the FPS target.
Explicit refresh requests select the lowest supported same-resolution display mode
at or above the requested rate, or the highest supported mode if none reaches it.
Android's OS, battery policy, and hardware retain final control; Unity may round FPS
caps down to a supported display divisor. There is no mobile Unlimited option.
Focus/resume and quality changes reapply the saved preferences rather than forcing
the maximum display rate. Desktop retains its VSync/Unlimited controls; WebGL frame
pacing remains browser-controlled.

The editable rows live in BootScene. **Tools > Settings > Wire Graphics Controls**
repairs their bindings using the existing dropdown style without duplicating rows.

## Session boundary

`LobbyController` retains its existing match/party session. Its open-world partial
routes home navigation and serializes city/menu transitions with the existing
lifecycle gate. `OpenWorldLobbyController` owns a separate, mutually exclusive
social session on the same NGO `NetworkManager`.

Social discovery uses the existing MPS `MatchmakeSessionAsync(QuickJoinOptions,
SessionOptions)` autojoin infrastructure, with a three-second search and
`CreateSession = true`. An available city is joined; otherwise this player hosts
a public 16-player city through Relay. Session type is `UniteSocial`, and indexed
version/style properties explicitly separate cities from match discovery.
Session type alone is only a client-side SDK key, not a discovery filter.
The social protocol suffix is `open-world-v5`, isolating the current avatar and
Pokémon companion replication from older city sessions.

The city temporarily replaces NGO's connection approval and disables the default
match player prefab. It restores both settings before a match/party connection
can start. New connections submit their stable player ID in connection data;
the host checks session membership before creating a trainer and rejects
duplicate IDs. This is a prototype membership binding, not cryptographic proof
of identity; a production authentication scheme must validate the caller's
identity rather than trusting a supplied UGS ID.

The **Match menu** button leaves the social session and opens the existing static
menu, where custom lobbies, party matchmaking, clothing, and other existing menus
continue to work. **Enter city** returns from that menu when no party is active.
No match is started by a city host and no city visitor is placed in a match team.

## Trainer and scene

`ChineseLobby` references the existing city model with its generated colliders.
The scene owns a small HUD and spawn center. `OpenWorldTrainerAvatar` uses the
existing `TrainerModel`, clothing serialization, locomotion animations,
`ClientNetworkTransform`, InputManager movement action, and Cinemachine.
Movement is owner-controlled; the host creates one avatar per admitted member.
The city has no Pokémon/combat player coordinators.

Desktop: use the normal movement bindings and right-drag to orbit the camera.
Controller: left stick moves, right stick rotates the camera. Mobile adds an
on-screen left stick wired into the same movement action and single-finger
camera orbit on the world outside UI. The orbit gesture owns its starting finger,
so movement-stick touches cannot rotate the camera or interrupt another drag.
Android/iOS Editor targets also show the stick; Device Simulator touch and
left-mouse world dragging support testing camera orbit in the Editor.
Settings/text entry
block movement. Falling below the environment returns the owner to its spawn.
The avatar prefab is registered in `DefaultNetworkPrefabs`; `ChineseLobby` is
appended to build settings without changing existing scene indices.

## Editable UI

Open-world lobby UI shells are authored; picker rows are generated on first opening:

- `Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab`, placed in `ChineseLobby`, owns
  the status label, movement hint, Settings/Match menu buttons, mobile stick,
  and configured EventSystem. Edit text, styles, layout, input module, and button
  callbacks directly in the prefab or through scene overrides.
- `Assets/Prefabs/Lobby/OpenWorldHomeButton.prefab`, placed under `MainMenuUI` in
  `LobbyScene`, owns the Enter city button and its serialized click callback.
- `BootScene` owns `SettingsCanvas`'s OpenWorldLobby settings row. Its toggle is
  assigned to `SettingsManager.openWorldLobbyToggle` in the Inspector.
- `Assets/Prefabs/Lobby/OpenWorldTrainer.prefab` owns the overhead `PlayerName`
  label, reusing Unite Royale's font, material, and camera-facing behavior.
  Edit its position, size, and styling in the prefab. Its text follows the
  trainer's replicated player name, including subsequent identity updates.
- The HUD's **Emotes** button opens its authored scrollable animation picker.
  Its panel, close button, and scrollbar are editable in
  `OpenWorldLobbyHUD.prefab`; shared row styling lives in
  `Assets/Prefabs/Lobby/OpenWorldEmoteButton.prefab`.

Runtime code generates picker rows, updates status/preference values and visibility, and binds
the settings toggle listener. The authored mobile controls are enabled on mobile;
the city's EventSystem defers to an already-active system when necessary.

## Pokémon companions

The bottom-right **Companion** button displays the selected Pokémon's circular
portrait and name. Click it to open the scrollable roster, then choose a Pokémon.
Hidden characters are excluded. The picker blocks trainer movement while open;
Close, Escape, settings, and losing the local avatar dismiss it. The choice is
saved by Pokémon name in `PlayerPrefs` (`OpenWorldLobby.Companion`) and restored
after returning to the city or reconnecting.

`OpenWorldPokemonCompanion` on the trainer replicates the selection through an
owner-only, server-validated RPC. Each client loads the selected Pokémon's final
evolution model through Addressables and creates a cosmetic follower for every
trainer. These are not combat Pokémon or separately network-spawned objects.
Switching Pokémon, despawning, and leaving the scene release the model handle
and remove the old follower.

`Assets/Prefabs/Lobby/OpenWorldPokemonFollower.prefab` owns the NavMeshAgent and
follow/stop distances, repath interval, sampling radius, and catch-up distance.
It uses the agent type from ChineseLobby's existing baked NavMesh, waits safely
if no nearby mesh is available, and relocates near its trainer after a teleport.
Its model's `Walking` animator bool follows actual agent velocity, with root
motion disabled. The baked scene and navigation data are not modified.

The picker shell is authored in `OpenWorldLobbyHUD.prefab`. On first opening,
selectable companions are read from `CharactersList` and buttons are instantiated
from `OpenWorldPokemonButton.prefab`, with runtime names, portraits, and callbacks.
Rows are reused when reopened. Roster changes need no Editor rebuild. After changing
the scene's agent type, run **Tools > Open World Lobby > Rebuild Pokémon Companions**
to rebuild the follower wiring.

## Emotes

`Assets/Animations/Lobby/OpenWorldEmotes.asset` has separate **Male Animations**
and **Female Animations** lists. Each entry exposes only
**Animation Name**, **Display Name**, and **Speed**. Enter an animator state's
short name or an animation clip name; the Editor automatically finds the clip,
full state path, layer, and controller from `TrainerModel.prefab`, including
states whose names differ from their clips. Invalid names are not playable.
These resolved details are hidden, cached build data, not manual setup fields.
Bindings refresh when editing the asset or reimporting trainer controllers.
The asset's **Refresh Animation Bindings** context action can also refresh them.

Display names can be customized independently of the animation name. Renaming a
display name does not change playback or network identity. **Speed** is a
playback multiplier: `1` is normal, `2` is twice as fast, and `0.5` is half speed.
The animator's original speed is restored before blending back to idle.
Keep list order stable: generated buttons and network messages use male entries
followed by female entries as their combined indices. Existing indices remain
unchanged by the two-list migration.

Selection closes the picker and requests playback from the avatar's owner.
The server validates the index against that trainer's gender, then replicates
the selection, start time, and duration. Observers and late joiners play the
same animation at its current offset. Clips marked as looping keep playing until
replaced or interrupted by movement; late joiners start at the current loop phase.
Non-looping clips return to idle automatically;
completion uses the clip's duration, authored state speed, and emote speed, not just animator
state exit callbacks. Both trainer controllers have a `ResetAnim` trigger from
Any State to their normal idle with a 0.2-second blend. Movement is blocked only
while the picker or another input-blocking menu is open. Camera orbit remains
available during emotes. Movement immediately cancels playback locally and
replicates cancellation; selecting any emote, including the current one, starts
a new playback. The Emotes button depends only on the local trainer's readiness,
not any trainer's playback state.
Outfit changes cancel the current emote. Host migration recreates trainers in
idle rather than resuming an emote.

The HUD prefab contains empty picker content and a reference to the reusable
`OpenWorldEmoteButton.prefab`. Buttons are instantiated from the catalog on first
opening and reused afterward; availability and labels refresh when opened or when
the trainer animator changes. Catalog changes need no Editor rebuild. **Tools >
Open World Lobby > Rebuild Emote Menu** rebuilds only the authored shell and template
wiring, replacing the panel/open button, so preserve custom layout changes first.
The catalog Inspector provides **Reset Male Display Names** and **Reset Female
Display Names** buttons. Each numbers only its own list starting at `Emote 1`,
preserves animation bindings and speeds, and supports Undo. Existing custom
names are not changed until the corresponding reset button is pressed.

## City chat

The existing `ChatManager`, message prefabs, notification/unread system, and
`ChatMessageReciever` network prefab also serve the city. **Global** means the
current city only; **Private** sends to the selected city trainer and the sender.
The server validates session ID, service membership, and the sender's current
approved connection ID before routing. City chat uses a separate stable
`GameSessionView`; it does not replace the party view or mutate the persistent
local player's party properties. Owner IDs are rebuilt from approved connections
on the server and replicated trainer ownership on clients, never trusted from
service-authored player properties.

The authored `ChatHolder` in `OpenWorldLobbyHUD.prefab` is preserved. The HUD's
`ChatManager` references it, the shared message prefabs, an **OpenChatButton**,
and a **CityChatNotification**. All controls, labels, badge styling, and layout
remain editable in the prefab. Send works through the send button or input
submission. Opening chat blocks trainer movement/emotes through the existing
input checks. **Tools > Open World Lobby > Wire Existing Chat UI** repairs
bindings without rebuilding the existing panel; missing opener/notification UI
is copied from the static lobby in the Editor, never created at runtime.

Delivered global city messages also appear above the sender's nameplate, including
the local trainer, even while the chat panel is closed. Private and party messages
never become bubbles. This reuses the server-validated chat delivery and resolves
the sender by stable player ID in the receiving client's spawned trainer roster;
no extra RPC or replayed history is used. New messages replace the current bubble
and restart its six-second, unscaled timer. Despawning or disabling the trainer
clears it. Text wraps within bounded dimensions, with overflow ellipsis and rich
text disabled.

`OpenWorldTrainer.prefab` owns the editable **ChatBubble** world-space canvas,
background from `Sprites/Chat`, and message label. The root's
`OpenWorldTrainerChatBubble` component exposes duration, display length, size
bounds, and padding. **Tools > Open World Lobby > Wire Trainer Chat Bubble**
repairs references or creates missing bubble UI in the Editor, not at runtime.

The network receiver is rediscovered after reconnects and recreated by the new
host when needed, without duplicate receivers or event subscriptions. Received
history and unread counts remain local during same-city host migration; they
are cleared when leaving or recovering into a different city. Historical
messages are not replayed to new arrivals. Validate live Relay host migration
with separate processes before release.

## Host migration

Both joining and hosting social players opt into MPS `WithHostMigration` with a
custom `IMigrationDataHandler`. Gameplay sessions do not enable this feature.
The host uploads versioned, city-scoped stable-ID/pose snapshots every three
seconds. `Apply` only restores plain cached data: MPS calls it while NGO is
stopped. The SDK elects/promotes a host, allocates new Relay networking, and
restarts NGO; trainers are spawned again against fresh connection IDs.

Owners continuously retain their latest local pose and restore it on their fresh
avatar, including when no first host snapshot exists. Migration snapshots are a
fallback for remote poses, not a persistent world save. Reconnection temporarily
freezes input and displays migration status; it is not a seamless transport swap.
Callbacks are session-scoped and intentional departure suppresses recovery.

On explicit migration failure, deletion/removal, or a 90-second disconnected
deadline, recovery serially leaves the old session and runs discovery again,
excluding the failed city's indexed ID. If recovery also fails, the HUD offers
the match menu. Initial connection failure falls back to the static menu.
SDK leave tasks are retained when a wait times out; no replacement network starts
until that cleanup actually finishes. Navigation waits for an in-flight migration
to finish instead of racing the SDK's network restart.

## Validation

EditMode coverage: `Unite.Settings.Tests` (default, persistence, authored UI wiring),
`OpenWorldLobbyTests` in `Unite.NetworkResilience.Tests` (discovery isolation,
routing boundaries, snapshot validation/round-trip), plus avatar/prefab helpers.
`Unite.OpenWorld.PlayMode.Tests` additionally starts a local UTP host and client
with the real avatar prefab. It checks host/visitor ownership, identity and
pose replication, despawning, local pose retention, and scene-camera restoration.
Its empty outfit fixture avoids external Addressables requests; it does not
validate full outfit rendering, Relay discovery, or live service host migration.
Emote tests cover catalog/state coverage, friendly naming, validation and reset
transitions (`Unite.OpenWorldEmotes.Tests`), plus host/client playback, menu
filtering, ownership, repetition, and return-to-idle in the PlayMode suite.
Companion tests cover runtime roster/icon wiring and baked agent compatibility,
NavMesh following and `Walking`/idle transitions, missing-mesh/teleport recovery,
host/visitor selection replication, server validation, rapid switching, saved
selection restoration, picker input blocking, and follower cleanup.

Before shipping, validate with separate standalone processes, not just Editor
stop/start:

1. Fresh preference defaults to city; disabled preference opens the static menu.
2. First player hosts; second joins the same city and both see movement/outfits.
3. A full city causes another player to host a separate city.
4. Graceful host departure migrates remaining trainers to a new host.
5. Kill the host process, including before the first snapshot; verify recovery.
6. Repeat host migration and verify latest local poses and one avatar per player.
7. Toggle off/open the match menu during connection and during migration; verify
   no concurrent NGO connections, orphan avatars, or altered match configuration.
8. Create/join a match lobby; complete/leave a match; verify existing party return
   behavior and city return after explicitly leaving the party.
9. Test service outages, failed migration, and cleanup timeout, then retry.
10. Check collisions, camera, and trainer rendering on desktop and Android.

## Placeable NPCs

Drag `Assets/Prefabs/Lobby/OpenWorldTrainerNpc.prefab` or
`Assets/Prefabs/Lobby/OpenWorldPokemonNpc.prefab` into `ChineseLobby`. These are
stationary, scene-authored NPCs, not network player avatars or followers. Each
client renders and interacts with them locally; dialogue and animation playback
are intentionally not broadcast to other visitors.
The city already includes a trainer and an Eevee near spawn under **Lobby NPCs**;
move, duplicate, or remove these prefab instances as needed.

On `OpenWorldNpc`, configure:

- **Visual**: the child model. Trainers require `TrainerModel` on this child's
  root. Replace the Pokémon prefab's Visual child with another Pokémon model
  and update this reference to change species. Do not use a combat Pokémon root.
- **Clothes**: the same Base64 `PlayerClothesInfo.Serialize()` string used for
  players. An empty string uses the default male outfit. Invalid strings log a
  warning and disable interaction rather than applying a partial outfit.
- **Idle Animation / Interact Animation**: clips matching the model's rig and
  trainer gender. Idle loops regardless of the clip import setting; interaction
  plays once, then returns to idle. Leave Idle empty to use the model controller.
  Clips play in place, with root motion disabled.
- **Animation Blend Duration**: transition time in seconds, shared by the NPC and
  the optional player reaction. Defaults to **0.2**; set **0** for instant changes.
  Idle/interaction transitions blend from the last displayed pose, after Animator
  evaluation and before clothing bone synchronization. Player reactions also
  blend back into the live locomotion pose when finished or cancelled by movement.
- **Play Trainer Animation**: optionally animate the interacting local player as
  well, for either NPC kind. Assign **Male Trainer Animation** and **Female Trainer
  Animation**; the player's actual outfit gender selects the clip. A missing
  clip for that gender skips only the player's animation. This is off by default.
  The clip plays once and releases control to the normal locomotion controller;
  moving blends back to locomotion. Clothing changes, ownership loss, disabling,
  and despawning also release playback. As with NPC reactions, this is local,
  not replicated.
- **Interaction**: Animation or Dialogue for trainers. Pokémon always use
  Animation, enforced at runtime as well as in the Inspector.
- **Dialogues**: the existing dialogue pages, title/body/optional preview image.
  **Portrait Mode** selects Default (professor), Hidden, or Custom; Custom uses
  **Portrait Prefab** in the existing UIObject3D. NPC dialogue defaults to Hidden.
  Existing `DialogueTrigger`s also expose these options and retain Default.
- **Interaction Distance / Label**: range and prompt text. The closest ready NPC
  shows its world-space button. Press **E**, gamepad **A / south**, or click/tap
  that button. Chat focus, menus, and an open dialogue suppress interaction;
  dialogue also blocks trainer movement until closed.

Dialogue keyboard/controller controls (also available outside NPC dialogue):

- **Enter / Space / E**, or gamepad **A / south**: reveal the current page's text;
  press again to advance, or close after the last page.
- **Arrow keys**, **D-pad**, **left stick**, or **shoulder buttons**: previous/next
  page. Stick navigation uses a deadzone and delayed repeat.
- **Escape**, or gamepad **B / east**: close immediately.

Mouse/touch buttons remain usable. Dialogue temporarily owns EventSystem
navigation to prevent a single confirm from also submitting a selected button,
and restores the previous navigation/selection when closed. The input that opens
dialogue is ignored by it; closing input cannot reopen an NPC in the same frame.
Text uses unscaled time so it still types when the game is paused.

The prefab's **InteractPrompt** canvas/button/label remain editable in the
Inspector. The root capsule provides stationary collision. NPCs need no
NetworkObject or NavMeshAgent. **Tools > Open World Lobby > Create NPC Prefabs**
creates missing example prefabs without overwriting existing customization.

NPC tests cover prefab wiring, player outfit compatibility, Pokémon mode
enforcement, real trainer/Pokémon skeleton poses after normal Animator evaluation,
gender-specific owner reactions, idle looping, one-shot return, and cleanup.
Transition tests check intermediate poses in both directions, movement fade-out,
cached-pose continuity, and the zero-duration option.
Dialogue tests cover keyboard/gamepad paging and reveal, duplicate-submit
prevention, opening/closing input isolation, paused typing, and navigation restore.

Proximity-based chat, invitations, and social party formation are not included.

### Stage jukebox

The city stage has an editable `OpenWorldStageJukebox` prefab with a purple placeholder
DJ console at `(0, 0.55, -33.5)`. Its child **Stage Area** defines the music zone using
an editable collider volume; box and sphere shapes are supported directly. Player position, not camera
position or physics trigger callbacks, determines whether stage music is audible.
Entering crossfades from the city theme to the shared stage track; leaving restores
the city theme. Playback uses the server clock and existing track loop points so
entering players and late joiners hear the current position, not a fresh intro.

The console uses the existing nearest-NPC prompt (E / controller A / touch).
Its scroll view, status label, and Close button are authored inside
`OpenWorldLobbyHUD.prefab`. The jukebox initializes the menu from its own playlist;
the menu lazily instantiates a row per playable entry from
`OpenWorldStageTrackButton.prefab`, reuses rows on reopen, and rebuilds them when
the source playlist changes. The HUD does not own or duplicate the playlist. Movement is
blocked while the menu is open. Close, Escape, controller B, moving out of range,
and losing the local avatar dismiss it. The server validates ownership identity,
stage membership, console distance, track index, and a global five-second cooldown.
Selecting the current track does not restart it or consume the cooldown. Track and
playback/cooldown timing are included in city host-migration snapshots.

Select **Stage Jukebox** in `ChineseLobby` or edit `OpenWorldStageJukebox.prefab`:
the Inspector's **Playable Tracks / Menu Order** list lets you add, remove, and
drag entries, each with a **Display Name** and **Music Track** (JSAM Music File).
Blank display names fall back to the music asset name. **Default Track** chooses
the initial song and follows its entry when reordered; the fallback is the first
playable entry. Missing or multi-clip music assets are skipped, with Inspector
warnings; an empty playlist keeps normal city music. Single-clip assets ensure
every listener uses the same recording. Non-looping music repeats in the jukebox;
the asset's starting pitch is shared without per-listener random pitch variation.
Ship the same playlist to all clients.

Customize row visuals, label, selected prefix, and layout height in
`OpenWorldStageTrackButton.prefab`. Assign a different row prefab and Scroll Rect
on the HUD's **Open World Stage Menu** component without touching the jukebox.
Mouse/touch scrolling and gamepad focus use the existing scroll view. The optional
**Tools > Open World Lobby > Configure Dynamic Stage Menu** command converts the
legacy static rows while retaining the existing panel, scroll view, viewport,
scrollbar, and layout settings; it does not rebuild the city stage. The stage
theme uses the existing lobby-stage MP3 and the standard music volume track.

### Trainer interactions

Remote trainers use the same nearest-target selection and world-space interaction
prompt as NPCs (E, controller A, or the prompt button within 2.5 units). The local
trainer is excluded. The city HUD contains an authored trainer profile panel displaying the selected trainer's synchronized name, outfit,
and card customization. Private chat opens the existing city conversation by
player ID. The match-history area currently reads "Match history is coming soon!".
The profile blocks world movement, closes with Close/Escape/controller B, and
closes automatically if the selected trainer despawns or the local trainer leaves.
