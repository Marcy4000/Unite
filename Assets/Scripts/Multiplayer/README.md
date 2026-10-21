# Multiplayer Sessions integration

The project uses Multiplayer Services 2.4.0 with NGO. `LobbyController` remains
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

## Validation

`Assets/Tests/Editor/SessionMigrationTests.cs` covers stable views, deleted and
pending properties, departing players, and matchmaking roster validation.

Live validation still requires authenticated clients and a configured
`StandardsQueue`: test create/join/code/browser/quick join, visibility and kicking,
map/type changes, simultaneous party matchmaking, cancellation, host loss,
draft/loadout synchronization, results/party restoration, and native/WebGL Relay
connectivity. Unit tests do not contact Unity Gaming Services.
