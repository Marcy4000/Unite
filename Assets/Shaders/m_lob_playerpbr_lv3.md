# Lobby player PBR (URP)

`m_lob_playerpbr_lv3.shader` reimplements
`Compiled/==PGAME==_LobbyPlayer_m_lob_playerpbr_lv3.txt` for the project's Unity 6 / URP version.
The shader asset GUID is unchanged; its ShaderLab name is restored to
`==PGAME==/LobbyPlayer/m_lob_playerpbr_lv3` (previously suffixed `_URP_Standard`).

## Texture setup

| Texture | Channels | Import settings |
| --- | --- | --- |
| `_MainTex` | Diffuse RGB, perceptual roughness A | Default texture; sRGB enabled for color |
| `_BumpMapNr` | Tangent-space normal RGB, metallic A | **Default texture, sRGB disabled**, preserve alpha; not Unity Normal Map |
| `_BumpMap2` | Detail normal RGB | **Default texture, sRGB disabled**; independent tiling/offset |
| `_MixMap` | Opacity R, ramp/matcap mask G, emission mask B | Default texture, sRGB disabled |
| `_MatcapTex` | Environment RGB | Enable mipmaps; uses roughness-dependent mip levels |
| `_OutlineMap` | Outline color RGB, width A | Sampled at mip zero with the mesh's original UVs |

The dump labels `_MixMap.a` as NormalMask, but none of the recovered programs uses it;
it remains reserved rather than inventing a normal-masking operation. There is no AO
channel in the recovered shader.

The recommended packed-data imports above use encoding **LinearRGBA (0)**. For existing
converted assets, `_PackedNormalEncoding` and `_DetailNormalEncoding` also support Unity
Normal Maps (1) and sRGB RGBA data (2), independently. Unity normal compression may replace
alpha, so mode 1 uses the scalar metallic offset instead of treating encoded normal X as
metallic. The clothing converter detects these modes without changing texture importers.
`_RoughnessAlphaSquared` is an off-by-default adapter for legacy/bag/sock roughness layouts;
ordinary player PBR retains its original unsquared-alpha calculation.

Material flags are evaluated from their saved float properties. Inspector toggles retain
the dump's keyword names, but toggling them through `Material.SetFloat` also works without
separately synchronizing keywords. `_ALPHATEST_ON` is additionally supported directly.

## Recovered behavior

- Roughness is `clamp(MainTex.a + offset, 0.04, 1)`; metallic comes from packed-normal alpha.
- Normal strength scales XY only; detail normals use reoriented normal mapping.
- Diffuse, the original view-dependent specular lobe, six-mip reflection probe sampling,
  quadratic top/bottom ambient, fixed classic lobby SH coefficients, tinted directional
  rim, emission, and the original rational tone curve are restored.
- Matcap environment replaces the probe **and** main-light direct specular. Ramp and
  matcap highlights are additive and masked by `_MixMap.g`; ramp takes priority if both
  modes are enabled. The ramp is indexed by the squared view-normal cosine, not N.L.
- `_MainPlayerPositionWS.xyz` is actually an **object-space** bag deformation center in
  the dump. W is its radius. Zero radius disables deformation safely.
- The outline restores vertex-color/texture tinting, optional tangent-space smooth normals,
  constant screen-width extrusion, and depth bias. `_Offset` controls depth bias, not width.
  `_OutlineEnabled` can disable it. `_ScreenResolution.y` defaults to a 1080 reference height.

Forward-only lighting also works with URP's deferred renderer. ShadowCaster, DepthOnly,
and DepthNormalsOnly use consistent bag geometry and mask clipping. Main-light cascades,
screen-space shadows, additional lights (including clustered Forward+), fog, instancing,
and stereo setup use current URP helpers. No materials or scenes are migrated automatically.

## Alpha and render state

Main texture alpha is **roughness**, never opacity. Enable `_AlphaClip` or `_ALPHATEST_ON`
to clip `_MixMap.r` against `_Cutoff`. Without a mask, opacity is one.

For transparency, set `_BlendMode = 1`, source/destination blend factors to the intended
blend mode, disable `_ZWrite` if appropriate, and set the material's render queue and
RenderType override. `_BlendMode` alone changes output alpha, not queue or blending.
Opaque defaults use Geometry queue, backface culling, depth writes, and One/Zero RGB blending.

The separate alpha channel always accumulates coverage using `One/OneMinusSrcAlpha` and
Add; archived alpha factor/operation properties remain saved but are not applied. This
prevents soft masks from reducing opacity over opaque surfaces in UI camera captures.
Outlines use the `TrainerOutline` tag and require `TrainerOutlineRendererFeature`, already
installed on all project quality renderers. See `../UI/UIObject3D/Rendering.md`.

## Reconstruction limits and extensions

The dump contains selected compiled variants rather than the original source. Missing
debug modes are reconstructed as raw channel previews (AO displays white). Classic SH
uses the dump's fixed coefficients, not the current scene's light probes.

The previous reimplementation's `_SHColorScale`, extra directional light, and diffuse
shadow-shaping controls are retained as neutral-by-default extensions. The extra light
defaults to black. Fog, normal-depth output, consistent auxiliary-pass clipping/deformation,
and guards for degenerate normals/rim thresholds are URP compatibility improvements.
Full original-game pixel parity still requires its lighting, textures, and post-processing.

## Tests

Run the **Unite.Shader.Tests** EditMode assembly in Unity Test Runner. It compiles five
passes with representative URP keyword sets and uses GPU readback to verify packed
channels, normal strength/detail blending, mask alpha/clipping, ambient coefficients,
emission/tone mapping, normal-depth output, and degenerate inputs. GPU tests require a
graphics device and are skipped with `-nographics`.
