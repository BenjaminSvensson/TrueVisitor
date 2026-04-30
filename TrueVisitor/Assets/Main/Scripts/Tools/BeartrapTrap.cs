using UnityEngine;

public class BeartrapTrap : MonoBehaviour
{
    [SerializeField] private Transform openVisual;
    [SerializeField] private Transform closedVisual;
    [SerializeField] private string openVisualName = "BearTrapOpen";
    [SerializeField] private string closedVisualName = "BearTrapClosed";
    [SerializeField] private Vector3 triggerCenter = new Vector3(0f, 0.18f, 0f);
    [SerializeField] private Vector3 triggerSize = new Vector3(1.2f, 0.35f, 1.2f);
    [SerializeField] private float trapDuration = 3f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip snapClip;

    private static AudioClip generatedSnapClip;
    private BoxCollider triggerCollider;
    private bool triggered;

    private void Awake()
    {
        ResolveVisuals();
        EnsureTriggerCollider();
        EnsureAudioSource();
        InitializeOpen();
    }

    public void InitializeOpen()
    {
        triggered = false;
        SetVisualState(true);

        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        Trigger(player);
    }

    private void Trigger(PlayerController player)
    {
        triggered = true;
        SetVisualState(false);

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        player.LockMovementForSeconds(trapDuration);
        PlaySnapSound();
    }

    private void ResolveVisuals()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (openVisual == null && child.name == openVisualName)
            {
                openVisual = child;
            }

            if (closedVisual == null && child.name == closedVisualName)
            {
                closedVisual = child;
            }
        }
    }

    private void SetVisualState(bool open)
    {
        if (openVisual != null)
        {
            openVisual.gameObject.SetActive(open);
        }

        if (closedVisual != null)
        {
            closedVisual.gameObject.SetActive(!open);
        }
    }

    private void EnsureTriggerCollider()
    {
        triggerCollider = GetComponent<BoxCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }

        triggerCollider.isTrigger = true;
        triggerCollider.center = triggerCenter;
        triggerCollider.size = triggerSize;
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

    private void PlaySnapSound()
    {
        if (audioSource == null)
        {
            return;
        }

        AudioClip clip = snapClip != null ? snapClip : GetGeneratedSnapClip();
        audioSource.PlayOneShot(clip);
    }

    private static AudioClip GetGeneratedSnapClip()
    {
        if (generatedSnapClip != null)
        {
            return generatedSnapClip;
        }

        const int sampleRate = 44100;
        const float length = 0.18f;
        int sampleCount = Mathf.CeilToInt(sampleRate * length);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float decay = Mathf.Exp(-26f * t);
            float lowClick = Mathf.Sin(Mathf.PI * 2f * 95f * t);
            float metalClick = Mathf.Sign(Mathf.Sin(Mathf.PI * 2f * 720f * t));
            samples[i] = Mathf.Clamp((lowClick * 0.45f + metalClick * 0.28f) * decay, -1f, 1f);
        }

        generatedSnapClip = AudioClip.Create("Generated Beartrap Snap", sampleCount, 1, sampleRate, false);
        generatedSnapClip.SetData(samples, 0);
        return generatedSnapClip;
    }

    private void OnValidate()
    {
        triggerSize.x = Mathf.Max(0.1f, triggerSize.x);
        triggerSize.y = Mathf.Max(0.1f, triggerSize.y);
        triggerSize.z = Mathf.Max(0.1f, triggerSize.z);
        trapDuration = Mathf.Max(0f, trapDuration);

        if (string.IsNullOrWhiteSpace(openVisualName))
        {
            openVisualName = "BearTrapOpen";
        }

        if (string.IsNullOrWhiteSpace(closedVisualName))
        {
            closedVisualName = "BearTrapClosed";
        }
    }
}
