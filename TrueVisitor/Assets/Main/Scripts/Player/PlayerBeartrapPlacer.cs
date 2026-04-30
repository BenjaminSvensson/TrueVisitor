using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBeartrapPlacer : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Camera placementCamera;
    [SerializeField] private GameObject beartrapPrefab;
    [SerializeField] private string itemId = "beartrap";
    [SerializeField] private float placementDistance = 2.5f;
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private float placementYOffset = 0.02f;
    [SerializeField] private float groundProbeHeight = 1.5f;
    [SerializeField] private float groundProbeDistance = 4f;
    [SerializeField] private bool deselectAfterPlace = true;

    private bool selected;

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }

        if (placementCamera == null)
        {
            placementCamera = GetComponentInChildren<Camera>();
        }
    }

    private void Update()
    {
        if (WasSelectPressed())
        {
            ToggleSelection();
        }

        if (!selected)
        {
            return;
        }

        if (inventory == null || inventory.GetQuantity(itemId) <= 0)
        {
            selected = false;
            return;
        }

        if (WasCancelPressed())
        {
            selected = false;
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPlaceSelectedBeartrap();
        }
    }

    private void ToggleSelection()
    {
        if (inventory == null || inventory.GetQuantity(itemId) <= 0)
        {
            selected = false;
            return;
        }

        selected = !selected;
    }

    private void TryPlaceSelectedBeartrap()
    {
        if (beartrapPrefab == null || inventory == null)
        {
            return;
        }

        if (!TryGetPlacementPose(out Vector3 position, out Quaternion rotation))
        {
            return;
        }

        if (!inventory.TryRemoveItem(itemId))
        {
            selected = false;
            return;
        }

        GameObject placedTrap = Instantiate(beartrapPrefab, position, rotation);
        BeartrapTrap trap = placedTrap.GetComponent<BeartrapTrap>();
        if (trap != null)
        {
            trap.InitializeOpen();
        }

        if (deselectAfterPlace || inventory.GetQuantity(itemId) <= 0)
        {
            selected = false;
        }
    }

    private bool TryGetPlacementPose(out Vector3 position, out Quaternion rotation)
    {
        Transform viewTransform = placementCamera != null ? placementCamera.transform : transform;
        Ray placementRay = new Ray(viewTransform.position, viewTransform.forward);

        if (Physics.Raycast(placementRay, out RaycastHit hit, placementDistance, placementMask, QueryTriggerInteraction.Ignore))
        {
            position = hit.point + hit.normal * placementYOffset;
            rotation = GetFlatPlacementRotation(viewTransform.forward, hit.normal);
            return true;
        }

        Vector3 fallback = viewTransform.position + viewTransform.forward * placementDistance;
        Vector3 probeStart = fallback + Vector3.up * groundProbeHeight;
        if (Physics.Raycast(probeStart, Vector3.down, out hit, groundProbeHeight + groundProbeDistance, placementMask, QueryTriggerInteraction.Ignore))
        {
            position = hit.point + hit.normal * placementYOffset;
            rotation = GetFlatPlacementRotation(viewTransform.forward, hit.normal);
            return true;
        }

        position = default;
        rotation = default;
        return false;
    }

    private Quaternion GetFlatPlacementRotation(Vector3 forward, Vector3 up)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, up);
        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = Vector3.ProjectOnPlane(transform.forward, up);
        }

        return Quaternion.LookRotation(flatForward.normalized, up);
    }

    private bool WasSelectPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame);
    }

    private bool WasCancelPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
    }

    private void OnValidate()
    {
        placementDistance = Mathf.Max(0.1f, placementDistance);
        placementYOffset = Mathf.Max(0f, placementYOffset);
        groundProbeHeight = Mathf.Max(0f, groundProbeHeight);
        groundProbeDistance = Mathf.Max(0.1f, groundProbeDistance);

        if (string.IsNullOrWhiteSpace(itemId))
        {
            itemId = "beartrap";
        }
    }
}
