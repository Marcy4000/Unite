# ChineseLobby environment reconstruction

## Scope and source availability

Read all **122** material JSON files in
`Assets/Model/Lobby/pref_sce_lobby_newcity_online_lv3/Materials`.
The 18 shaders here cover **68** of those materials. FX shaders are outside this
implementation. Only new `Environment*` files belong to this work; no material,
scene, texture importer, editor script, or render-pipeline setting is changed.
No Unity refresh, scene load, or explicit Unity compilation was requested.

**Important:** there are no compiled dumps for any of the 15
`==MAIN==/Environment_Variant_*` names or `==PGAME==/MainCity/atmosphere` anywhere
under `Assets/Shaders/Compiled`. Consequently those 16 implementations are
source-informed reconstructions, **not verified reproductions of their missing
original programs**. The two requested BattleV3 shaders do have their exact
compiled dumps. An exact atmosphere/Environment reconstruction requires the
missing dumps, source code, or reference captures.

## Exact shader-name coverage

All names below are literal shader names, not aliases or fallback mappings.

| Shader | File |
| --- | --- |
| `==MAIN==/Environment_Variant_Balloon` | `EnvironmentBalloon.shader` |
| `==MAIN==/Environment_Variant_BasicFoliage` | `EnvironmentBasicFoliage.shader` |
| `==MAIN==/Environment_Variant_Billboard` | `EnvironmentBillboard.shader` |
| `==MAIN==/Environment_Variant_Building` | `EnvironmentBuilding.shader` |
| `==MAIN==/Environment_Variant_Decal` | `EnvironmentDecal.shader` |
| `==MAIN==/Environment_Variant_FarPlane` | `EnvironmentFarPlane.shader` |
| `==MAIN==/Environment_Variant_Gloss` | `EnvironmentGloss.shader` |
| `==MAIN==/Environment_Variant_MeshBlend` | `EnvironmentMeshBlend.shader` |
| `==MAIN==/Environment_Variant_Metal` | `EnvironmentMetal.shader` |
| `==MAIN==/Environment_Variant_Mirror` | `EnvironmentMirror.shader` |
| `==MAIN==/Environment_Variant_Mix` | `EnvironmentMix.shader` |
| `==MAIN==/Environment_Variant_Sea` | `EnvironmentSea.shader` |
| `==MAIN==/Environment_Variant_TreeCrown` | `EnvironmentTreeCrown.shader` |
| `==MAIN==/Environment_Variant_TreeLeaf` | `EnvironmentTreeLeaf.shader` |
| `==MAIN==/Environment_Variant_Window` | `EnvironmentWindow.shader` |
| `==PGAME==/MainCity/atmosphere` | `EnvironmentAtmosphere.shader` |
| `==PGAME==/BattleV3/m_bat_MetalEmissColorLMV3_lv3` | `EnvironmentMetalEmissColorLMV3.shader` |
| `==PGAME==/BattleV3/m_bat_interwaterV3_lv3` | `EnvironmentInterwaterV3.shader` |

`EnvironmentInput.hlsl` shares texture declarations and a consistent
`UnityPerMaterial` constant-buffer layout. Every saved texture, float, and color
property name in the 68 matching JSONs is exposed by its corresponding shader.
Legacy/editor-only properties are retained even where their semantics cannot be
recovered. Neither material keywords nor the original custom ShaderGUI are
required to select these variants.

The atmosphere JSON has both a stale float and a vector called
`_horizonAdjust`; one shader cannot declare both. It is declared as a vector,
preserving the color/vector entry. This is the sole property-type ambiguity
found in the material inventory.

## Recovered compiled rendering

Exact available source files:

- `../Compiled/==PGAME==_BattleV3_m_bat_MetalEmissColorLMV3_lv3.txt`
- `../Compiled/==PGAME==_BattleV3_m_bat_interwaterV3_lv3.txt`

Related compiled families used to interpret the replacement Environment assets:

- `==PGAME==_BattleV3_m_bat_FullroughnessV3_lv3.txt`: RGB tangent normal,
  roughness in alpha, metallic offset, legacy direct-light highlight.
- `==PGAME==_BattleV3_m_bat_2layerterrianV3_lv3.txt`: diffuse RGB / height A,
  vertex color RG height weights, height-aware blend normalization, per-layer
  metallic/roughness/normal strengths.
- `==PGAME==_Battle2.0_m_bat_BasicScene_lv3.txt` and
  `==PGAME==_Battle2.0_m_bat_BasicFoliage_lv3.txt`: material property contracts
  for main color, normal/roughness packing, MROE, emission, foliage, custom
  lightmaps, and vertex-lightmap modes. These are related shaders, not the
  missing `Environment_Variant_*` sources.
- `==PGAME==_BattleV3_m_bat_TranslateGlassV3_lv3.txt`: reflection/glass controls
  and UV/normal contracts; the missing Environment reflection modes remain
  inferred.

`EnvironmentCommon.hlsl` implements the recovered nonstandard highlight rather
than substituting URP Lit. For roughness `r` and clamped `N.H`, the highlight is
`min((r*r / max((N.H)^2 * (r^4 - 1) + 1, .001))^2 * (.25*r+.25), 100)`.
The dielectric F0 is .04, diffuse is multiplied by `(1-metallic)`, and the probe
mip curve is `r*(1.7-.7*r)*6`. A .04 roughness floor is used for stable standalone
rendering; the MetalEmiss dump itself permits zero roughness.

### MetalEmissColorLMV3

- `_MainTex`: diffuse RGB, alpha recolor mask `saturate(A*2-1)`, independently
  exposed output alpha through `_IFMask`. `_MainColor.a` is not opacity.
- `_BumpMap`: raw RGB tangent normal mapped from [0,1] to [-1,1]; XY strength
  is clamped `_NormalStrong`, exactly as in the compiled fragment.
- `_MixMap`: R metallic, G roughness, B occlusion applied as `sqrt(B)`, A emission
  multiplied by `_IFEmiss`, original diffuse RGB, and `_EmissColor`.
- `_LightmapTex`: raw UV1, RGB irradiance multiplied by **4.84**, A direct-light
  visibility and normal-bend interpolation toward one.
- `_MaskFlip` and global `_PostUVFlip1` blend `_MaskColor`/`_MaskColor2`, matching
  the dump. Recoloring is not gated by `_IFMask`.
- Original property definitions/defaults are copied from the dump. Saved
  replacement-Environment properties are also retained, but `_MainColor` does
  not multiply this original shader's compiled diffuse path.

### InterwaterV3

`EnvironmentWater.hlsl` recovers the compiled water path separately:

- Vertex displacement is three octaves of the same sin-dot hash/value noise,
  using `.125/.25/.5` weights, original `_WaveScale`, `_WaveHeight`, and
  `_causticscaleoffset.zw * _Time.x` motion.
- `_MainTex.R` is the shore/foam threshold, **G is opacity**, not texture alpha.
- `_BumpMap.RG` supplies two world-XZ scrolling perturbations. The second layer
  has half weight; their unnormalized X component drives the foam threshold.
- `_Caustic` uses world XZ divided by `_causticscaleoffset.xy`, scrolling and
  perturbation, then `_CausticColor`.
- Reflection uses the original front dual-paraboloid lookup and fourth-power
  Fresnel, not a cubemap substitute.
- Foam uses `_foam1`/`_foam2` cubic interpolation. Opacity is
  `saturate(_CausticColor.a * main.G + foam * _FoamColor.a)`.
- The compiled vertex's light-color varying is intentionally reproduced as
  the SH evaluation input in this path; it is not silently replaced with the
  mesh normal.
- `_HeightBlendWeight` exists in the original property block but is unused by
  the recovered forward fragment. Vertex color is passed through by that dump
  but is likewise not used by its forward fragment.
- The dump's `Blend Zero Zero`/`ZTest Off` placeholders are not usable dynamic
  states. The shader binds the original `_SrcBlendFactor`, `_DstBlendFactor`,
  separate-alpha factors, blend operations, `_ZWrite`, `_ZTest`, `_Cull`, and
  `_AlphaToMask`, honoring the actual saved material values instead.

## URP integration and defaults

Targets installed URP **17.3.0**. Uses `UniversalPipeline` and
`UniversalForwardOnly` instead of the dumps' obsolete Lightweight tags.
Surface variants have matching deformed/alpha-clipped `ShadowCaster`,
`DepthOnly`, and `DepthNormalsOnly` passes. Depth normals support URP octahedral
encoding. Sky/water deliberately do not introduce opaque shadow/depth passes.
Surface blending, separate-alpha blending, culling, Z testing/writing, and
depth offset are material-driven. `_DisableShadowCaster` clips the shadow pass.

The importer remains responsible for **material render queues**. ShaderLab queue
tags cannot be driven by `_RenderType` or `_RenderQueueOffset` floats. In
particular, Sea/MeshBlend export transparent properties with an offset of -510;
the importer must restore their effective material queue. Interwater's
`_RenderType=1` is an enum, not queue 1; its shader default is Geometry+300.

Uses URP's main light, standard per-object additional lights, shadows, SH/static
lightmaps, reflection probe 0, fog, instancing, and stereo transforms. Current
project renderers use standard forward mode. Forward+ clustered light loops,
APV, motion vectors, rendering-layer MRT, light cookies, and Meta/baking passes
are not implemented. The replacement shaders do not depend on URP Lit's
`_BaseMap`/`_BaseColor` property naming.

Original global names kept outside `UnityPerMaterial`:
`_ExposureColor`, `_GIDiffuseColor`, `_GISpecularColor`, `_DarkCorner`,
`_MainHighLightOffset`, `_PostUVFlip1`.

- Unset exposure/specular-GI/dark-corner globals default to white, avoiding black
  materials when the original scene controller is absent.
- `_GIDiffuseColor` is additive to one, matching the recovered code.
- Global `_EnvironmentGlobalsConfigured=1` disables the nonzero-value heuristic,
  allowing intentionally black exposure, specular GI, or dark corners.
- Material `_EnvironmentAmbientFloor=.08` prevents black unbaked standalone
  surfaces; set it to zero for strict SH-only lighting.
- Material `_EnvironmentLegacyToneMap=1` reproduces the dumps' display curve
  `pow(1.019*C/(C+.155), 2.2)` after exposure/corner attenuation. Set it to zero
  if the active URP post stack should own tonemapping. Atmosphere does not apply
  this legacy curve by default.
- The old `_MainLightPosition.w * _MainLightColor.a` radiance packing is replaced
  by URP's Light API; copying that packing would black out modern directional
  lights.

**Texture import requirement:** packed normal/roughness and MROE maps need their
original linear RGBA channels. The reconstruction samples raw RGB/RG normals,
as the compiled GLSL does. Unity NormalMap/DXT5nm conversion or sRGB import of
packed data can destroy/change those channels. This work does not change any
texture importer. The parent importer must preserve the source packing.
The parent is implementing linear Default/raw-RGBA import for environment
normal, MROE, and custom-lightmap textures, explicitly excluding FX MixMap color
textures. Those importer changes are outside the ownership of these files.

## Inferred paths and remaining fidelity limits

Foliage variants always sample texture alpha and clip at `_Cutoff` in forward,
shadow, depth, and depth-normal passes. Legacy `_UseAlphaInput` / `_UseAlphaClip`
zeros in the export do not disable the foliage variant's intrinsic cutout.
Billboard always samples `_MainTex` because its property contract has no
`_UseMainMap` switch. Its emission is modulated by the source texture RGB/alpha
rather than adding a flat emission color over the entire image.

- Gloss interprets `_NormalMap.RGB` as normal and A as roughness, supported by
  `t_nr_*` texture names and the compiled Fullroughness family. Metal uses MROE.
- Mix uses height-A plus vertex RG weighting from compiled two-layer terrain,
  adapted to `_MainTex`/`_BlendTex`/`_NormalMap` and their saved ST transforms.
- Building/foliage use vertex red as baked visibility. Foliage has double-sided
  normals, saved tree tints, SSS, rim, optional vertex-lightmap color, and wind;
  exact custom tree lighting, normal-bending, debug controls, and min/max remaps
  are unavailable. Their properties are retained, not claimed fully emulated.
- Balloon deformation, MeshBlend vertex-alpha feathering, Building alpha
  emission, Billboard unlit display, and FarPlane unlit color are inferred.
  Billboard does not invent camera-facing geometry deformation.
- Mirror/Window use saved cubemaps and glass controls; proprietary planar or
  SSR reflection mode dispatch and legacy-glass-lightmap behavior are unknown.
- **Unnamed cubemap references:** `mat_newcity_glass01_lv3`,
  `mat_newcity_glass01a_lv3`, `mat_newcity_glass02_lv3`, and
  `mat_newcity_water06_lv3` each export `_ReflectionCube` with `IsNull=false`
  but an empty texture name. At initial reconstruction no corresponding cube
  data was supplied. These references cannot be identified from material JSON
  alone, and their
  `_Use_Reflection_Texture` enum values 3/4 must not force a null cube sample.
  Mirror/Window/Sea now default `_ReflectionCubeAvailable=0`, retaining the URP
  reflection probe instead. Once real cube data is assigned, explicitly set
  `_ReflectionCubeAvailable=1` on that material to enable it. This opt-in avoids
  unreliable shader-side null/black-texel heuristics and preserves intentionally
  black regions in real cubemaps. Importers should only enable it after resolving
  actual cube data; no importer or material was edited here.
- **Subsequently supplied cube assets:** `Assets/Cubemaps` now contains the
  user's DDS cubemaps. The parent will explicitly map glass01/glass01a/glass02
  to `Assets/Cubemaps/HDRI_newcity_techglass.dds` and water06 to
  `Assets/Cubemaps/HDRI_CartoonWater01.dds`. These are **inferred semantic
  mappings**, not recovered export names. The parent must also set
  `_ReflectionCubeAvailable=1` on those four materials to opt into the assigned
  DDS cubes rather than the existing probe fallback. No additional reflection
  shader change is needed for those supplied assets.
- Sea uses world-space coast/normal/caustic sampling, saved channel selectors,
  depth tint, foam, ripple and cubemap controls. Scene-depth/refraction and the
  exported `_WaterVPMatrixm*` projection are not reconstructed; shore/depth fade
  uses the coast map, not an invented external depth texture.
- Atmosphere uses all 13 saved palette colors with elevation/front-back blends,
  clouds/noise, horizon and rim. Its exact palette layout, horizon projection,
  and original cloud equations are unknown because its compiled source is absent.
- Non-BattleV3 defaults are sensible standalone defaults plus representative
  saved compatibility values, not recovered original shader defaults. Always
  restore saved properties from JSON for the intended materials.

## Validation

All 68 matching material JSONs have every saved property name represented;
there are no duplicate ShaderLab properties. The HLSL is checked independently
of the running Unity Editor using its bundled DXC library and the installed URP
headers. This is not a Unity ShaderLab import or a visual-fidelity test. The
parent owns Unity refresh/compilation, importer validation, and scene captures.
Final independent DXC validation passes **378 vertex/fragment programs** with
zero failures: 126 baseline programs, 126 lightmap/cascade/additional-light/
octahedral-normal/punctual-shadow programs, and 126 instanced/screen-shadow/fog
programs. No Unity refresh was invoked by this work.

Parent integration reports all 18 shaders supported with no Unity shader
errors, all 181 imported materials created/remapped, and correctly textured
Scene View rendering. Its initial 180/184 test result identified the four
unavailable cubes above, not missing environment shader names; the parent
adjusted those expectations and reports unresolved-reference warnings. This
work does not independently repeat or claim ownership of that integration run.
The later supplied DDS assets and explicit semantic mapping supersede the
initial missing-cube-data limitation, but the export's unnamed-reference
ambiguity remains. Parent owns validation of those assignments and texture
import/render-feature changes.
