using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;

public class MoveLearnPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelBg, moveLearnPrefab, arrowIcon;
    private bool isLearningMove;
    private PlayerControls controls;
    private float timer;
    private MoveAsset[] moves;
    private Queue<MoveAsset[]> moveQueue = new Queue<MoveAsset[]>();

    private string[] controlPaths = new string[]
    {
        "<Gamepad>/dpad/left",
        "<Gamepad>/dpad/right",
        "<Gamepad>/dpad/up"
    };

    public bool IsLearningMove => isLearningMove;
    
    public static event Action<MoveAsset> onSelectedMove;

    private void Start()
    {
        CloseLearnMovePanel();
        controls = new PlayerControls();
        controls.asset.Enable();
    }

    public void EnqueueNewMove(MoveAsset[] moves)
    {
        moveQueue.Enqueue(moves);
    }

    public void ShowLearnMovePanel(MoveAsset[] moves)
    {
        panelBg.SetActive(true);
        arrowIcon.SetActive(true);
        this.moves = moves;
        foreach (MoveAsset move in moves)
        {
            var moveUIObject = Instantiate(moveLearnPrefab, panelBg.transform);
            moveUIObject.GetComponent<MoveUI>().Initialize(move);
            var screenControl = moveUIObject.GetComponent<OnScreenButton>();
#if UNITY_ANDROID
            screenControl.controlPath = controlPaths[moveUIObject.transform.GetSiblingIndex()];
#else
            screenControl.enabled = false;
#endif

        }
        isLearningMove = true;
        timer = 10f;
    }

    public void CloseLearnMovePanel()
    {
        foreach (Transform child in panelBg.transform)
        {
            Destroy(child.gameObject);
        }
        panelBg.SetActive(false);
        arrowIcon.SetActive(false);
        isLearningMove = false;
        moves = null;
    }

    private void Update()
    {
        if (isLearningMove)
        {
            if (controls.LearnMove.Move1.WasPerformedThisFrame())
            {
                TrySelectingMove(0);
            }

            if (controls.LearnMove.Move2.WasPerformedThisFrame())
            {
                TrySelectingMove(1);
            }

            if (controls.LearnMove.Move3.WasPerformedThisFrame())
            {
                TrySelectingMove(2);
            }

            timer -= Time.deltaTime;

            if (timer <= 0)
            {
                TrySelectingMove(0);
            }
        }
        else
        {
            if (moveQueue.Count > 0)
            {
                ShowLearnMovePanel(moveQueue.Dequeue());
            }
        }
    }

    private void TrySelectingMove(int id)
    {
        if (id > moves.Length-1)
        {
            return;
        }
        onSelectedMove?.Invoke(moves[id]);
        CloseLearnMovePanel();
    }
}
