using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

public class MobileInputManager : MonoBehaviour
{
    private void Awake()
    {
#if UNITY_ANDROID
        EnhancedTouchSupport.Enable();
#else
        Destroy(gameObject);
#endif
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }
}
