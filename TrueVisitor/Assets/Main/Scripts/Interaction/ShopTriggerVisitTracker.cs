using UnityEngine;

public class ShopTriggerVisitTracker : MonoBehaviour
{
    [SerializeField] private bool visited;
    [SerializeField] private GameObject[] objectsToDisable;
    [SerializeField] private GameObject[] objectsToEnable;

    public static bool HasVisitedAnyShopTrigger { get; private set; }
    public bool Visited => visited;

    private void Awake()
    {
        if (visited)
        {
            HasVisitedAnyShopTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (visited)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        visited = true;
        HasVisitedAnyShopTrigger = true;
        ApplyObjectStateChanges();
    }

    private void ApplyObjectStateChanges()
    {
        SetObjectsActive(objectsToDisable, false);
        SetObjectsActive(objectsToEnable, true);
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
}
