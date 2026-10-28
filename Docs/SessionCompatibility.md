# Session compatibility and shared UI

- `SessionCompatibility.Current` derives the exact compatibility value from
  Project Settings > Player > Version (`Application.version`), prefixed with
  `unite-app:`. Change that version whenever builds must not play together.
- New sessions advertise public `GameVersion` in `String1`; browser queries and
  quick join require the same value. This deliberately cannot match the
  `sessions-v1` value queried by already-shipped clients.
- All successful session acquisition paths pass through `ActivateSessionAsync`,
  including code/ID joins, quick join, matched sessions, and party restoration.
  Missing/legacy/different values cause an immediate leave and a readable version
  error instead of activating that session.
- The installed MPS API has no version predicate on `JoinSessionOptions` and
  automatically connects networking during a join. This is a post-join guard,
  not server-side admission control: a transient SDK membership/connection is
  possible, and unpatched clients can still attempt direct joins. Joins that fail
  inside the SDK before returning session properties show the general connection
  error, since their remote version is unavailable.
- Party Matchmaker tickets include a `GameVersion` ticket attribute and the same
  value in every player's custom data. Configure the deployed `StandardsQueue`
  match definition to require equality of player `GameVersion` values (or use
  version-specific pools selected by that ticket attribute). These client changes
  do not modify Unity Dashboard configuration. Without a backend partitioning
  rule, mixed-version assignments may still occur, but the returned roster is
  checked before leaving the party/connecting to a match and rejected with the
  version error. No permissive fallback accepts missing version data.
- Controller cursor hiding only applies during Starting/Playing, after two
  seconds of mouse inactivity. Mouse motion, wheel/buttons, keyboard activity,
  focus loss, disconnected controllers, settings/credits/keybind panels, errors,
  and non-game scenes restore the pointer. Aim consumes the same input mode.
- Boot remembers nonblank name submissions under PlayerPrefs `PlayerName` and
  restores through the existing input field, retaining its character validation.
- Connection errors use an asset-independent, scaled uGUI/TMP modal with a dark
  opaque panel, white text, selected cyan dismissal button, and raycast backdrop.

Focused EditMode fixtures: `SessionCompatibilityTests` and `SharedUiTests` in
`Assets/Editor/NetworkResilienceTests`. Live two-build UGS/Relay tests and device
visual checks remain integration checks, not offline unit-test coverage.
