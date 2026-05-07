using UnityEngine;

public class ShopTriggerVisitTracker : MonoBehaviour
{
    [SerializeField] private bool visited;

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
    }
}
