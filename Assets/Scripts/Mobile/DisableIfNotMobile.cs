using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableIfNotMobile : MonoBehaviour
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
        }
    }
}
