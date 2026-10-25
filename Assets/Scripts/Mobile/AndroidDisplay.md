# Android fullscreen and camera cutouts

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
- Also test an older Android phone and a phone without a cutout (no extra padding).

If Android minification is enabled in the future, preserve the JNI entry point in
the project's custom ProGuard rules:

```proguard
-keep class com.nottimistudios.unite.UniteAndroidDisplay { public static void apply(android.app.Activity); }
```
