using UnityEngine;

public class BreakableWindowInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject intactVisual;
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private Collider windowCollider;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip breakClip;
    [SerializeField, Range(0f, 1f)] private float breakVolume = 1f;
    [SerializeField] private float breakSoundMinDistance = 4f;
    [SerializeField] private float breakSoundMaxDistance = 35f;
    [SerializeField] private float alertDuration = 8f;
    [SerializeField] private bool canBreakOnlyOnce = true;
    [SerializeField] private bool disableColliderAfterBreak = true;

    private bool broken;

    public bool IsBroken => broken;

    private void Awake()
    {
        if (windowCollider == null)
        {
            windowCollider = GetComponent<Collider>();
        }

        EnsureAudioSource();
        ApplyVisualState();
    }

    public void Interact()
    {
        if (broken && canBreakOnlyOnce)
        {
            return;
        }

        BreakWindow();
    }

    public void BreakWindow()
    {
        if (broken && canBreakOnlyOnce)
        {
            return;
        }

        broken = true;
        ApplyVisualState();
        PlayBreakSound();
        AlertVisitors();

        if (disableColliderAfterBreak && windowCollider != null)
        {
            windowCollider.enabled = false;
        }
    }

    private void ApplyVisualState()
    {
        if (intactVisual != null)
        {
            intactVisual.SetActive(!broken);
        }

        if (brokenVisual != null)
        {
            brokenVisual.SetActive(broken);
        }
    }

    private void PlayBreakSound()
    {
        if (breakClip == null)
        {
            return;
        }

        GameObject soundObject = new GameObject("Window Break Sound");
        soundObject.transform.position = transform.position;

        AudioSource oneShotSource = soundObject.AddComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
        oneShotSource.spatialBlend = 1f;
        oneShotSource.rolloffMode = AudioRolloffMode.Linear;
        oneShotSource.minDistance = breakSoundMinDistance;
        oneShotSource.maxDistance = breakSoundMaxDistance;
        oneShotSource.volume = breakVolume;
        oneShotSource.PlayOneShot(breakClip, breakVolume);

        Destroy(soundObject, breakClip.length + 0.25f);
    }

    private void AlertVisitors()
    {
        VisitorAI[] visitors = FindObjectsByType<VisitorAI>(FindObjectsSortMode.None);
        for (int i = 0; i < visitors.Length; i++)
        {
            if (visitors[i] != null)
            {
                visitors[i].AlertToNoise(transform.position, alertDuration);
            }
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
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = breakSoundMinDistance;
        audioSource.maxDistance = breakSoundMaxDistance;
    }

    private void OnValidate()
    {
        alertDuration = Mathf.Max(0f, alertDuration);
        breakSoundMinDistance = Mathf.Max(0f, breakSoundMinDistance);
        breakSoundMaxDistance = Mathf.Max(breakSoundMinDistance, breakSoundMaxDistance);
    }
}
