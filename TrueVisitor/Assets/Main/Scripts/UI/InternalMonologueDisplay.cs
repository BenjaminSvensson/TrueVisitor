using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InternalMonologueDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI monologueText;
    [SerializeField] private float fadeDuration = 0.65f;
    [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, -260f);
    [SerializeField] private Vector2 size = new Vector2(980f, 180f);
    [SerializeField] private float fontSize = 34f;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Coroutine monologueRoutine;

    private void Awake()
    {
        EnsureReferences();
        SetAlpha(0f);
    }

    public static InternalMonologueDisplay FindOrCreate()
    {
        InternalMonologueDisplay existing = FindAnyObjectByType<InternalMonologueDisplay>(FindObjectsInactive.Include);
        if (existing != null)
        {
            return existing;
        }

        GameObject canvasObject = new GameObject("InternalMonologue", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas newCanvas = canvasObject.GetComponent<Canvas>();
        newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject textObject = new GameObject("Internal Monologue Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);

        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = new Vector2(0.5f, 0.5f);
        textTransform.anchorMax = new Vector2(0.5f, 0.5f);
        textTransform.anchoredPosition = new Vector2(0f, -260f);
        textTransform.sizeDelta = new Vector2(980f, 180f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 34f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        InternalMonologueDisplay display = canvasObject.AddComponent<InternalMonologueDisplay>();
        display.canvas = newCanvas;
        display.canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        display.monologueText = text;
        display.EnsureReferences();
        display.SetAlpha(0f);
        return display;
    }

    public void Show(string message, float holdDuration)
    {
        EnsureReferences();
        if (monologueText == null)
        {
            return;
        }

        if (monologueRoutine != null)
        {
            StopCoroutine(monologueRoutine);
        }

        monologueRoutine = StartCoroutine(ShowRoutine(message, holdDuration));
    }

    public void Hide()
    {
        EnsureReferences();
        if (monologueRoutine != null)
        {
            StopCoroutine(monologueRoutine);
        }

        monologueRoutine = StartCoroutine(HideRoutine());
    }

    private IEnumerator ShowRoutine(string message, float holdDuration)
    {
        monologueText.text = message;
        yield return FadeTo(1f);

        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
            yield return FadeTo(0f);
            monologueRoutine = null;
        }
    }

    private IEnumerator HideRoutine()
    {
        yield return FadeTo(0f);
        monologueRoutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (fadeDuration <= 0f)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float startAlpha = GetAlpha();
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void EnsureReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (monologueText == null)
        {
            monologueText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        ApplyLayoutSettings();
    }

    private void ApplyLayoutSettings()
    {
        if (monologueText == null)
        {
            return;
        }

        RectTransform textTransform = monologueText.rectTransform;
        textTransform.anchorMin = new Vector2(0.5f, 0.5f);
        textTransform.anchorMax = new Vector2(0.5f, 0.5f);
        textTransform.anchoredPosition = anchoredPosition;
        textTransform.sizeDelta = size;

        monologueText.fontSize = fontSize;
        monologueText.alignment = TextAlignmentOptions.Center;
        monologueText.textWrappingMode = TextWrappingModes.Normal;
        monologueText.raycastTarget = false;
    }

    private float GetAlpha()
    {
        if (canvasGroup != null)
        {
            return canvasGroup.alpha;
        }

        return monologueText != null ? monologueText.color.a : 0f;
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
            return;
        }

        if (monologueText == null)
        {
            return;
        }

        Color color = monologueText.color;
        color.a = alpha;
        monologueText.color = color;
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);
        size.x = Mathf.Max(1f, size.x);
        size.y = Mathf.Max(1f, size.y);
        fontSize = Mathf.Max(1f, fontSize);
        EnsureReferences();
    }
}
