using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

public class MobileInputManager : MonoBehaviour
{
    private void Awake()
    {
        if (Application.isEditor)
        {
            return;
        }

        if (Application.platform != RuntimePlatform.Android)
        {
            Destroy(gameObject);
            return;
        }

        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }
}
