using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractionTipDisplay : MonoBehaviour
{
    private static readonly HashSet<string> shownInteractableTypes = new HashSet<string>();

    [SerializeField] private TextMeshProUGUI tipText;
    [SerializeField] private string message = "F - Interact";
    [SerializeField] private float showDuration = 2.5f;
    [SerializeField] private float fadeDuration = 0.25f;

    private Canvas canvas;
    private Coroutine showRoutine;

    private void Awake()
    {
        EnsureReferences();
        SetAlpha(0f);
    }

    public static InteractionTipDisplay FindOrCreate()
    {
        InteractionTipDisplay existing = FindAnyObjectByType<InteractionTipDisplay>(FindObjectsInactive.Include);
        if (existing != null)
        {
            return existing;
        }

        GameObject canvasObject = new GameObject("InteractionTip", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas newCanvas = canvasObject.GetComponent<Canvas>();
        newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject textObject = new GameObject("Interaction Tip Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);

        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = new Vector2(0.5f, 0.5f);
        textTransform.anchorMax = new Vector2(0.5f, 0.5f);
        textTransform.anchoredPosition = new Vector2(0f, -90f);
        textTransform.sizeDelta = new Vector2(420f, 60f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 30f;
        text.raycastTarget = false;

        InteractionTipDisplay display = canvasObject.AddComponent<InteractionTipDisplay>();
        display.tipText = text;
        display.EnsureReferences();
        display.SetAlpha(0f);
        return display;
    }

    public void ShowFirstTimeFor(IInteractable interactable)
    {
        if (interactable == null)
        {
            return;
        }

        string typeKey = interactable.GetType().FullName;
        if (shownInteractableTypes.Contains(typeKey))
        {
            return;
        }

        shownInteractableTypes.Add(typeKey);
        Show();
    }

    public void Show()
    {
        EnsureReferences();
        if (tipText == null)
        {
            return;
        }

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }

        showRoutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        tipText.text = message;
        yield return FadeTo(1f);
        yield return new WaitForSeconds(showDuration);
        yield return FadeTo(0f);
        showRoutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (fadeDuration <= 0f)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float startAlpha = tipText.color.a;
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

        if (tipText == null)
        {
            tipText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void SetAlpha(float alpha)
    {
        if (tipText == null)
        {
            return;
        }

        Color color = tipText.color;
        color.a = alpha;
        tipText.color = color;
    }

    private void OnValidate()
    {
        showDuration = Mathf.Max(0f, showDuration);
        fadeDuration = Mathf.Max(0f, fadeDuration);
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "F - Interact";
        }
    }
}
