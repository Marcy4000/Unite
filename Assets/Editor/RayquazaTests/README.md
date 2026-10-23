# Rayquaza capture buff

Rayquaza has its own objective type; Zapdos and goal weakening are unchanged.
Capture awards 30 Aeos Energy to the securing player and 15 to each teammate
(including dead teammates), capped by normal carry capacity. Living teammates
receive `floor(maximum HP * 0.3) + 3000` shield HP.

The shield is not timed. While it remains, it provides 4x base scoring speed,
protected scoring, and 40% increased damage against enemy players (physical,
special, and true damage, including item/periodic damage through the shared
damage pipeline). It is consumed before every other shield. Death removes it;
respawning does not restore it. Scoring does not consume it.

**True damage bypasses shields and interrupts scoring even when the Rayquaza
shield survives.** A hit that breaks the Rayquaza shield also interrupts normally,
even if another shield absorbs the rest. Hindrances cannot interrupt an already
protected scoring channel; this does not grant general combat hindrance immunity.
Voluntary cancellation, leaving a valid goal, and death still end scoring.

The green/gold shield prefab is in `Assets/Resources/Effects/RayquazaShield.prefab`.
It is driven locally from replicated shield state, respects vision, and is not
a network prefab. `Tools/Rayquaza/Create Missing Shield Visual` recreates missing
authored assets without replacing existing artwork.

## Verification

- EditMode assembly: `Unite.Rayquaza.Tests` (rules, scoring, assets, visuals).
- PlayMode assembly: `Unite.Rayquaza.PlayMode.Tests` (real isolated NGO server;
  shield ordering, shield break, true damage, death/respawn, damage targeting).
- Multiplayer smoke test on Theia: secure Rayquaza with living and dead allies,
  verify capped 30/15 rewards, living-only shields and unchanged enemy goals.
  Score while taking ordinary damage, hindrances, true damage, and shield-breaking
  hits; compare host and remote-owner behavior. Verify visuals hide in fog/bushes,
  vanish on death/break, and appear correctly for a late joiner.
- On Remoat, secure Zapdos and verify enemy goals still become defenseless.
