using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InternalMonologueTrigger : MonoBehaviour
{
    [TextArea(2, 6)]
    [SerializeField] private string monologueText = "I should keep moving.";
    [SerializeField] private float holdDuration = 3f;
    [SerializeField] private bool hideWhenPlayerExits = false;
    [SerializeField] private bool playOnlyOnce = true;

    private bool hasPlayed;
    private InternalMonologueDisplay display;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        TryPlay();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!hideWhenPlayerExits || !other.CompareTag("Player"))
        {
            return;
        }

        GetDisplay().Hide();
    }

    public bool TryPlay()
    {
        if (playOnlyOnce && hasPlayed)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(monologueText))
        {
            return false;
        }

        hasPlayed = true;
        GetDisplay().Show(monologueText, holdDuration);
        return true;
    }

    private InternalMonologueDisplay GetDisplay()
    {
        if (display == null)
        {
            display = InternalMonologueDisplay.FindOrCreate();
        }

        return display;
    }

    private void OnValidate()
    {
        holdDuration = Mathf.Max(0f, holdDuration);
    }
}
