# Clothing material restoration

Open **Tools → Clothing → Convert Clothing Materials to URP** (`AssignNormalMaps.cs`).

1. Select a material root. The default is `Assets/Model/Clothing`; narrow it to a model
   folder when reviewing an outfit. Hair-only filtering uses the **original shader family**,
   so hair embedded in a Top folder is included and shadow overlays are excluded.
2. Run **Preview conversion** and inspect its JSON report.
3. Enable **Allow approximate adapters** only if the listed visual compromises are acceptable.
4. Run **Apply conversion with backups** to update existing `.mat` assets in place.

The original data currently lives in `Materials/<material-name>.json`. JSON-formatted `.txt`
files are also supported. Compiled shader `.txt` files are reference programs, not material
data. Missing/malformed/ambiguous material dumps are reported, not guessed from the already
converted shader or a substring such as `hair`, `shadow`, `pants`, or `backpack`.

## What wins

Each conversion starts from a **fresh target-shader material**, then restores saved source
floats, ints, colors/vectors, texture references, tiling, and offsets. Adapters translate
layout/behavior differences; rim safety and render-state synchronization run **last**.

The old global face/hair presets are deliberately no longer applied. They mixed per-material
original values with unrelated defaults, forced main-height attenuation to 10/object-space,
overwrote cutoffs after source restoration, and applied the same face shader to almost every
clothing surface. Old serialized values that are absent from the source must not leak back in.
The preset assets remain untouched for other tools/manual use.

Explicit null textures stay null. `_EmissionMap` is never stuffed into `_MixMap`, since the
channel layouts are unrelated. Missing source mask/emission data disables those features
instead of using white shader defaults or the previous conversion's stale maps.

## Shader routing

| Original family | Destination | Notes |
| --- | --- | --- |
| Player skin (Lv3/Lv1), SkinNoShadow | Trainer Face | NoShadow disables receiving shadows |
| PlayerPBRHairColor | Trainer Hair | All four region colors, lobe parameters, shape ST, UV selection restored |
| Player PBR, NoOutline | `m_lob_playerpbr_lv3` | NoOutline disables outline |
| PlayerPBRColorEye | Existing PlayerPBRColorEye shader | Never routed to skin |
| Lobby HairShadow | URP Unlit | Restores `_ShadowColor`, including multiply blend state; not hair highlights |
| Legacy/Lengcy, bags, socks | Player PBR **approximation** | Squared roughness flag; bag deformation; shoe texture selection for `_SHOEMODE` |
| HairColorFelt | Hair **approximation** | Felt detail/AO/Fresnel omitted |
| Felt, fur, alpha, recolorable player cloth | Player PBR **approximation** | Base surface only; missing specialized effects listed in report |
| Unknown shaders, FX, glass, unconverted eye variants, other special shaders | Unchanged | No blanket face/hair fallback |

Approximate adapters are **off by default**. In particular, routing a recolorable cloth
surface to base PBR does not recreate its `_ColorTex` region layout, extra patterns, or hue
operations. Routing fur does not recreate shell geometry. These are intentionally explicit
fallbacks, not claims that those original shaders have been reconstructed.

Source `_OutlineWidth` coefficients are multiplied by 216 for the new hair shader, whose
property uses multiplier units; face retains coefficient units. Bag handling follows the
source shader, not the folder (even shoes can use the original bag shader). Unmapped
properties are listed in `ignoredProperties` for future shader implementations.

## Why rims became extremely bright

The old converter capped the preset rim **before** JSON overrides. Source HDR colors with
channels of 1024 or more immediately undid that cap. In addition, many dumps store
`_RimlightScale = 10` and **no `_RimlightScale2`**. Combining that old off/out-of-domain start
with the new shader's default end of 1 or 1.5 creates a reversed ramp that lights almost the
entire surface.

Default **SafeAdaptation**:

- Treats starts outside `[0,1)` and end ≤ start as disabled, not as reversed ramps.
- Uses an explicit end of 1 when it was not saved; requires a saved nonzero rim direction
  and color before enabling the rim.
- Caps the final restored HDR color to the selected maximum (default **2**), scaling RGB
  together to preserve hue and preserving/clamping alpha separately.
- Sets `_RimEnabled` on trainer shaders; on base PBR it disables the effect with black RGB
  and a safe inactive range.

**Disabled** switches rims off. **PreserveSource** explicitly opts into unbounded source
values and warns about missing original runtime globals. It is not the recommended preset
for reconstructed lighting.

Likewise, an archived high HDR `_AddlightColor` without a saved `_AddLightDir` no longer
activates an arbitrary +Y light. The color is retained, but the direction is zeroed and a
warning explains the missing runtime setup. No global cap is applied to other HDR colors.

## Textures and normal encoding

Texture lookup prefers an exact name inside the model folder. Shared lookup is restricted
to clothing assets; multiple candidates are accepted only if file bytes **and importer
configuration** match. Missing/different same-name textures fail that material atomically.
There is no "take the first global match" fallback.

The assigned normal import selects LinearRGBA (0), UnityNormalMap (1), or SRGBRGBA (2)
automatically. Base PBR now supports the same compatibility modes, plus independent detail
normal encoding and legacy squared-roughness alpha. Its default shader math is unchanged.

**No texture import settings are rewritten.** Normal-map compression may have lost original
packed alpha; compatibility mode cannot recover skin-tint/metallic data that is no longer
in the imported texture. sRGB mask/hair-map imports are reported as accuracy warnings, not
silently changed. Existing importer edits are preserved.

## Render state and safety

Saved cull/depth/blend/stencil values are restored where the target exposes them. Legacy
blend modes 1–4 select transparent scheduling, mode 5 selects alpha test, and explicit source
queue values/offsets take priority. Source blend factors are preserved, not replaced by one
global transparent preset. `_MaskOn` alone does **not** enable base PBR alpha clipping.

Preview builds disposable material copies only. Apply backs up each original material file
to `Library/ClothingMaterialConversion/<run>/backups/<asset-guid>.mat`, records Undo, and
saves only the converted material—not other dirty assets. Unsaved materials are refused.
Every run writes a JSON report with paths, routes, skips, failures, warnings, and ignored
properties. Cancellation leaves already-applied materials backed up and reported.

Conversion does not extract embedded materials, remap FBXs, create duplicate material
assets, or edit scene/prefab references. Existing GUIDs remain intact. The old explicit
`ExtractMaterials` helper is retained separately and skips existing output paths.

To restore a backup, match its GUID to the report's material path and restore that `.mat`
file, keeping the existing `.meta`. The `ClothingMaterialConverter.ConvertClothingMaterials`
entry point remains available; `ClothingMaterialUpdater` forwards the historical API name.

## Tests

Run the `Unite.Shader.Tests` EditMode assembly. Converter tests exercise routing, source
precedence, final rim policies, importer-aware encoding, null/ambiguous/missing textures,
render state, shoe maps, idempotence, preview isolation, and GUID-preserving apply/backups.
Tests that apply conversion create and remove their own uniquely named temporary assets;
the project's clothing materials are not used as write targets.
