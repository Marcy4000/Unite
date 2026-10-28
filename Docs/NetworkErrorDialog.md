# Editing the network-error dialog

Open `Assets/Resources/UI/ConnectionFailureDialog.prefab` in Prefab Mode.
The hierarchy contains a Canvas, Backdrop, Dialog, Accent, Title, Message, and OK
button with a label. Edit their layout, colors, fonts, sprites, and button states
directly in the Inspector. The prefab stays active for convenient authoring.

The root `ConnectionFailureDialog` component holds three serialized references:
Title Text, Message Text, and Dismiss Button. Preserve these references, or reassign
them if you replace those objects. Their names and hierarchy can otherwise change.
Keep the backdrop raycast-enabled if clicks should not reach UI behind the modal.

`ConnectionFailureNotice` on the persistent lobby controller lazily spawns this
prefab when an error occurs and reuses it across scenes. It changes only the title,
message, visibility, and event-system selection; visual styling and the OK label
come from the prefab. Dismissal is wired at runtime, so no scene-specific button
reference is necessary. Do not add this resource prefab to NGO's network-prefab
list: the error UI is local and must work after networking stops.

Keep the default resource path unchanged. For an alternative prefab, add
`ConnectionFailureNotice` to the lobby-controller object in your scene and assign
its Dialog Prefab field; the controller reuses an existing presenter component.
No scene edits are required when using the default prefab.

To preview, open the prefab or temporarily drag it into a scene with an EventSystem.
Remove temporary preview instances afterward; the runtime presenter spawns its own.
