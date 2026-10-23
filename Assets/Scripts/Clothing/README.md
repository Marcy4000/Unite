# Trainer loading and Addressables

## Runtime

`TrainerOutfitResolver` resolves the face/eye relationship, shirt/sock variants,
and clothing exclusions before issuing loads. `TrainerModel` reuses garments whose
runtime keys are unchanged and loads only replacements, in parallel. The previous
outfit remains visible until replacements are ready. Rapid requests are coalesced
to the newest appearance; disabling the model releases pending requests and retries
the latest appearance on re-enable.

The loading text is only shown before the first successful outfit. Later swaps
keep the previous outfit visible without an overlay. `TrainerModel.Awake` disables
the authored debug clothing's renderers immediately; the prefab remains visible
in the editor, and its bounds stay available for UIObject3D camera sizing. Loaded
trainer clones preserve their real garments and do not show a loading-text flash.

## Wardrobe UI

`VirtualClothingList` takes over the authored fixed-height vertical layout at runtime.
Only the viewport's rows and a small overscan margin are instantiated. Scrolling and
category changes reuse the row pool; selection is stored by clothing index even
when the selected row is offscreen. The content's full height is retained for normal
scrollbar behavior. `ClothingSelectionItem` releases icon handles when recycled or
hidden, and clears stale icons before rebinding. No prefab layout edits are required.

Garments are manually instantiated from `LoadAssetAsync<GameObject>` handles.
`TrainerClothingInstance` owns each handle and only the materials customized for
skin/hair/eyes. Ordinary UIObject3D clones acquire their own prefab handles in
`Awake`; do not replace this with `ReleaseInstance` on manually instantiated clones.
There is no permanent wardrobe-wide cache: live trainers retain their assets,
and replacement loads overlap the previous outfit's lifetime to avoid bundle churn.

## Packaging

After importing or regenerating clothing, run:

**Tools > Clothing > Optimize Addressables Packaging**

The migration is repeatable and preserves source assets, GUIDs, existing explicit
addresses, outfit indices, and inherited preload labels. It removes broad folder
entries from the original `ClothingItems` group and partitions content as follows:

| Group | Packing |
| --- | --- |
| ClothingItems | Individual model roots, with item-specific dependencies |
| ClothingShared | Shared materials/textures grouped by the exact set of models using them |
| TrainerShaders | Small shared shader bundle |
| ClothingIcons | GUID-prefix buckets for wardrobe thumbnails |
| TrainerCardArt | Individual trainer-card images |

No category-wide clothing folders or source JSON files should remain addressable.
LZ4 and requested-assets-only loading are used. CRC checks remain enabled; changing
integrity policy is a separate measured decision, not part of this migration.

**Rebuild Addressables and the player for each target.** Repackaging requires a new
full content build/player baseline, not an update against the previous content
state. Do not publish the new remote catalog to older players without a compatible
content-release strategy.

## Verification

Run the `Unite.Clothing.Tests` EditMode assembly and the
`Unite.Clothing.PlayMode.Tests` PlayMode assembly (the latter uses Asset Database
play mode for lifecycle integration tests, not performance measurement). Compare Development Player captures
on Windows and Android, not Asset Database play mode. Test cold first appearance,
repeat appearance, one-slot changes, gender changes, rapid selections, disabling
during loading, and the ten-player loading screen.

CPU markers: `TrainerClothes.Resolve`, `TrainerClothes.Instantiate`,
`TrainerClothes.Commit`, and `TrainerClothes.Materials`. Use the Addressables Profiler
for asynchronous asset/bundle loading; these markers do not span asynchronous waits.
Check the new Addressables Build Report for model dependency closures, largest
bundles, source JSON inclusion, and duplicated dependencies. Test UIObject3D clones
after destroying their source trainer to verify independent asset lifetime.
