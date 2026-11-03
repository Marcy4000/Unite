# Open world main lobby

Start from `BootScene`. **Settings > Open world lobby** is persisted in
`PlayerPrefs` and defaults to enabled. Turning it off returns to the existing
static `LobbyScene`. Changes made during a party/match take effect the next time
the player reaches the main menu; they do not terminate a match or party.

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
on-screen left stick wired into the same movement action. Settings/text entry
block movement. Falling below the environment returns the owner to its spawn.
The avatar prefab is registered in `DefaultNetworkPrefabs`; `ChineseLobby` is
appended to build settings without changing existing scene indices.

## Editable UI

All open-world lobby UI is authored, not generated during startup:

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
  Its panel, close button, scrollbar, and animation buttons are editable in
  `OpenWorldLobbyHUD.prefab`; shared row styling lives in
  `Assets/Prefabs/Lobby/OpenWorldEmoteButton.prefab`.

Runtime code only updates status/preference values and visibility, and binds
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

The picker and all its callbacks are authored in `OpenWorldLobbyHUD.prefab`.
After changing the roster or the scene's agent type, run **Tools > Open World
Lobby > Rebuild Pokémon Companions** to rebuild the picker and follower wiring.

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
Keep list order stable: authored buttons and network messages use male entries
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

All picker buttons exist in the prefab, not instantiated at runtime. After
adding/reordering catalog entries, use **Tools > Open World Lobby > Rebuild Emote
Menu** in the Editor to rebuild the authored picker. Rebuilding replaces the
picker panel/open button, so preserve custom layout changes before running it.
Editing display names alone needs no rebuild: labels refresh when opened.
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
Companion tests cover authored roster/icon wiring and baked agent compatibility,
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

Proximity-based chat, invitations, and social party formation are not included.
