# PlayerPBRColorEye reconstruction

Reference: `Compiled/==PGAME==_LobbyPlayer_m_lob_PlayerPBRColorEye.txt`.
The shader name, existing material properties, and asset GUID are unchanged.

## Recovered forward behavior

- **UV1** (second mesh UV channel), transformed by `_MainTex_ST`, samples both textures.
  `_MixMap`'s own tiling/offset are unused. `_EyeUV = 0` is an explicit compatibility option
  for meshes authored for the previous UV0 conversion; the original uses `_EyeUV = 1`.
- Mask **A** selects four overlapping triangular region weights centered at 0, 1/3, 2/3,
  and 1: `saturate(1.03191495 - abs((a - center) * 3.19148946))`.
  Weights are not normalized. Region color alpha is unused.
- The region tint is multiplied by **1.2**; mask **G** blends this tint toward white.
  This result multiplies diffuse RGB.
- Mask **B** always multiplies the tinted result by `1 + B`. Both recovered forward
  variants ignore `_Emissive`, so it remains a saved compatibility property, not a gate.
- Output alpha is mask **R**, never main texture alpha. Lighting, shadows, fog, normals,
  `_BaseColor`, and the legacy outline parameters do not modulate forward RGB.
- Without `ENABLE_POST_TONE`, the result is
  `min(pow(color * 0.45 / (color * 0.45 + 0.155) * 1.019, 2.2), 100)`.
  It is **not saturated to 1**. With `ENABLE_POST_TONE`, internal tone mapping is bypassed.
  Negative inputs are guarded to avoid undefined fractional powers; black stays black.

No material, texture, importer, or scene assets are rewritten. Diffuse normally uses an
sRGB color import; packed masks should be linear data. Existing mask import settings are
left alone, so incorrectly imported data can still differ from the original appearance.

## Modern URP and compatibility choices

The forward LightMode is `UniversalForwardOnly`, keeping the unlit eye calculation in
both forward and deferred URP rendering. One shared material buffer supports SRP batching;
passes support instancing. The existing blend, depth-write, and cull properties now drive
render state instead of being ignored. `_ZTest` is an additional depth-test control.
Changing `_Surface`, `_Blend`, or `_QueueOffset` alone does not automatically synchronize
blend factors or the material render queue; use the actual render-state controls/queue.
RGB blend factors are retained; the separate alpha channel uses `One/OneMinusSrcAlpha`
coverage so eye alpha cannot punch holes through skin in transparent UI captures.

The dump's zero-valued dynamic render states are not reliable evidence for literal
`Blend Zero Zero` (which would erase the rendered color). Property-driven states retain
useful opaque defaults and allow saved per-material overrides.

The surviving ShadowCaster/DepthOnly programs are generic, unclipped passes with UV0/
`_BaseMap_ST`, unrelated to the recovered forward mask path. Default rendering remains
unclipped. If the optional `_AlphaClip` extension is enabled, all modern 3D passes instead
use the same selected eye UV and mask R cutoff, avoiding inconsistent silhouettes.
Shadow bias uses URP's light direction/position, normal bias, and shadow clamping rather
than the legacy outline `_lightDir`. A DepthNormalsOnly pass is added for URP depth effects.
There is no outline pass in the dump; `_OutlineColor`, `_Offset`, and `_lightDir` remain
available for serialized compatibility but are not invented into a new effect.

The recovered Lightweight2D program is simply UV0 `_BaseMap * _BaseColor`. Its modern
Universal2D equivalent can be selected with `_UseBaseMap2D = 1`. The default keeps the
previous conversion's `_MainTex * _BaseColor` fallback to avoid breaking existing 2D use.

## Validation

`PlayerPBRColorEyeTests` in the `Unite.Shader.Tests` EditMode assembly checks the recovered
region centers/transitions, green tint bypass, unconditional blue brightening, red alpha,
tone/HDR behavior, UV selection/shared ST, blend state, optional clipping, 2D fallback,
and representative pass/keyword compilation. These verify the reconstruction against the
surviving equations, not pixel parity with unavailable original runtime globals/rendering.
