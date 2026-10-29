# Lobby-to-selection transition

`LobbyController.StartGame` starts the transition through the persistent
`LoadingManagerTracker`, rather than loading the selection scene immediately.

1. The server snapshots the connected human clients and broadcasts a transition
   generation, destination, and shared network start time.
2. Each client closes `GameBeginScreenUI` over its lobby. Trainer previews load
   independently; a per-player placeholder remains until a preview renders.
   Placeholders use the authored PartyScreen sprites: `t_matchroom_off_*` for
   purple and `t_matchroom_on_*` for orange, selecting male/female from lobby
   clothing data and pose 1–5 from the player's team slot. The twenty sprites are
   assigned on the BootScene component and displayed without additional tint.
3. Each client acknowledges that its entrance has finished. Only after all
   participants are covered does the server start NGO scene loading.
4. Selection controllers still call `HideGameBeginScreen` when initialized. During
   a managed transition this reports local readiness instead of opening early.
5. Once NGO reports scene completion and every participant reports readiness in
   the destination scene, the server broadcasts a shared opening time.

The generation rejects acknowledgements from previous matches. Disconnected
clients are removed from the barriers; bots do not participate. The coordinator
survives scene changes and resets its subscriptions and local UI on network
teardown. Existing direct selection-scene loading retains a local fallback, but
does not provide the synchronized lobby-cover guarantee.

## Validation

- `MatchTransitionBarrierTests`: participant gating, scene readiness, stale
  acknowledgements, disconnects, and fresh state across consecutive matches.
- `MatchTransitionHostTests` / `MatchTransitionDedicatedServerTests`: real NGO
  RPCs from two clients, authenticated sender IDs, both readiness barriers, and
  consecutive generations. These fixtures disable scene loading; they do not
  replace a multi-process lobby-to-selection playtest.
- `GameBeginScreenEntranceTests` / `GameBeginScreenLifecycleTests`: entrance,
  preview fallback, cancellation, and repeated-use behavior.

For a manual regression, start Electrode Volleyball with three players per team,
return to the lobby, and start again without restarting the network session.
Repeat with ten players and with a slow client. The lobby should remain visible
until covered, then the selection screen should appear only as the panels open.
Missing/slow trainer previews must not delay either transition or leave blank slots.
