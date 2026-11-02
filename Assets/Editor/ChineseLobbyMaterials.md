# Chinese Lobby materials

Use **Tools > Chinese Lobby > Restore Materials From JSON** to rebuild the materials
for `pref_sce_lobby_newcity_online_lv3` (the model used by `ChineseLobby.unity`) and
its companion `pref_sce_lobby_newmaincity_01_lv4` effects model.

The importer reads each model's `Materials/*.json`, resolves textures inside that
model's own folder, and creates or updates a same-named `.mat` beside the JSON.
It preserves the original saved floats, integers, HDR colors, texture references,
and texture tiling/offsets, including legacy properties not exposed by the shader.
Null texture references remain null. Four non-null reflection-cubemap references
have empty names in the source export. Explicit mappings use the subsequently
supplied `Assets/Cubemaps/HDRI_newcity_techglass.dds` for glass01/glass01a/glass02
and `HDRI_CartoonWater01.dds` for water06. These are semantic asset-name matches,
not verified original PathID mappings; the source JSON cannot identify them.
The importer enables `_ReflectionCubeAvailable` for assigned cubes; otherwise
the reconstructed shader uses the current Unity reflection probe.
Unrecognized unnamed references are reported and left unassigned.
Original environment render queues and queue
offsets are restored instead of using the stale URP `_Surface` values in the exports.

The importer preflights every shader and named texture before changing assets;
missing dependencies stop the operation rather than silently substituting URP Lit.
Materials are updated in place so their GUIDs survive subsequent runs. Both FBX
importers receive explicit external-material remaps, which survive reimport and
apply to existing scene instances without unpacking the model or editing the scene.

Hologram `_TEX` and `_WSUV` keyword enums are restored from their saved floats.
The command also installs an idempotent **Chinese Lobby Hologram Stencil** Render
Objects feature on gameplay renderer assets in `Assets/Settings`: it draws only `FurPass1`
before transparent rendering, preserving the original hologram stencil contract.
Without this pass, the hologram's stencil-equal color pass would be invisible.
The dedicated trainer UI-preview renderer is deliberately unchanged.

Environment packed normal/roughness, MROE, and custom lightmap textures are imported
as linear RGBA data (`Default`, sRGB disabled, alpha-as-transparency disabled).
They must not be converted to Unity NormalMap/DXT5nm encoding, which discards or
rearranges channels these shaders sample directly. FX hologram `_MixMap` textures
are color images and are deliberately excluded from this data-texture rule.

Run the `Unite.ChineseLobby.Tests` EditMode assembly to check all source properties,
shader compilation/support, render queues, local texture resolution, and renderer
material bindings. Shader reconstruction notes live under `Assets/Shaders/ChineseLobby`.
