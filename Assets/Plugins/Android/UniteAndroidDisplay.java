package com.nottimistudios.unite;

import android.app.Activity;
import android.graphics.Color;
import android.os.Build;
import android.util.Log;
import android.view.DisplayCutout;
import android.view.View;
import android.view.Window;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;

/** Owns immersive mode and cutout avoidance independently of Unity's legacy safe-area flag. */
public final class UniteAndroidDisplay {
    private UniteAndroidDisplay() { }

    // Called through JNI. All Android view operations must run on the UI thread.
    public static void apply(final Activity activity) {
        activity.runOnUiThread(() -> {
            try {
                final Window window = activity.getWindow();
                final View content = activity.findViewById(android.R.id.content);
                if (content == null) {
                    Log.w("UniteDisplay", "Activity content view is not ready.");
                    return;
                }

                window.clearFlags(WindowManager.LayoutParams.FLAG_FORCE_NOT_FULLSCREEN);
                window.addFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN);

                // Let our content padding handle cutouts on every Android version. In
                // particular, Android 15+ can ignore NEVER/DEFAULT cutout layout modes.
                if (Build.VERSION.SDK_INT >= 28) {
                    WindowManager.LayoutParams attributes = window.getAttributes();
                    attributes.layoutInDisplayCutoutMode = Build.VERSION.SDK_INT >= 30
                            ? WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_ALWAYS
                            : WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
                    window.setAttributes(attributes);
                }

                content.setFitsSystemWindows(false);
                content.setBackgroundColor(Color.BLACK);
                content.setOnApplyWindowInsetsListener((view, insets) -> {
                    applyCutoutPadding(view, insets);
                    // Preserve IME and other insets for Unity/native dialogs.
                    return insets;
                });

                if (Build.VERSION.SDK_INT >= 30) {
                    window.setDecorFitsSystemWindows(false);
                    WindowInsetsController controller = window.getInsetsController();
                    if (controller != null) {
                        controller.setSystemBarsBehavior(
                                WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
                        controller.hide(WindowInsets.Type.statusBars()
                                | WindowInsets.Type.navigationBars());
                    }
                } else {
                    window.getDecorView().setSystemUiVisibility(
                            View.SYSTEM_UI_FLAG_FULLSCREEN
                            | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                            | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                            | View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                            | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                            | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
                }

                applyCutoutPadding(content, content.getRootWindowInsets());
                content.requestApplyInsets();
            } catch (RuntimeException exception) {
                Log.e("UniteDisplay", "Could not apply immersive display settings.", exception);
            }
        });
    }

    private static void applyCutoutPadding(View view, WindowInsets insets) {
        // Insets can be unavailable before the view's first layout. Do not clear
        // previously valid padding during an activity/rotation transition.
        if (insets == null) {
            return;
        }

        DisplayCutout cutout = Build.VERSION.SDK_INT >= 28 ? insets.getDisplayCutout() : null;
        int left = cutout == null ? 0 : cutout.getSafeInsetLeft();
        int top = cutout == null ? 0 : cutout.getSafeInsetTop();
        int right = cutout == null ? 0 : cutout.getSafeInsetRight();
        int bottom = cutout == null ? 0 : cutout.getSafeInsetBottom();

        if (view.getPaddingLeft() != left || view.getPaddingTop() != top
                || view.getPaddingRight() != right || view.getPaddingBottom() != bottom) {
            // Resize the actual Unity surface, not just its cameras or UI. This also
            // keeps touch coordinates and the rendering aspect ratio consistent.
            view.setPadding(left, top, right, bottom);
        }
    }
}
