# Character capture and outline rendering

## Alpha has two stages

UIObject3D captures a camera into an RGBA texture, then presents it as a UI image. Camera
RGB is already composited against the clear color. Rendering that texture through the
ordinary straight-alpha UI shader multiplies it by its alpha **again**, producing dark,
over-transparent soft edges.

`UIObject3DImage` now uses `Resources/UIObject3DPremultiplied.shader` as its default material:
`Blend One OneMinusSrcAlpha`. Tint/CanvasGroup alpha and RectMask2D softness multiply both
RGB and alpha, and the usual stencil Mask and UI alpha-clip properties are supported.
An explicitly assigned custom UI material still takes precedence: it must also understand
premultiplied camera captures. No custom materials or UI prefab references are overwritten.
The shader lives in Resources so runtime-created images also work in player builds.

The reconstructed face, hair, clothing, and eye shaders accumulate **coverage** using
`One OneMinusSrcAlpha` for the separate alpha channel. Their existing **RGB blend factors**
remain unchanged. In particular, archived alpha `SrcAlpha/OneMinusSrcAlpha` incorrectly gave
`a*a + destinationAlpha*(1-a)`: a 50% hair fade over fully opaque skin reduced captured alpha
to 0.75, letting the UI behind the character show through. Correct coverage is
`a + destinationAlpha*(1-a)`, which remains 1 over opaque skin.

`_SrcBlendFactorAlpha`, `_DstBlendFactorAlpha`, and `_BlendOpAlpha` remain serialized for dump
and converter compatibility, but no longer control these shaders' alpha coverage channel.
Lighting, mask clipping, RGB blending, and source material/import settings are unchanged.
True holes below the cutout threshold still reveal whatever is actually behind the mesh.

Use the default transparent **black** clear color for transparent captures. Other custom
shaders rendered into the same texture must also write correct coverage. A camera
background with nonzero RGB but zero alpha is not valid premultiplied content.

`UIObject3D.Render` also binds its own target before optional `GL.Clear` on both copy paths,
and always restores the previous active target (including on exceptions). It no longer
accidentally clears an unrelated active camera/UI render texture on the CopyTexture path.

## Explicit outlines

The three reconstructed outlined shaders use the custom **TrainerOutline** LightMode instead
of relying on `SRPDefaultUnlit` as an implicit second draw. Their regular forward passes
remain unchanged. `TrainerOutlineRendererFeature` explicitly draws opaque outlines after
opaque surfaces and transparent outlines after transparent surfaces. This avoids the
standard opaque pass's depth-priming `Equal` override rejecting expanded hull pixels.

The feature uses URP's RenderObjectsPass (Render Graph and compatibility implementations),
and is installed once on each Performant/Balanced/HighFidelity renderer asset. It does not
filter out Scene View or manually rendered UIObject3D cameras, or hard-code a character
layer: normal camera culling still applies. Only the dedicated outline tag is drawn, not
other SRPDefaultUnlit materials, and the regular pass cannot draw the outline a second time.

When adding a **new custom renderer asset**, add **Trainer Outline Renderer Feature** once.
Without it, the surfaces still render but these outline passes are not scheduled. Existing
per-material outline enable, width, mask, tint, and depth-offset controls remain authoritative.
This does not add an outline to the eye shader (the original eye dump has none).

## Regression tests

Run `Unite.Shader.Tests` in EditMode. Tests include actual manual URP camera renders into
transparent targets, with opaque/transparent queues and Game/SceneView camera types, and
verify that outlines add silhouette pixels outside the regular surface. GPU tests also
check hair/skin/clothing coverage over clear and opaque destinations, premultiplied UI
presentation with UI fading/clipping, UI shader variants, custom-material precedence, and
outline-feature registration in every quality renderer. Tests create only temporary objects.
