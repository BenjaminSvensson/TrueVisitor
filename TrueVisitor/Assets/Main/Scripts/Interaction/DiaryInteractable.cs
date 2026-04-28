using UnityEngine;

public class DiaryInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private CutsceneEvents cutsceneEvents;

    public void Interact()
    {
        CutsceneEvents diaryEvents = GetCutsceneEvents();
        if (diaryEvents == null)
        {
            Debug.LogWarning($"{nameof(DiaryInteractable)} could not find {nameof(CutsceneEvents)} to open the diary.", this);
            return;
        }

        diaryEvents.OpenDiary();
    }

    private CutsceneEvents GetCutsceneEvents()
    {
        if (cutsceneEvents != null)
        {
            return cutsceneEvents;
        }

        cutsceneEvents = FindAnyObjectByType<CutsceneEvents>();
        return cutsceneEvents;
    }
}
