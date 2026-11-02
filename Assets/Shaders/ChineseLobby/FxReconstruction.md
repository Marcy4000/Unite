# ChineseLobby FX reconstruction

## Scope

These 15 shaders cover all 113 FX shader references in the material JSONs in
`Assets/Model/Lobby/pref_sce_lobby_newcity_online_lv3/Materials` and
`Assets/Model/Lobby/pref_sce_lobby_newmaincity_01_lv4/Materials` at reconstruction
time. Shader names are case-sensitive and unchanged:

- `==PGAME==/Fx/m_1P_Base`
- `==PGAME==/Fx/m_1P_Base_Half`
- `==PGAME==/Fx/m_1P_fresnel`
- `==PGAME==/Fx/m_1P_scroll`
- `==PGAME==/Fx/m_1P_scroll_LerpColor`
- `==PGAME==/Fx/m_2P_dissolve`
- `==PGAME==/Fx/m_2P_dissolve_Half`
- `==PGAME==/Fx/m_2P_fresnel_dissolve`
- `==PGAME==/Fx/m_2P_mask`
- `==PGAME==/Fx/m_2P_mask_LerpColor`
- `==PGAME==/Fx/m_2P_mask_quickmask`
- `==PGAME==/Fx/m_2P_noise_LerpColor`
- `==PGAME==/Fx/m_3P_Noise_Mask`
- `==PGAME==/Fx/m_3P_noise_dissolve`
- `==PGAME==/Fx2/m_holographic2Pass`

Each `Fx_m_*.shader` uses the property declarations, labels, attributes, ranges,
defaults, and spellings from its corresponding `Assets/Shaders/Compiled` dump.
`FxCommon.hlsl`, `FxShadow.hlsl`, and `FxHolographic.hlsl` contain reconstructed
HLSL. Only new `Fx*` assets and their metadata/documentation belong to this work;
material restoration, FBX remapping, renderer configuration, and scene changes
are deliberately outside its scope.

## Recovered behavior

- UVs use the explicit lowercase `_tex1_st`, `_tex2_st`, and `_tex3_st` vectors,
  not Unity's automatic texture `_ST` values. Scrolling adds
  `frac(_TimeParameters.x * speed)` to the scaled/offset UV. The scroll shaders
  use `_Tex1_Scroll`; the other animated shaders use `_TexN_Speed`.
- Texture power, emission, and vertex RGB multiply the main color. Color
  interpolation uses `abs(sin(time * _ColorLerp))`, not a triangle wave or a
  remapped sine. Source color alpha is not an extra opacity multiplier.
- Masks sample red. Noise offsets are `(noise.r, 1 - noise.g) * strength`, not
  centered noise; distorted main UVs are computed in the fragment stage.
- Dissolve opacity is a cubic ramp of
  `sample.r + 2 * _DissolveOffset1 - 1` between `1 - _DissolveEdge1` and
  `_DissolveEdge1`. This is a soft alpha operation, not alpha clipping. The
  Fresnel/dissolve variant intentionally does not multiply main-texture alpha.
- Fresnel adds `pow(1 - saturate(dot(normal, view)), _FresnelPower1)` times its
  separate RGB/emission. Normals and view vectors are normalized in world space.
- Quick masking uses the untransformed mesh U or V coordinate and the source's
  two clamped ramps, offset, edge, and toggles.
- `_IF_ADD_MODE` premultiplies RGB by effect opacity **before** the global alpha
  multiplier. `_Aplha1` and `Vector1_AC3F40EB` retain their original misspellings.
  Base Half retains its special output alpha of `0.001` when the RGB source and
  destination blend factors multiply to one.
- `_Zoffset` shifts clip depth by offset divided by clip W; the sign adapts to
  reversed Z. It is not a world-space translation or ShaderLab polygon offset.
- The original `FOG_LINEAR` variants of the ordinary FX shaders do not apply
  fog; that behavior is retained. Hologram color does apply linear fog.
- Non-Half ordinary shaders retain solid, non-alpha-clipped shadow passes.
  Neither Half shader adds a shadow pass absent from its compiled source.

## Render state and URP adaptation

The dumps explicitly warn that they are not valid shaders. Their zeroed
`Blend Zero Zero`, `ZTest Off`, and stencil-mask entries do not preserve usable
material-property bindings. Forward blend factors (including separate alpha),
blend operations, depth comparison/write, culling, alpha-to-coverage, and
ordinary-shader stencil operations are reconstructed as bindings to the matching
original properties. This recovers the state stored by the material JSONs,
rather than copying the nonfunctional zero placeholders. Exact lost custom
ShaderGUI/state serialization logic cannot be recovered from those placeholders.

All ordinary shaders keep the exported `RenderType=Opaque` tag and transparent
queue: queue and restored blend/depth fields, not that tag, control transparent
drawing. No opaque URP Lit/alpha-test substitute is used. Material-specific
render queues and override tags must be restored by the material importer.
`_RenderType`, `_BlendMode`, and `_QueueOffset` are preserved properties, not
automatic replacements for missing custom inspector code.

URP includes/APIs target the locally installed URP 17.3 package. The original
`UniversalForwardOver` pass on Base Half is mapped to `UniversalForward` so stock
URP can draw it; `ColorOver=true` is retained as metadata. Its proprietary
color-over renderer behavior cannot be reproduced by a shader alone.

### Hologram integration requirement

The hologram retains its actual four-pass structure:

1. Color pass: stencil reference 1, comparison Equal, operation Keep.
2. `FurPass1`: deformed geometry, no color writes, stencil reference 1,
   comparison Always, operation Replace.
3. `DepthOnly`: undeformed, double-sided geometry, no color writes.
4. 2D fallback: undeformed, double-sided `_BaseMap * _BaseColor`.

Legacy Lightweight pipeline/forward/2D tags are mapped to their current URP
equivalents. `FurPass1` is intentionally not silently converted to an always-pass
stencil test. **Stock URP does not schedule this custom pass.** A renderer feature
must draw `FurPass1` before the transparent color pass, using the same depth and
stencil attachment. Otherwise the color pass is rejected wherever stencil is
not already 1. No renderer feature or renderer asset is modified here.

The main hologram vertex pass reproduces block-hashed displacement, slow
`frac(_Time.x)` flicker, and vertex-interpolated Fresnel color. `_WSUV_ON` uses
mesh UVs; `_WSUV_OFF` uses projected screen UVs with current-platform Y handling.
`_TEX_ON` multiplies `_MixMap`; `_TEX_OFF` omits it. `_MainTex` always contributes.
The original `_VROTATE_ON` keyword is retained; available variants all contain
the same recovered displacement, not an independently recoverable rotation.
The legacy 2D fallback reads `_BaseMap`/`_BaseMap_ST` even though they were not
declared as exposed properties in the original dump; no new property is invented.

## Material restoration and limitations

The JSONs contain saved float/color/texture data but no serialized keyword list.
The material-restoration code must enable `_TEX_ON` or `_TEX_OFF` from `_TEX`,
and `_WSUV_ON` or `_WSUV_OFF` from `_WSUV`, disabling the opposite keyword.
Copying floats alone does not update shader keywords. Preserve mesh vertex
colors and normals, explicit transform vectors, all saved blend/depth/stencil
fields, and custom material queues. Extra obsolete saved properties are not
evidence of active behavior absent from the compiled programs.

The exact zero-width ramp at edge `0.5` would divide by zero in the source;
reconstruction supplies a deterministic step at that boundary. Shader precision,
modern shadow bias/clamping, reversed Z, and screen-coordinate conventions can
also produce small platform differences. The original custom renderer and
inspector code, runtime animation inputs, textures, and camera/postprocessing
are not recoverable from shader dumps alone.

## Offline validation

- All 15 names match both material directories' distinct referenced FX names.
- Every shader's entire property block matches its dump after whitespace removal.
- 392 vertex/fragment HLSL syntax compilations pass with `glslangValidator` and
  temporary URP API stubs, covering pass entry points, linear-fog variants,
  reversed Z, hologram texture/UV keywords, and directional/punctual shadows.
- 60 pass/stage preprocessing checks resolve the actual locally installed URP
  headers; the APIs used are also inspected in those headers.
- This is not Unity ShaderLab import, Unity shader compilation, rendering, or
  image-parity validation. Direct glslang compilation of the unmodified URP
  headers encounters its min16float overload limitations; stub-based checks do
  not validate Unity's implementation of those headers.
- Unity refresh, material/importer edits, and scene changes are intentionally not
  performed. The parent restoration task owns import and visual verification.
