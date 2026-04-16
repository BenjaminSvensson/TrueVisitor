using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class CutsceneEvents : MonoBehaviour
{
    [SerializeField] private GameObject diaryUI;
    [SerializeField] private GameObject playerCharacter;
    [SerializeField] private Vector3 playerUpstairsPosition;
    [SerializeField] private Quaternion playerUpstairsRotation;

    private void Start()
    {
        if (diaryUI != null)
            diaryUI.SetActive(false);
    }
    public void OpenDiary()
    {
        diaryUI.SetActive(true);
    }
    public void CloseDiary()
    {
        diaryUI.SetActive(false);
    }

    public void MovePlayerUpstairs()
    {
        if (playerCharacter != null)
        {
            playerCharacter.transform.position = playerUpstairsPosition;
            playerCharacter.transform.rotation = playerUpstairsRotation;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame && diaryUI.activeSelf)
        {
            CloseDiary();
        }
    }
}
