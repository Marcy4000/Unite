# Multiplayer Sessions integration

The project uses Multiplayer Services 2.4.1 with NGO. `LobbyController` remains
the scene-facing component, but an `ISession` owns lobby membership, heartbeats,
service events, Relay setup, NGO startup, and normal network shutdown.

## Data and UI

`GameSessionView` and `GameSessionPlayer` are game-owned views, not Lobby SDK
models. Existing UI methods retain their lobby terminology through type aliases.
Player objects remain stable as service data changes; pending local writes are
overlaid until saved. Set metadata through `LobbyController`, not by modifying
the views directly.

Existing player keys and visibility are preserved. Nine normal player properties
are used; `MatchReady` occupies the tenth slot during party handoff and the match.
Do not add `WithPlayerName()` or another player property without accounting for
the service's ten-property limit.

Queries use public `HostDisplayName` and indexed `GameVersion = sessions-v1`
properties. The version filter excludes old, manually configured Lobby/Relay
lobbies. Joining by a code from an older build is not supported.

## Deliberate compatibility boundaries

- `SessionCapacityAdapter` uses the direct Lobby API **only** to change
  `MaxPlayers`, which the MPS 2.4 public session API exposes as read-only. It
  refreshes the session after the update. A new party initially allocates Relay
  for the largest supported map before reducing the lobby's join limit, so later
  map/type changes cannot outgrow that allocation.
- `PartyMatchmakerAdapter` uses the direct Matchmaker API for multi-player
  tickets and stored matchmaking results. MPS 2.4's high-level matchmaking call
  submits only the local player. It does not allocate Relay or start NGO.

`StandardsQueue` must use peer-to-peer Match ID assignments and return exactly
two teams within map 0's team capacity. Stored results determine the full roster,
teams, and deterministic player slots. Blue/Orange team names are honored;
otherwise both teams are ordered consistently by name and ID.

Party members acknowledge the match ID before the party host shuts down its
connection. Each member then leaves through Sessions and creates/joins the match
session. Each member acknowledges receiving the full match roster. The match host
waits for those acknowledgements and the assigned roster's NGO connections instead
of a fixed startup delay.

A return-session ID is distributed before leaving the party. On return from
results, the original leader recreates that party; members retry joining it for
up to 30 seconds. Choosing to leave explicitly returns to the main menu. Seamless
gameplay host migration is not enabled: host loss exits the session.

## Network failure handling (first resilience increment)

`SessionFailureGuard` arbitrates unexpected failures using an activation generation.
Session event callbacks capture that generation, so callbacks from an old session
cannot terminate a replacement. Duplicate transport/NGO/UGS failure notifications
start only one cleanup. Failures during a lobby operation remain pending until the
lifecycle gate is released. Intentional departures detach failure listeners before
Sessions shuts NGO down. Both host and client shutdown/transport failure paths are
monitored; a remote client departing does not terminate the host's session.

Cleanup has a 15-second overall deadline, including draining player/host writes.
A timed-out SDK task is **not cancelled**: its session and leave task are retained,
and the controller refuses to reuse them until cleanup has actually finished.
The failure path returns to the main menu even if the service cleanup times out,
clears pending results presentation, and shows an asset-independent failure notice.
`LastConnectionFailure` and `onConnectionFailure` are available for a styled UI.
This interruption does not synthesize a win/loss result.

`ClientReadinessBarrier` tracks the expected map/content readiness roster. It
rejects unknown clients, departed clients, and acknowledgements made on behalf of
another sender. Disconnects remove blocking readiness entries. Map loading and
the racing ready screen have inspector-configurable 120-second deadlines; content
load errors and scene synchronization timeouts fail safely instead of waiting
forever. Late Addressables callbacks are ignored after network despawn.

**Not implemented by this increment:** preserving disconnected characters,
automatic NGO reconnection, crash/relaunch rejoining, or gameplay host migration.
`ISession.ReconnectAsync()` only reconnects lobby membership in this SDK. Do not
enable `WithHostMigration` until a game-specific checkpoint handler can restore
all relevant state and validate completion before gameplay resumes. Player
ownership currently still controls essential simulation, and initialization
resets some state; these must be separated for safe recovery.

### Failure-injection checklist

Use standalone processes (Editor stop is not equivalent to a process crash):

- Kill a client during map loading: the host must not wait on its readiness entry.
- Kill the host or interrupt its transport: surviving clients return to a usable
  menu with one notice, no match result, and no stuck loading overlay.
- Interrupt a connection during party-to-match handoff: the failure must not be
  silently discarded because a lobby operation was in progress.
- Disconnect a client during gameplay: the host must not end everyone else's
  session (the departing character is still removed until preservation is built).
- Leave normally, including matchmaking cancellation/party handoff: no false
  connection-failure notice should appear.
- Simulate a stalled leave/property write: the menu returns within the cleanup
  deadline; a replacement session must not reuse the still-active network stack.
- Repeatedly create/join/leave: no stale player-manager callbacks or subscriptions
  should affect subsequent scenes.

These are live validation requirements, not claims that unit tests exercise Relay.

## Validation

`Assets/Tests/Editor/SessionMigrationTests.cs` covers stable views, deleted and
pending properties, departing players, and matchmaking roster validation.
`Assets/Editor/NetworkResilienceTests/NetworkFailureTests.cs` covers failure generations,
deduplication/intentional departure, readiness roster removal and sender validation,
and bounded waits for stalled SDK tasks.
`NetworkBehaviourTeardownTests` checks repeated despawn/destruction before network
startup without a session or initialized Addressables handles.

Live validation still requires authenticated clients and a configured
`StandardsQueue`: test create/join/code/browser/quick join, visibility and kicking,
map/type changes, simultaneous party matchmaking, cancellation, host loss,
draft/loadout synchronization, results/party restoration, and native/WebGL Relay
connectivity. Unit tests do not contact Unity Gaming Services.
