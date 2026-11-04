# Alolan Ninetales accuracy checks

Reference data: `Assets/Moves/Dumps/vulpix_skills.json` and
`Assets/Moves/Dumps/age-named-v2/3c44b23/age_new_037_*_pbb.bin.md`.

## Geometry adopted from the extraction

Distances are normalized by 1000, consistent with the databin indicator ranges.

| Reference | Implemented geometry |
| --- | --- |
| `P1B1`, `MRadius=2000` | Snow Warning radius 2, including its warning visual. |
| `S22B1`, `MRadius=4000` | Aurora Veil radius 4 for caster and allies. |
| `A1B2` / `A1B4`, `Cube`, size X=3000 / Z=4000 | Boosted basic forward-oriented 3-by-4 box; use the forward-offset branch's Z=1500 offset. |
| `S2B1`, `Cube`, size X=2000 / Z=8000 | Stationary damage line, width 2; retain the databin's separate width-2.5 aiming indicator. |
| `S21B1`, initial `Cube`, X=4000 / Z=10000 | Initial stationary line hit, not a travelling blast. |
| `S21B1`, moving `Sphere`, `MRadius=2750` | Rounded second wave and frost radius 2.75; sweep between frames and handle initial wall overlap. |
| `S11B1`, moving `Cube`, X=4000 / Z=5000 | Travelling 4-by-5 gust, one hit per victim. |
| `S11B1`, collapse radius 1000, limit 2500 | Expanding collapse from radius 1 to 2.5, one hit per victim across all samples. |

The extraction is a declaration reader, **not an execution trace**. Scalar radius
fields and shape enums are metadata-backed; nested vector decoding is explicitly
marked as a candidate by the exporter. The tests compare the selected geometry
against these declarations; they do not certify complete runtime equivalence.

## Deliberately retained project tuning

Damage/healing formulas, buff percentages, speeds, cast/recovery timings, freeze
duration, snow expiry, wall lifetime, and frost tick timing remain unchanged.
Avalanche's existing 0.25-second collapse presentation is now sampled for growth;
its gust uses the existing project wind speed. AGE raw times are not converted
into seconds. Icy Wind retains two damage applications and its existing shove
policy; its separate six-unit auxiliary region has unresolved effect semantics.

Do not infer missing upgraded-entry behavior, near-target boosted-attack branch
conditions, Avalanche's bullet-type-specific blocking policy, passive sight rules,
or Snow Globe's primary timeline from this extraction. The referenced `*_plus`
entries and `age_new_037_G1` are absent; `U1B1` alone is insufficient to replace
Snow Globe's current timing or collision policy.

## Tests

- EditMode assembly: `Unite.Ninetales.Tests` (source-backed radii/box dimensions,
  existing formulas/assets, and warning presentation).
- PlayMode assembly: `Unite.Ninetales.PlayMode.Tests` (dedicated server and host:
  outer-ring boundaries, rotated boosted boxes, line/wave phases, wall overlap,
  swept gusts, per-victim hit limits, and existing lifecycle/cleanup regressions).
