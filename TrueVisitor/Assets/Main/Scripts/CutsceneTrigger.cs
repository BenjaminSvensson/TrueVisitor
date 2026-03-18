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

    bool hasPlayed = false;

    void Start()
    {
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
        cutsceneAnimator.Play(animationName);

        yield return null;

        float length =
            cutsceneAnimator.GetCurrentAnimatorStateInfo(0).length;

        yield return new WaitForSeconds(length);

        // Fade in before return
        if (useFade)
            yield return StartCoroutine(Fade(1));

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
}