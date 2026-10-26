# Boosted attack counter

`HpCanvas` references the shared prefab, but **does not instantiate it by default**.
Character migrations opt in individually; attack rules remain unchanged.

`ChargedBasicAttackBase` supplies shared owner-side lifecycle and charge mapping:
**0 = empty, 1 = white, 2 = white + yellow ready**. The ready slot replaces the
last normal charge; it is not a third indicator. Death/reset clears the display;
relearning or disposing the attack removes its module. Actual combat state drives
the UI, including expiry and boosts granted by moves.

Migrated: Cinderace, Glaceon, Sylveon, Vaporeon, Jolteon, Flygon, Meowstic,
Marshadow, Emboar, Flareon, and Duraludon. Vaporeon's simple step-count gauge and
ready decoration, Meowstic's attack pips/ring, Flareon's attack pips, and Emboar's
Pledge pips are retired.
Legacy serialized pip references are retained solely to hide old authored geometry;
builders no longer create it. Passive, target-mark, heat, and storage UI is preserved.

Cresselia and Serperior keep their timed basic-attack gauges instead of a discrete
counter. Glaceon's icicle timer, Jolteon's passive gauge, Emboar's Reckless gauge,
and Duraludon's cannon/Dragon Pulse timers are unchanged and can coexist above the
counter. Glaceon's special attack modes and Vaporeon's Dive temporarily hide the
normal-cycle counter. Emboar's three stored Pledge attacks show as three yellow
icons, counting down with use, without changing the natural cycle or Reckless.
`SetChargeCounts(normalCount, boostedCount)` supports stored empowered attacks;
the usual `SetState(normalCount, ready)` still displays a single yellow ready slot.

Basic-attack counters are strictly personal. Only owner-side attack initialization
creates the UI. Server-authoritative discrete charges replicate only to the owner,
not other players. Local charge counters stay local. The counter itself has no
network state; network combat still keeps its authority and validation.

```csharp
// Capacity is the maximum number of WHITE normal-charge icons, excluding ready.
BoostedAttackCounterModule counter = manager.HPBar.GetOrCreateBoostedAttackCounter(1);
counter.SetState(0, false); // Empty row.
counter.SetState(1, false); // One white icon.
counter.SetState(1, true);  // One white icon followed by one yellow ready icon.
counter.SetState(0, true);  // Granted boost: just the yellow icon.
counter.ResetCounter();    // No icons; does not affect actual attacks.
counter.SetVisibility(false);
```

The module is presentation-only and has no charge timer, attack rules, or network
variables. The character supplies normal charges and readiness, including resets
on consumption, death, or evolution as appropriate. Visibility changes retain state;
`Cleanup` clears it. A null result means the HP bar has no counter prefab configured.

Icons use `t_common_bg_id`, are pooled, and do not intercept UI input. Icon size,
spacing, and colors can be edited on the prefab. The row is left-aligned beneath the
HP bar and placed below visible legacy gauges, the vision icon, and registered modules. Hiding or
moving those modules updates the counter position on the next `LateUpdate`;
`ShowGenericGuage` also updates it immediately.

Test coverage: `Unite.HPBar.Tests` and `Unite.HPBar.PlayMode.Tests`.
