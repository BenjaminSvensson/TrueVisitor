using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class BeartrapTrap : MonoBehaviour, IInteractable
{
    private static readonly List<BeartrapTrap> activeTraps = new List<BeartrapTrap>();

    [SerializeField] private Transform openVisual;
    [SerializeField] private Transform closedVisual;
    [SerializeField] private string openVisualName = "BearTrapOpen";
    [SerializeField] private string closedVisualName = "BearTrapClosed";
    [SerializeField] private Vector3 triggerCenter = new Vector3(0f, 0.18f, 0f);
    [SerializeField] private Vector3 triggerSize = new Vector3(1.2f, 0.35f, 1.2f);
    [SerializeField] private float trapDuration = 3f;
    [SerializeField] private string pickupItemId = "beartrap";
    [SerializeField] private string pickupDisplayName = "Beartrap";
    [SerializeField] private int pickupQuantity = 1;
    [SerializeField] private int maxTriggerUses = 2;
    [SerializeField] private string pickupLayerName = "Interact";
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip snapClip;
    [SerializeField, Range(0f, 1f)] private float snapVolume = 1f;
    [SerializeField] private Vector2 snapPitchRange = Vector2.one;
    [SerializeField] private UnityEvent onTriggered;

    private static AudioClip generatedSnapClip;
    private BoxCollider triggerCollider;
    private bool triggered;
    private int triggerUseCount;
    private int defaultLayer;
    private int pickupLayer = -1;

    public static IReadOnlyList<BeartrapTrap> ActiveTraps => activeTraps;
    public bool IsTriggered => triggered;

    private void Awake()
    {
        defaultLayer = gameObject.layer;
        ResolvePickupLayer();
        ResolveVisuals();
        EnsureTriggerCollider();
        EnsureAudioSource();
        InitializeOpen();
    }

    private void OnEnable()
    {
        if (!activeTraps.Contains(this))
        {
            activeTraps.Add(this);
        }
    }

    private void OnDisable()
    {
        activeTraps.Remove(this);
    }

    public void InitializeOpen()
    {
        triggered = false;
        SetVisualState(true);
        SetPickupInteractable(false);

        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
    }

    public void PlayHitSound()
    {
        PlaySnapSound();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            Trigger(player);
            return;
        }

        VisitorAI visitor = other.GetComponentInParent<VisitorAI>();
        if (visitor != null)
        {
            Trigger(visitor);
        }
    }

    public bool TryTrigger(VisitorAI visitor)
    {
        if (triggered || visitor == null)
        {
            return false;
        }

        Trigger(visitor);
        return true;
    }

    private void Trigger(PlayerController player)
    {
        triggered = true;
        triggerUseCount++;
        SetVisualState(false);

        player.LockMovementForSeconds(trapDuration);
        PlaySnapSound();
        onTriggered?.Invoke();
        FinishTriggeredUse();
    }

    private void Trigger(VisitorAI visitor)
    {
        triggered = true;
        triggerUseCount++;
        SetVisualState(false);

        visitor.TrapForSeconds(trapDuration, transform.position);
        PlaySnapSound();
        onTriggered?.Invoke();
        FinishTriggeredUse();
    }

    private void FinishTriggeredUse()
    {
        if (triggerUseCount >= maxTriggerUses)
        {
            SetPickupInteractable(false);
            Destroy(gameObject, trapDuration);
            return;
        }

        SetPickupInteractable(true);
    }

    public void Interact()
    {
        if (!triggered)
        {
            return;
        }

        PlayerInventory inventory = FindAnyObjectByType<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning($"{nameof(BeartrapTrap)} could not find a {nameof(PlayerInventory)} for pickup.", this);
            return;
        }

        inventory.AddItem(pickupItemId, pickupDisplayName, pickupQuantity);
        Destroy(gameObject);
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

    private void SetPickupInteractable(bool canPickup)
    {
        int targetLayer = canPickup && pickupLayer >= 0 ? pickupLayer : defaultLayer;
        SetLayerRecursively(transform, targetLayer);

        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
            triggerCollider.isTrigger = true;
        }
    }

    private void SetLayerRecursively(Transform target, int layer)
    {
        target.gameObject.layer = layer;
        for (int i = 0; i < target.childCount; i++)
        {
            SetLayerRecursively(target.GetChild(i), layer);
        }
    }

    private void ResolvePickupLayer()
    {
        pickupLayer = LayerMask.NameToLayer(pickupLayerName);
        if (pickupLayer < 0)
        {
            Debug.LogWarning($"{nameof(BeartrapTrap)} could not find a pickup layer named '{pickupLayerName}'.", this);
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

    private void PlaySnapSound()
    {
        if (audioSource == null)
        {
            return;
        }

        AudioClip clip = snapClip != null ? snapClip : GetGeneratedSnapClip();
        audioSource.pitch = Random.Range(snapPitchRange.x, snapPitchRange.y);
        audioSource.PlayOneShot(clip, snapVolume);
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
        pickupQuantity = Mathf.Max(1, pickupQuantity);
        maxTriggerUses = Mathf.Max(1, maxTriggerUses);
        snapPitchRange.x = Mathf.Max(0.01f, snapPitchRange.x);
        snapPitchRange.y = Mathf.Max(snapPitchRange.x, snapPitchRange.y);

        if (string.IsNullOrWhiteSpace(openVisualName))
        {
            openVisualName = "BearTrapOpen";
        }

        if (string.IsNullOrWhiteSpace(closedVisualName))
        {
            closedVisualName = "BearTrapClosed";
        }

        if (string.IsNullOrWhiteSpace(pickupItemId))
        {
            pickupItemId = "beartrap";
        }

        if (string.IsNullOrWhiteSpace(pickupDisplayName))
        {
            pickupDisplayName = pickupItemId;
        }

        if (string.IsNullOrWhiteSpace(pickupLayerName))
        {
            pickupLayerName = "Interact";
        }
    }
}
