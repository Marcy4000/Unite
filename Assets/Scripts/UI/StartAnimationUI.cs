using UnityEngine;

public class StartAnimationUI : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private void Start()
    {
        GameManager.Instance.onGameStateChanged += HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Starting)
        {
            StartAnimation();
        }else if (state == GameState.Playing)
        {
            animator.Play("Idle");
        }
    }

    public void StartAnimation()
    {
        animator.SetTrigger("PlayIntro");
    }
}
