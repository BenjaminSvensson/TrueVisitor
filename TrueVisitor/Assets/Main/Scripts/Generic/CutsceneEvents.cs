using UnityEngine;
using UnityEngine.InputSystem;

public class CutsceneEvents : MonoBehaviour
{
    [SerializeField] private GameObject diaryUI;
    [SerializeField] private GameObject playerCharacter;
    [SerializeField] private Vector3 playerUpstairsPosition;
    [SerializeField] private Quaternion playerUpstairsRotation;
    [SerializeField] private Animator cutsceneAnimator;

    private void Start()
    {
        if (diaryUI != null)
            diaryUI.SetActive(false);
    }
    public void OpenDiary()
    {
        if (diaryUI == null)
            return;

        diaryUI.SetActive(true);
    }

    public void CloseDiary()
    {
        if (diaryUI == null)
            return;

        diaryUI.SetActive(false);
    }

    public void MovePlayerUpstairs()
    {
        // Kept for old animation events. CutsceneTriggerAdvanced now handles seamless teleporting.
    }

    private void Update()
    {
        if (diaryUI == null)
            return;

        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame && diaryUI.activeSelf)
        {
            CloseDiary();
        }
    }
}
