using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class InteractionCursor : MonoBehaviour
{
    [SerializeField] private float interactScale = 1.35f;
    [SerializeField] private float scaleSpeed = 16f;

    private RectTransform rectTransform;
    private Vector3 baseScale;
    private bool canInteract;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        baseScale = rectTransform.localScale;
    }

    private void Update()
    {
        Vector3 targetScale = baseScale * (canInteract ? interactScale : 1f);
        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            targetScale,
            1f - Mathf.Exp(-scaleSpeed * Time.deltaTime)
        );
    }

    private void OnDisable()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale = baseScale;
        }
    }

    public void SetCanInteract(bool value)
    {
        canInteract = value;
    }

    public static InteractionCursor FindOrAttachToCenteredCursor()
    {
        InteractionCursor existingCursor = FindAnyObjectByType<InteractionCursor>();
        if (existingCursor != null)
        {
            return existingCursor;
        }

        Image bestImage = null;
        float bestScore = float.MaxValue;
        Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            RectTransform rect = image.rectTransform;
            if (rect == null || rect.sizeDelta.x > 64f || rect.sizeDelta.y > 64f)
            {
                continue;
            }

            float anchorDistance =
                Mathf.Abs(rect.anchorMin.x - 0.5f) +
                Mathf.Abs(rect.anchorMin.y - 0.5f) +
                Mathf.Abs(rect.anchorMax.x - 0.5f) +
                Mathf.Abs(rect.anchorMax.y - 0.5f);

            if (anchorDistance > 0.01f)
            {
                continue;
            }

            float score = rect.anchoredPosition.sqrMagnitude + rect.sizeDelta.sqrMagnitude * 0.001f;
            if (score < bestScore)
            {
                bestScore = score;
                bestImage = image;
            }
        }

        return bestImage != null ? bestImage.gameObject.AddComponent<InteractionCursor>() : null;
    }
}
