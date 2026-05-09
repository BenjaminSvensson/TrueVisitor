using UnityEngine;
using UnityEngine.Events;

public class DoorInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform doorPivot;
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private float openSpeed = 8f;
    [SerializeField] private bool startsOpen = false;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private float visitorAlertDistance = 9f;
    [SerializeField] private float visitorAlertDuration = 4f;
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
        EnsureAudioSource();
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
        SetOpen(!isOpen, null);
    }

    public void Open()
    {
        SetOpen(true, null);
    }

    public void Open(VisitorAI opener)
    {
        SetOpen(true, opener);
    }

    public void Close()
    {
        SetOpen(false, null);
    }

    private void SetOpen(bool open, VisitorAI alertVisitorToIgnore)
    {
        if (isOpen == open)
        {
            return;
        }

        isOpen = open;

        if (isOpen)
        {
            PlaySound(openClip);
            AlertVisitors(alertVisitorToIgnore);
            onOpened?.Invoke();
        }
        else
        {
            PlaySound(closeClip);
            AlertVisitors(alertVisitorToIgnore);
            onClosed?.Invoke();
        }
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.maxDistance = 12f;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip);
    }

    private void AlertVisitors(VisitorAI visitorToIgnore)
    {
        if (visitorAlertDistance <= 0f || visitorAlertDuration <= 0f)
        {
            return;
        }

        VisitorAI[] visitors = FindObjectsByType<VisitorAI>(FindObjectsSortMode.None);
        Vector3 soundPosition = transform.position;
        Vector2 soundFlat = new Vector2(soundPosition.x, soundPosition.z);
        for (int i = 0; i < visitors.Length; i++)
        {
            VisitorAI visitor = visitors[i];
            if (visitor == null)
            {
                continue;
            }

            if (visitor == visitorToIgnore)
            {
                continue;
            }

            Vector3 visitorPosition = visitor.transform.position;
            Vector2 visitorFlat = new Vector2(visitorPosition.x, visitorPosition.z);
            if (Vector2.Distance(visitorFlat, soundFlat) <= visitorAlertDistance)
            {
                visitor.AlertToNoise(soundPosition, visitorAlertDuration);
            }
        }
    }

    private void OnValidate()
    {
        openSpeed = Mathf.Max(0f, openSpeed);
        visitorAlertDistance = Mathf.Max(0f, visitorAlertDistance);
        visitorAlertDuration = Mathf.Max(0f, visitorAlertDuration);
    }
}
