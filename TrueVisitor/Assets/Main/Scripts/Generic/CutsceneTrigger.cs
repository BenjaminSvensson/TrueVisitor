using UnityEngine;
using System.Collections;
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
        if (playOnlyOnce && hasPlayed) return;

        if (other.CompareTag("Player"))
        {
            StartCoroutine(PlayCutscene());
        }
    }

    IEnumerator PlayCutscene()
    {
        hasPlayed = true;

        // Fade in
        if (useFade)
            yield return StartCoroutine(Fade(1));

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
            yield return StartCoroutine(Fade(0));

        // Play animation
        cutsceneAnimator.Play(animationName, 0, 0f);
        cutsceneAnimator.Update(0f);

        yield return new WaitForSeconds(GetCutsceneLength());

        // Fade in before return
        if (useFade)
            yield return StartCoroutine(Fade(1));

        if (teleportPlayerToCutsceneEnd)
        {
            SampleCutsceneEndPose();
            TeleportPlayerToCutsceneEnd();
        }

        // Back to player
        cutsceneCamera.gameObject.SetActive(false);
        playerCamera.gameObject.SetActive(true);

        // Fade out to game
        if (useFade)
            yield return StartCoroutine(Fade(0));
    }

    IEnumerator Fade(float target)
    {
        if (fadeImage == null) yield break;

        Color c = fadeImage.color;

        while (!Mathf.Approximately(c.a, target))
        {
            c.a = Mathf.MoveTowards(
                c.a,
                target,
                fadeSpeed * Time.deltaTime
            );

            fadeImage.color = c;

            yield return null;
        }
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
        return stateInfo.length;
    }
}
