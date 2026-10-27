# Extracted Euler animation repair

Open **Tools > Animation > Repair Extracted Euler Animations...**.

1. Select animation clips, a folder, an imported model, or an Animator Controller in the Project window.
2. Click **Add selected clips / folders / controllers**. Folders are searched recursively.
3. Choose the angles' **true source rotation order**. Use **XYZ** for the test Vaporeon exports, which contain XYZ values incorrectly labelled ZXY.
4. Leave **Bake FPS** at **0** to use each source clip's frame rate. Original key times are included, even when off the frame grid.
5. Choose an output folder below `Assets/`.
6. Optionally assign an Animator Controller and enable **Replace controller references**. This updates its states, nested state machines, and referenced blend trees. Shared blend trees will affect their other users too; reference changes support Undo. Selecting a controller adds its clips but does not enable replacement automatically.
7. Click **Create corrected copies** and confirm.

The tool bakes quaternion curves using the chosen order, uses consistent quaternion signs, and preserves valid non-rotation curves, object-reference curves, events, and clip settings. It also replaces invalid **NaN tangents** with finite neighbouring slopes (zero for constant tracks and unused endpoints), without altering key values or valid infinite stepped tangents. This prevents invalid bone positions from making the mesh disappear. Source assets are never overwritten. Existing output filenames receive unique suffixes. Quaternion-only clips (and other clips without Euler tracks) are **copied**, not converted again: their rotation keys, timing and frame rate are retained, and NaN tangents are still checked. Their filenames end in `_Copy.anim`; the Console logs each source clip name/path and destination as **skipped Euler conversion**. These copies also participate in optional controller-reference replacement. Humanoid clips requiring Euler conversion, incomplete XYZ, and conflicting rotation tracks are rejected with Console errors.

This tool does **not** automatically detect the correct order, retarget different skeletons, fix bone paths, or repair arbitrary broken animation. Use it only after diagnosing a rotation-order mismatch. It reconstructs rotations from the supplied Euler data; if extraction already lost information, request the original local quaternion curves instead. Rapid turns over 180 degrees between samples or Euler singularities between sparse source keys may require a better source export.

## Vaporeon repair

- Originals: `Assets/Model/Pokemons/Test/Anims/` (unchanged).
- Corrected copies: `Assets/Model/Pokemons/Test/CorrectedAnims/`.
- `Assets/Model/Pokemons/Test/Vaporeon2.controller` now references the corrected copies, so its existing scene Animator needs no changes.
- Keep the Animator on the model's `reference` child, since clip paths start with `Bip001`.
- The corrected stun clip also repairs NaN position tangents inherited from the export. In particular, the constant `Bip001 Spine1` Y/Z tracks evaluated to NaN until 1.66666 seconds, hiding the skinned mesh. Its key values, rotations and timing are unchanged by this tangent repair.

EditMode tests are in the `Unite.AnimationRepair.Editor.Tests` assembly. They cover conversion, sign continuity, unchanged source data, settings/events, output paths, nested controller references, and sampling the Vaporeon idle/attack initial poses on its imported skeleton.
