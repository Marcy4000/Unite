# UIObject3D stops rendering after Play Mode restarts

The project disables domain reload (`m_EnterPlayModeOptions: 1`). Previously,
`UIObject3DTimer` set its static `IsQuitting` flag when leaving Play Mode, but
never reset it. The next session destroyed the old timer without resetting the
flag. Once the timer was gone, all subsequent delayed calls were silently
discarded: timer creation was blocked by `IsQuitting`.

Every UIObject3D relies on this shared timer to complete startup and rebuilds.
With startup blocked, `started` remained false and the preview image could stay
transparent indefinitely. All previews were affected regardless of model.
Recompiling scripts or restarting Unity could temporarily hide the issue by
resetting static state.

The timer now resets its quitting flag, cached component, and pending edit-mode
callbacks at `SubsystemRegistration`, before scene initialization, even without
domain reload. Its named quit handler is unregistered before registering it,
preventing duplicate subscriptions over repeated sessions. Actual application
shutdown still prevents creation of replacement timer objects.

## Validation

- Run Play Mode tests filtered to `UIObject3DTimerPlayModeTests`. These simulate
  retained shutdown state over three sessions and replacement of a destroyed
  timer, and require the actual delayed startup callbacks to execute.
- With Reload Domain disabled, open a UI containing previews, enter Play Mode,
  stop, and enter again several times without editing scripts. Check different
  preview screens and model changes, not only the initial screen.
- This identifies an editor-session failure path. If the issue also occurs in a
  standalone player without restarting the application, record that separately;
  the retained Play Mode shutdown flag does not explain that case.

Automated validation: all four `UIObject3DTimerPlayModeTests` and all eight
existing `TrainerPreviewRenderingTests` passed in Unity 6000.3.24f1 through Unity
MCP. An isolated lifecycle harness also reproduced dropped startup callbacks
with the original timer and passed with the fix.
