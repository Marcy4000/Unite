# Trainer face and hair accuracy corrections

The two existing shader names and GUIDs are unchanged. Material assets, texture imports,
presets, and `TrainerModel` are not rewritten. Shared pass code is in `TrainerLighting.hlsl`.

## TrainerHead portrait colors

`PlayerHeadUI` uses `UI/TrainerHead` for the hair and pupil layers instead of multiplying
the saved color into an already rendered sprite. The shader reconstructs the pre-tone
value of each neutral sprite pixel, applies the saved trainer color, and reapplies the
appropriate hair or eye curve. `TrainerColorTone.hlsl` is shared with the 3D shaders so
their tone response cannot drift independently. Saved colors are passed as shader color
uniforms, like `TrainerClothingInstance`, not converted to linear a second time.

The neutral sprite supplies the baked lighting and eye brightness; these are not added
again during recoloring. White retains the original sprite, black stays black, and alpha
is not tone mapped. Each portrait owns and reuses its color materials, including updating
stencil copies when recolored inside UI masks. Materials in `Resources/UI` also cover
older embedded `PlayerHeadUI` instances that do not inherit the current prefab materials.

These remain static sprite portraits: this matches the shader's color response under the
sprite's baked shading, not changing 3D lighting, specular reflections, or post-processing.
`TrainerHeadColorTests` checks GPU output against both recovered curves and verifies
prefab wiring, material reuse, isolation, and cleanup.

## Packed normal compatibility

The project's assigned textures are a mixture of Unity Normal Maps and sRGB RGBA textures.
Choose **Packed Normal Encoding** to match the assigned `_BumpMapNr`:

| Encoding | Texture import | Behavior |
| --- | --- | --- |
| **UnityNormalMap (default)** | Unity Normal Map | Preserves Unity/DXT/BC5 decoding used by the previous shaders |
| **LinearRGBA** | Default texture, sRGB disabled | Exact recovered RGB decode and original alpha-channel semantics |
| **SRGBRGBA** | Default texture, sRGB enabled | Undoes the sampler's RGB color conversion before decoding the normal; alpha is unchanged |

Unity Normal Map imports can repack or discard alpha. That lost channel cannot be recovered
in a shader: in compatibility mode, face retains its previous `_MixMap.a` tint-bypass fallback;
hair uses `_MetallicOffset` as its base metallic value instead of mistaking encoded normal X
for metallic. For dump-accurate alpha behavior, use one of the RGBA modes with a texture that
preserves the original alpha. No automatic importer changes or texture-type guessing occurs.

## Face (`m_lob_playerpbrSkin_lv3` dump)

- With RGBA encoding, `_BumpMapNr.a` bypasses `_colorSkin`; it is **not metallic**.
- `_MainTex.a` is squared **before** `_RoughNessOffset` is added.
- `_MixMap.r` controls cutout alpha; `_MixMap.b` controls untinted main-texture emission.
- Ambient SH is evaluated at vertices and scaled once, including `_SHColorScale`.
- Main-light diffuse uses the original shadow shaping without an extra Lambert `1/pi`
  or metallic reduction. Specular includes the original `1.2` factor and is **not**
  multiplied by the diffuse shadow ramp a second time.
- `_faceshadowScale` is honored directly, including values set without synchronizing keywords.
- The additional-light loop uses the recovered dielectric distribution/visibility/Fresnel
  term. Main-light height and rim-height attenuation were calculated but unused in the dump's
  fragment program and are no longer applied to face lighting.
- Custom add light multiplies the combined main/ambient/probe lighting, before rim and emission.

## Hair (`m_lob_PlayerPBRHairColor_lv3` dump)

- Mask G blends the four-region tint toward **white**, not toward its inverted color.
  Region metallic/roughness offsets are weighted by **1-G**.
- The two anisotropic lobes use the **geometric bitangent shifted along the geometric normal**.
  The second axis uses **half-vector dot tangent**, not light-vector dot tangent.
- Lobe color and SH diffuse both use `albedo * (1-metallic)`.
- Hair's diffuse angular shadow shaping does not dim the anisotropic highlights twice.
- Environment reflection uses metallic F0, the recovered fourth-power grazing blend, and
  roughness-dependent surface reduction, rather than a scalar fifth-power approximation.
- Main-light height attenuation uses world Y in both surviving X/Y keyword variants. Below
  zero, the undefined logarithm is guarded to produce finite zero attenuation.
- UV0/UV1 selection now also applies to outline clipping, shadow, depth, normals, and meta.
- As in the recovered programs, additional scene lights do not add a second hair lighting model.
- Previously unexposed rim/add-light controls are available. Hair rim is **opt-in** to avoid
  unexpectedly activating archived HDR rim colors (some saved materials contain values of 1024).

## Common fixes and intentional compatibility extensions

Both shaders now implement the rational tone curve unless **ENABLE_POST_TONE** is enabled;
that keyword bypasses internal tonemapping because external post-processing will do it.
Fog uses a computed fog factor rather than raw fragment depth. Cutout alpha is preserved.

Outlines use the original screen-resolution scaling, face vertex-alpha width mask, hair
texture/vertex tint rules, and decoded smooth normals. Zero projected normals are safe;
the depth bias is adapted to reversed-Z. `_OutlineWidth` retains its existing units: an
absolute coefficient for face, a multiplier for hair. `_Offset` scales the recovered depth
bias, and `_OutlineEnabled` can turn outlines off.

Outlines are scheduled explicitly by `TrainerOutlineRendererFeature` using the
`TrainerOutline` pass tag. All three project quality renderers include it; add it once to
any new custom renderer. This supports Scene View and UIObject3D cameras without relying
on an implicit unlit extra pass or the main opaque pass's depth-priming state.

Separate output alpha uses coverage compositing (`One/OneMinusSrcAlpha`), not the archived
alpha blend factors that squared hair mask fades. RGB blending is unchanged. UIObject3D
also presents captures with premultiplied-alpha UI blending. See
`../UI/UIObject3D/Rendering.md` for the two-stage capture/compositing contract.

Each shader has one consistent material CBUFFER across its passes. Current URP helpers
handle cascade/screen shadows, punctual shadow bias, instancing, and normal-depth output.
Face supports clustered additional lights; hair preserves its original lighting model.
Hair's existing meta pass now shares the forward color/mask logic and emits the correct emission.

Only selected variants survived the dump. Classic SH uses scene SH as an extension; debug
modes are raw channel previews. Non-default normal strength and face object-space add-light
height remain compatibility controls. Several shared legacy properties are retained but unused
where the recovered programs ignored them; in particular hair uses region roughness offsets,
not `_RoughNessOffset`. Face's simple 2D fallback is not claimed as an exact dump reconstruction.

## Validation

The `Unite.Shader.Tests` EditMode assembly includes compilation checks for both shaders and
GPU readback checks for channel interpretation, encoding modes, SH scaling, emission,
tone-curve bypass, cutout/depth consistency, custom lights, face specular, hair region masks,
UV selection, and anisotropic lobe axes. GPU readback requires a graphics device.
