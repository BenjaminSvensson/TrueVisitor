using UnityEngine;
using System.Collections;
using TMPro;

public class BedInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private CutsceneTriggerAdvanced cutsceneTrigger;
    [SerializeField] private ShopTriggerVisitTracker requiredShopTrigger;
    [SerializeField] private TextMeshProUGUI interactWarningText;
    [SerializeField] private string sleepBlockedMessage = "Reinforce your house before sleeping";
    [SerializeField] private float warningFadeDuration = 0.35f;
    [SerializeField] private float warningHoldDuration = 1.4f;
    [SerializeField] private bool playCutscene = true;
    [SerializeField] private bool changeWorldToNight = true;
    [SerializeField] private bool onlyInteractOnce = true;
    [SerializeField] private GameObject[] objectsToDisable;
    [SerializeField] private GameObject[] objectsToEnable;
    [SerializeField] private TextMeshProUGUI changeText1;
    [SerializeField] private TextMeshProUGUI changeText2;
    [SerializeField] private SurviveTimerDisplay surviveTimer;
    [SerializeField] private Light[] lightsToChange;
    [SerializeField] private Color nightAmbientColor = new Color(0.025f, 0.035f, 0.08f, 1f);
    [SerializeField] private Color nightLightColor = new Color(0.35f, 0.45f, 0.75f, 1f);
    [SerializeField] private float directionalNightIntensity = 0.05f;
    [SerializeField] private float localLightIntensityMultiplier = 0.35f;

    private bool hasInteracted;
    private Coroutine warningRoutine;

    public void Interact()
    {
        if (!CanSleep())
        {
            ShowSleepBlockedWarning();
            return;
        }

        if (onlyInteractOnce && hasInteracted)
        {
            return;
        }

        hasInteracted = true;

        ApplyObjectStateChanges();

        if (playCutscene)
        {
            PlayCutscene();
        }

        if (changeWorldToNight)
        {
            ApplyNightLighting();
            StartSurviveTimer();
        }
    }

    private bool CanSleep()
    {
        if (requiredShopTrigger != null)
        {
            return requiredShopTrigger.Visited;
        }

        return ShopTriggerVisitTracker.HasVisitedAnyShopTrigger;
    }

    private void ShowSleepBlockedWarning()
    {
        TextMeshProUGUI warningText = GetInteractWarningText();
        if (warningText == null)
        {
            Debug.LogWarning($"{nameof(BedInteractable)} could not find InteractWarning text.", this);
            return;
        }

        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
        }

        warningRoutine = StartCoroutine(FadeWarning(warningText));
    }

    private TextMeshProUGUI GetInteractWarningText()
    {
        if (interactWarningText != null)
        {
            return interactWarningText;
        }

        GameObject warningObject = GameObject.Find("InteractWarning");
        if (warningObject != null)
        {
            interactWarningText = warningObject.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        return interactWarningText;
    }

    private IEnumerator FadeWarning(TextMeshProUGUI warningText)
    {
        warningText.text = sleepBlockedMessage;
        warningText.gameObject.SetActive(true);
        if (warningText.transform.root != null)
        {
            warningText.transform.root.gameObject.SetActive(true);
        }

        yield return FadeWarningAlpha(warningText, 1f);
        yield return new WaitForSeconds(warningHoldDuration);
        yield return FadeWarningAlpha(warningText, 0f);

        warningRoutine = null;
    }

    private IEnumerator FadeWarningAlpha(TextMeshProUGUI warningText, float targetAlpha)
    {
        Color color = warningText.color;
        if (warningFadeDuration <= 0f)
        {
            color.a = targetAlpha;
            warningText.color = color;
            yield break;
        }

        while (!Mathf.Approximately(color.a, targetAlpha))
        {
            color.a = Mathf.MoveTowards(color.a, targetAlpha, Time.deltaTime / warningFadeDuration);
            warningText.color = color;
            yield return null;
        }
    }

    private void PlayCutscene()
    {
        CutsceneTriggerAdvanced trigger = GetCutsceneTrigger();
        if (trigger == null)
        {
            Debug.LogWarning($"{nameof(BedInteractable)} could not find a {nameof(CutsceneTriggerAdvanced)} to play.", this);
            return;
        }

        trigger.TryPlayCutscene();
    }

    private CutsceneTriggerAdvanced GetCutsceneTrigger()
    {
        if (cutsceneTrigger != null)
        {
            return cutsceneTrigger;
        }

        cutsceneTrigger = FindAnyObjectByType<CutsceneTriggerAdvanced>();
        return cutsceneTrigger;
    }

    private void ApplyNightLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = nightAmbientColor;
        RenderSettings.fog = true;
        RenderSettings.fogColor = nightAmbientColor;

        Light[] lights = lightsToChange != null && lightsToChange.Length > 0
            ? lightsToChange
            : FindObjectsByType<Light>(FindObjectsSortMode.None);

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
            {
                continue;
            }

            light.color = nightLightColor;
            if (light.type == LightType.Directional)
            {
                light.intensity = directionalNightIntensity;
            }
            else
            {
                light.intensity *= localLightIntensityMultiplier;
            }
        }
    }

    private void StartSurviveTimer()
    {
        SurviveTimerDisplay timer = GetSurviveTimer();
        if (timer != null)
        {
            timer.StartCountdown();
        }
    }

    private SurviveTimerDisplay GetSurviveTimer()
    {
        if (surviveTimer != null)
        {
            return surviveTimer;
        }

        GameObject timerObject = GameObject.Find("SurviveTime");
        if (timerObject == null)
        {
            timerObject = GameObject.Find("FreakyTimer");
        }

        if (timerObject != null)
        {
            surviveTimer = timerObject.GetComponent<SurviveTimerDisplay>();
        }

        if (surviveTimer == null)
        {
            surviveTimer = FindAnyObjectByType<SurviveTimerDisplay>(FindObjectsInactive.Include);
        }

        return surviveTimer;
    }

    private void ApplyObjectStateChanges()
    {
        SetObjectsActive(objectsToDisable, false);
        SetObjectsActive(objectsToEnable, true);

        if (changeText1 != null)
        {
            changeText1.text = "THE FREAKY VISITOR IS VISITING TONIGHT!";
        }

        if (changeText2 != null)
        {
            changeText2.text = "Heeeelp";
        }
    }

    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
        {
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                objects[i].SetActive(active);
            }
        }
    }

    private void OnValidate()
    {
        directionalNightIntensity = Mathf.Max(0f, directionalNightIntensity);
        localLightIntensityMultiplier = Mathf.Max(0f, localLightIntensityMultiplier);
        warningFadeDuration = Mathf.Max(0f, warningFadeDuration);
        warningHoldDuration = Mathf.Max(0f, warningHoldDuration);

        if (string.IsNullOrWhiteSpace(sleepBlockedMessage))
        {
            sleepBlockedMessage = "Reinforce your house before sleeping";
        }
    }
}
