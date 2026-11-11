# Fake mesh shadows

Players and wild Pokémon (including objectives and soldiers) automatically receive a `FakeMeshShadow` component when their model loads. Evolution rebuilds it for the new model. Unite Royale trainers receive it after their clothes load, and rebuild it when their outfit changes. Open world lobby avatars, Pokémon followers, trainer NPCs and Pokémon NPCs also receive shadows automatically. Trainer outfit changes refresh their shadow meshes; follower shadows follow model placement and visibility. Generic interaction NPCs do not receive shadows. This is local presentation and needs no network state, light, renderer feature, or rebake.

The component renders a second copy of each MeshRenderer/SkinnedMeshRenderer through `Unite/Fake Mesh Shadow`. The vertex shader projects the animated silhouette onto the ground plane. Bones and blend shapes follow the source model; no per-frame `BakeMesh` calls or shadow maps are used. Shadow objects are children of their source renderers, so model visibility and hierarchy activation also hide shadows.

## Tuning

Add `FakeMeshShadow` to a player, wild Pokémon or Unite Royale trainer prefab to save settings in the Inspector. If absent, their controller adds it at runtime with defaults. For other objects, add the component to their root and optionally assign Source Root to just the visible mesh hierarchy.

- **Shadow Color:** black with alpha 0.3 by default.
- **Light Bend:** world X/Z displacement per unit above ground. `(0, 0)` projects straight down; `(0.5, 0)` stretches toward world +X. This is the direction the shadow extends, away from the light.
- **Ground Layers:** use only walkable collider layers. Defaults exclude Players, WildPokemons, UI, UIObject3D and Ignore Raycast. Triggers and the object's own colliders are ignored.
- **Probe Height / Distance:** probe starts 2 units above the root and reaches 12 units below it. No suitable ground hit hides the shadow.
- **Maximum Slope:** rejects steep surfaces, default 45 degrees.
- **Surface Offset:** 0.015 units above the plane to avoid flicker.
- **Shadow Material:** optional override; the default Resources material keeps the shader included in builds.

The component samples one plane beneath the root, which suits flat maps and gentle slopes. It cannot wrap across steps, curved terrain or ledges, and projects filled geometry without texture alpha cutouts. Transparent mesh effects should be kept outside Source Root. An extreme bend is clamped to keep projection directed into the ground plane.

The shader reserves **stencil bit 0 (mask 1)** until the camera clears its depth/stencil buffer. Overlapping triangles, model parts and adjacent characters blend once, keeping opacity uniform. World effects using that stencil bit must use a different bit. Cameras need a stencil-capable depth target (the normal URP camera target supplies one); camera stacks sharing depth also share the mask.

Cost is one ground query per enabled character per frame plus extra mesh draws and skinning. It avoids realtime shadow-map rendering, but high-poly characters still cost more than blob shadows. Profile on target devices; simplify shadow meshes if needed. Existing realtime shadow settings are left as authored.

Editor rendering tests cover projection from outside the clip volume, directional bending and overlapping geometry opacity.
