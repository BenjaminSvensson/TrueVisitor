using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CutsceneTriggerAdvanced : MonoBehaviour
{
    [Header("Cameras")]
    public Camera playerCamera;
    public Camera cutsceneCamera;

    [Header("Animation")]
    public Animator cutsceneAnimator;
    public string animationName;

    [Header("Sound (optional)")]
    public bool useSound = false;
    public AudioSource audioSource;
    public AudioClip soundClip;

    [Header("Fade (optional)")]
    public bool useFade = false;
    public Image fadeImage;
    public float fadeSpeed = 2f;

    [Header("Settings")]
    public bool playOnTrigger = true;
    public bool playOnlyOnce = true;

    [Header("Seamless Return")]
    public bool teleportPlayerToCutsceneEnd = false;

    bool hasPlayed = false;
    bool isPlaying = false;
    bool skipRequested = false;
    PlayerController playerController;

    void Start()
    {
        CachePlayerController();

        if (cutsceneCamera != null)
            cutsceneCamera.gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!playOnTrigger) return;
        if (isPlaying) return;
        if (playOnlyOnce && hasPlayed) return;

        if (other.CompareTag("Player"))
        {
            StartCoroutine(PlayCutscene());
        }
    }

    void OnDisable()
    {
        isPlaying = false;
        skipRequested = false;
    }

    IEnumerator PlayCutscene()
    {
        if (!CanPlayCutscene())
            yield break;

        isPlaying = true;
        skipRequested = false;
        hasPlayed = true;

        // Fade in
        if (useFade)
            yield return StartCoroutine(Fade(1, true));

        if (skipRequested)
        {
            yield return StartCoroutine(FinishCutscene(true));
            yield break;
        }

        // Switch camera
        playerCamera.gameObject.SetActive(false);
        cutsceneCamera.gameObject.SetActive(true);

        // Play sound
        if (useSound && audioSource && soundClip)
        {
            audioSource.clip = soundClip;
            audioSource.Play();
        }

        // Fade out
        if (useFade)
            yield return StartCoroutine(Fade(0, true));

        if (skipRequested)
        {
            yield return StartCoroutine(FinishCutscene(true));
            yield break;
        }

        // Play animation
        cutsceneAnimator.Play(animationName, 0, 0f);
        cutsceneAnimator.Update(0f);

        yield return StartCoroutine(WaitForCutsceneOrSkip(GetCutsceneLength()));

        yield return StartCoroutine(FinishCutscene(skipRequested));
    }

    IEnumerator FinishCutscene(bool skipped)
    {
        if (skipped && useSound && audioSource != null)
        {
            audioSource.Stop();
        }

        // Fade in before return
        if (useFade && !skipped)
            yield return StartCoroutine(Fade(1, false));

        if (teleportPlayerToCutsceneEnd)
        {
            if (cutsceneCamera != null && !cutsceneCamera.gameObject.activeSelf)
            {
                cutsceneCamera.gameObject.SetActive(true);
            }

            SampleCutsceneEndPose();
            TeleportPlayerToCutsceneEnd();
        }

        // Back to player
        cutsceneCamera.gameObject.SetActive(false);
        playerCamera.gameObject.SetActive(true);

        // Fade out to game
        if (useFade)
            yield return StartCoroutine(Fade(0, false));

        skipRequested = false;
        isPlaying = false;
    }

    IEnumerator WaitForCutsceneOrSkip(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (WasSkipPressedThisFrame())
            {
                skipRequested = true;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator Fade(float target, bool allowSkip)
    {
        if (fadeImage == null) yield break;

        Color c = fadeImage.color;
        if (fadeSpeed <= 0f)
        {
            c.a = target;
            fadeImage.color = c;
            yield break;
        }

        while (!Mathf.Approximately(c.a, target))
        {
            if (allowSkip && WasSkipPressedThisFrame())
            {
                skipRequested = true;
                yield break;
            }

            c.a = Mathf.MoveTowards(
                c.a,
                target,
                fadeSpeed * Time.deltaTime
            );

            fadeImage.color = c;

            yield return null;
        }
    }

    bool WasSkipPressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
    }

    void CachePlayerController()
    {
        if (playerController != null)
            return;

        if (playerCamera != null)
            playerController = playerCamera.GetComponentInParent<PlayerController>();

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();
    }

    bool CanPlayCutscene()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning($"{nameof(CutsceneTriggerAdvanced)} needs a player camera before it can play.", this);
            return false;
        }

        if (cutsceneCamera == null)
        {
            Debug.LogWarning($"{nameof(CutsceneTriggerAdvanced)} needs a cutscene camera before it can play.", this);
            return false;
        }

        if (cutsceneAnimator == null)
        {
            Debug.LogWarning($"{nameof(CutsceneTriggerAdvanced)} needs a cutscene animator before it can play.", this);
            return false;
        }

        if (string.IsNullOrEmpty(animationName))
        {
            Debug.LogWarning($"{nameof(CutsceneTriggerAdvanced)} needs an animation name before it can play.", this);
            return false;
        }

        return true;
    }

    void TeleportPlayerToCutsceneEnd()
    {
        if (cutsceneCamera == null)
            return;

        CachePlayerController();

        if (playerController == null)
        {
            Debug.LogWarning($"{nameof(CutsceneTriggerAdvanced)} could not find a {nameof(PlayerController)} for seamless cutscene teleport.", this);
            return;
        }

        playerController.TeleportToCameraPose(cutsceneCamera.transform);
    }

    void SampleCutsceneEndPose()
    {
        if (cutsceneAnimator == null || string.IsNullOrEmpty(animationName))
            return;

        cutsceneAnimator.Play(animationName, 0, 0.9999f);
        cutsceneAnimator.Update(0f);
    }

    float GetCutsceneLength()
    {
        if (cutsceneAnimator == null || cutsceneAnimator.runtimeAnimatorController == null)
            return 0f;

        AnimationClip[] clips = cutsceneAnimator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name == animationName)
                return clip.length;
        }

        AnimatorStateInfo stateInfo = cutsceneAnimator.GetCurrentAnimatorStateInfo(0);
        if (float.IsNaN(stateInfo.length) || float.IsInfinity(stateInfo.length))
            return 0f;

        return stateInfo.length;
    }
}
