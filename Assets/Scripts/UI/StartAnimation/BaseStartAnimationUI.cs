using System;
using System.Collections;
using UnityEngine;

public abstract class BaseStartAnimationUI : MonoBehaviour
{
    public event Action OnAnimationComplete;

    public event Action OnAnimationReady;

    private bool isSubscribed = false;

    protected virtual void Start()
    {
        SubscribeToGameManager();
    }

    protected virtual void OnEnable()
    {
        SubscribeToGameManager();
    }

    protected virtual void OnDisable()
    {
        UnsubscribeFromGameManager();
    }

    protected virtual void OnDestroy()
    {
        UnsubscribeFromGameManager();
    }

    private void SubscribeToGameManager()
    {
        if (isSubscribed || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.onGameStateChanged += HandleGameStateChanged;
        isSubscribed = true;
    }

    private void UnsubscribeFromGameManager()
    {
        if (!isSubscribed || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.onGameStateChanged -= HandleGameStateChanged;
        isSubscribed = false;
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Starting)
        {
            StartAnimation();
        }
        else if (state == GameState.Playing)
        {
            ResetAnimation();
        }
    }

    public void StartAnimation()
    {
        StartCoroutine(AnimationSequence());
    }

    private IEnumerator AnimationSequence()
    {
        OnAnimationReady?.Invoke();

        yield return StartCoroutine(DoStartAnimation());

        OnAnimationComplete?.Invoke();
    }

    protected abstract IEnumerator DoStartAnimation();

    protected abstract void ResetAnimation();
}
