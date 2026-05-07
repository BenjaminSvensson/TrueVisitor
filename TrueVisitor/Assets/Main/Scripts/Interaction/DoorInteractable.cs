using UnityEngine;
using UnityEngine.Events;

public class DoorInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform doorPivot;
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private float openSpeed = 8f;
    [SerializeField] private bool startsOpen = false;
    [SerializeField] private UnityEvent onOpened;
    [SerializeField] private UnityEvent onClosed;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (doorPivot == null)
        {
            doorPivot = transform;
        }

        closedRotation = doorPivot.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        isOpen = startsOpen;
        doorPivot.localRotation = isOpen ? openRotation : closedRotation;
    }

    private void Update()
    {
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        if (openSpeed <= 0f)
        {
            doorPivot.localRotation = targetRotation;
            return;
        }

        doorPivot.localRotation = Quaternion.Slerp(
            doorPivot.localRotation,
            targetRotation,
            1f - Mathf.Exp(-openSpeed * Time.deltaTime)
        );
    }

    public void Interact()
    {
        SetOpen(!isOpen);
    }

    public void Open()
    {
        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        if (isOpen == open)
        {
            return;
        }

        isOpen = open;

        if (isOpen)
        {
            onOpened?.Invoke();
        }
        else
        {
            onClosed?.Invoke();
        }
    }

    private void OnValidate()
    {
        openSpeed = Mathf.Max(0f, openSpeed);
    }
}
