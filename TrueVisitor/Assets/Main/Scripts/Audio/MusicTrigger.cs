using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MusicTrigger : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private bool playOnce = true;
    [SerializeField] private bool stopOnExit;
    [SerializeField] private bool loop = true;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    private Collider triggerCollider;
    private Coroutine fadeRoutine;
    private bool hasPlayed;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
        EnsureAudioSource();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (playOnce && hasPlayed)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        PlayMusic();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!stopOnExit || other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        StopMusic();
    }

    public void PlayMusic()
    {
        EnsureAudioSource();
        if (audioSource == null || musicClip == null)
        {
            return;
        }

        hasPlayed = true;
        audioSource.clip = musicClip;
        audioSource.loop = loop;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        audioSource.volume = fadeInDuration <= 0f ? volume : 0f;
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }

        if (fadeInDuration > 0f)
        {
            fadeRoutine = StartCoroutine(FadeVolume(volume, fadeInDuration, false));
        }
    }

    public void StopMusic()
    {
        if (audioSource == null)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        if (fadeOutDuration > 0f)
        {
            fadeRoutine = StartCoroutine(FadeVolume(0f, fadeOutDuration, true));
        }
        else
        {
            audioSource.Stop();
            audioSource.volume = 0f;
        }
    }

    private IEnumerator FadeVolume(float targetVolume, float duration, bool stopWhenDone)
    {
        float startVolume = audioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }

        audioSource.volume = targetVolume;
        if (stopWhenDone)
        {
            audioSource.Stop();
        }

        fadeRoutine = null;
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
        audioSource.spatialBlend = 0f;
        audioSource.volume = Mathf.Clamp01(volume);
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);

        Collider colliderComponent = GetComponent<Collider>();
        if (colliderComponent != null)
        {
            colliderComponent.isTrigger = true;
        }
    }
}
