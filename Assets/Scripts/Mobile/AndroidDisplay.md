# Mobile display, refresh rate, and zoom rendering

## Refresh-rate policy

Phone startup selects the highest advertised refresh rate, not a fixed 75 FPS.
On Android, the native plugin compares `Display.getSupportedModes()` with the
current mode's physical width and height and requests the fastest matching mode
using `preferredDisplayModeId` and `preferredRefreshRate`. It never changes native
resolution or Unity surface dimensions to obtain a higher rate. Unity's target
frame rate is the rounded selected refresh rate (including fractional rates such
as 119.88 Hz), with VSync disabled. If native discovery fails, the current Unity
display rate is used; invalid/missing rates fall back to 60 FPS.

The persistent display component reapplies this on startup, focus regain, and
resume, after Unity's deferred layout. Resolution, VSync, and quality callbacks
in SettingsManager cannot replace this mobile policy. Desktop settings are unchanged.
On iOS the target uses Unity's reported display refresh rate; ProMotion is enabled
in Player Settings. There is no Android JNI or window manipulation on iOS.

These are requests, not guaranteed delivered FPS: Android OEM settings, battery
saver, thermal limits, OS arbitration and iOS Low Power Mode can cap the device.
Switching refresh in system settings while continuously focused is picked up on
the next focus/resume or game quality change; this is not a per-frame display poll.

## Zoom rendering policy

CameraController samples the interpolated Cinemachine follow distance and FOV,
without changing zoom ownership or priority (jump pad 100 still outranks cannon 10).
At the normal 26.48-unit distance / 25-degree FOV rendering stays at the selected
quality's original URP render scale. Relative view footprint (distance times
tan(half FOV)) interpolates a scale multiplier from 1.0 at 1.15x normal footprint
to 0.8 at 3.2x, clamped beyond those bounds. Cannon zoom is approximately 0.96x;
jump-pad zoom reaches 0.8x. Updates are quantized to 0.01 scale increments to limit
render-target resizing during a zoom transition.

URP 17 exposes this scale on the pipeline asset, not per camera: it affects scene
rendering while this gameplay camera is enabled. Screen-space overlay HUD remains
at native surface resolution; camera-space/world-space UI is rendered with the
scene. Culling, visibility, shadows, simulation, input and UI layout are unchanged.
The maximum reduction is approximately 36% of scene pixel count, **not** a measured
36% frame-time improvement: geometry/CPU cost is unchanged. No asset is marked dirty
or saved. The original in-memory scale is restored at normal zoom, on disable or
destruction, and when switching pipelines. External scale changes become the new
baseline and are not overwritten on teardown.

## Android fullscreen and camera cutouts

`AndroidDisplaySettings` starts automatically in Android players; no scene component
or custom Activity/manifest is required. `UniteAndroidDisplay.java` hides both system
bars using `WindowInsetsController` (Android 11+) or immersive-sticky flags (older
Android). Edge swipes can still reveal transient system bars without resizing the game.
Startup, focus regain and resume reapply immersive mode; window-inset dispatch updates
cutout padding when the phone rotates.

Keep **Fullscreen Mode = Fullscreen Window**, **Hide Navigation Bar = enabled**, and
**Render outside safe area = enabled** in Android Player Settings. The latter avoids
Unity's legacy window-resizing behavior on older Android. On Android 15+ it has no
effect, so the native plugin owns cutout avoidance on all supported versions instead.
It pads the Activity content view with the physical display-cutout insets, leaving
a black strip at the camera edge and resizing the actual Unity surface. It deliberately
does not reserve space for hidden status/navigation bars. Do not add another global
safe-area inset on top of this surface.

## Device verification (requires a rebuilt APK)

- Cold launch in both landscape orientations: no persistent status/navigation bars;
  a black strip clears the camera cutout; circles and UI retain their proportions.
- Rotate between landscape orientations: the strip follows the camera edge, with
  no stale padding on the opposite edge. Test touches at all four viewport corners.
- Swipe to reveal system bars: they overlay temporarily and then hide, rather than
  shrinking/stretching the game.
- Background/resume, lock/unlock, and open/close the keyboard or a native dialog:
  immersive mode returns and touch targets remain aligned.
- Open/close game settings: it must not switch Android to desktop windowed mode.
- On 60/90/120/144+ Hz phones, verify the requested same-resolution mode and actual
  display refresh using Android developer overlay/ADB, including after background,
  lock/unlock and quality/VSync/resolution callbacks. Repeat with battery saver on.
- Profile normal/cannon/jump-pad zoom with identical scene content and resolution:
  compare GPU frame time and scene render-target size, verify HUD text/touch targets,
  then exit jump selection, disable the camera and switch quality while zoomed out.
  All original quality scales must return without compounding on repeated jumps.
- On a ProMotion iPhone, verify the reported rate and delivered FPS on device, with
  Low Power Mode both off and on. Editor tests cannot establish device support.
- Also test an older Android phone and a phone without a cutout (no extra padding).

If Android minification is enabled in the future, preserve the JNI entry point in
the project's custom ProGuard rules:

```proguard
-keep class com.nottimistudios.unite.UniteAndroidDisplay { public static void apply(android.app.Activity); public static float getHighestRefreshRate(android.app.Activity); }
```
