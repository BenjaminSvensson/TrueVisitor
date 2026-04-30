using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBeartrapPlacer : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Camera placementCamera;
    [SerializeField] private GameObject beartrapPrefab;
    [SerializeField] private string itemId = "beartrap";
    [SerializeField] private float placementDistance = 2.5f;
    [SerializeField] private string placementLayerName = "Ground";
    [SerializeField] private LayerMask placementMask = 1 << 8;
    [SerializeField] private float placementYOffset = 0f;
    [SerializeField, Range(0f, 1f)] private float minimumSurfaceUpDot = 0.2f;
    [SerializeField] private float groundProbeHeight = 1.5f;
    [SerializeField] private float groundProbeDistance = 4f;
    [SerializeField] private bool deselectAfterPlace = true;

    private int resolvedPlacementMask;

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

        ResolvePlacementMask();
    }

    private void Update()
    {
        if (WasSelectPressed())
        {
            ToggleSelection();
        }

        if (!IsSelected())
        {
            return;
        }

        if (inventory == null || inventory.GetQuantity(itemId) <= 0)
        {
            inventory?.ClearSelectedItem();
            return;
        }

        if (WasCancelPressed())
        {
            inventory.ClearSelectedItem();
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPlaceSelectedItem();
        }
    }

    private void ToggleSelection()
    {
        if (inventory == null || inventory.GetQuantity(itemId) <= 0)
        {
            inventory?.ClearSelectedItem();
            return;
        }

        inventory.ToggleSelectedItem(itemId);
    }

    public bool TryPlaceSelectedItem()
    {
        if (!IsSelected() || beartrapPrefab == null || inventory == null)
        {
            return false;
        }

        if (!TryGetPlacementPose(out Vector3 position, out Quaternion rotation, out Vector3 surfacePoint, out Vector3 surfaceNormal))
        {
            return false;
        }

        if (!inventory.TryRemoveItem(itemId))
        {
            inventory.ClearSelectedItem();
            return false;
        }

        GameObject placedTrap = Instantiate(beartrapPrefab, position, rotation);
        AlignPlacedTrapToSurface(placedTrap.transform, surfacePoint, surfaceNormal);

        BeartrapTrap trap = placedTrap.GetComponent<BeartrapTrap>();
        if (trap != null)
        {
            trap.InitializeOpen();
        }

        if (deselectAfterPlace || inventory.GetQuantity(itemId) <= 0)
        {
            inventory.ClearSelectedItem();
        }

        return true;
    }

    private bool IsSelected()
    {
        return inventory != null && inventory.IsSelected(itemId);
    }

    private bool TryGetPlacementPose(out Vector3 position, out Quaternion rotation, out Vector3 surfacePoint, out Vector3 surfaceNormal)
    {
        Transform viewTransform = placementCamera != null ? placementCamera.transform : transform;
        Ray placementRay = new Ray(viewTransform.position, viewTransform.forward);
        Vector3 probeCenter = viewTransform.position + viewTransform.forward * placementDistance;

        int mask = GetPlacementMask();
        if (mask == 0)
        {
            position = default;
            rotation = default;
            surfacePoint = default;
            surfaceNormal = Vector3.up;
            return false;
        }

        if (Physics.Raycast(placementRay, out RaycastHit hit, placementDistance, mask, QueryTriggerInteraction.Ignore))
        {
            probeCenter = hit.point;
            if (TryBuildPlacementFromHit(hit, viewTransform.forward, out position, out rotation, out surfacePoint, out surfaceNormal))
            {
                return true;
            }
        }

        Vector3 probeStart = probeCenter + Vector3.up * groundProbeHeight;
        if (Physics.Raycast(probeStart, Vector3.down, out hit, groundProbeHeight + groundProbeDistance, mask, QueryTriggerInteraction.Ignore))
        {
            if (TryBuildPlacementFromHit(hit, viewTransform.forward, out position, out rotation, out surfacePoint, out surfaceNormal))
            {
                return true;
            }
        }

        position = default;
        rotation = default;
        surfacePoint = default;
        surfaceNormal = Vector3.up;
        return false;
    }

    private int GetPlacementMask()
    {
        if (resolvedPlacementMask == 0)
        {
            ResolvePlacementMask();
        }

        return resolvedPlacementMask;
    }

    private void ResolvePlacementMask()
    {
        int layer = LayerMask.NameToLayer(placementLayerName);
        if (layer >= 0)
        {
            resolvedPlacementMask = 1 << layer;
            placementMask = resolvedPlacementMask;
            return;
        }

        resolvedPlacementMask = placementMask.value;
        Debug.LogWarning($"{nameof(PlayerBeartrapPlacer)} could not find a placement layer named '{placementLayerName}'.", this);
    }

    private bool TryBuildPlacementFromHit(
        RaycastHit hit,
        Vector3 forward,
        out Vector3 position,
        out Quaternion rotation,
        out Vector3 surfacePoint,
        out Vector3 surfaceNormal)
    {
        surfacePoint = hit.point;
        surfaceNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;

        if (Vector3.Dot(surfaceNormal, Vector3.up) < minimumSurfaceUpDot)
        {
            position = default;
            rotation = default;
            return false;
        }

        position = surfacePoint + surfaceNormal * placementYOffset;
        rotation = GetFlatPlacementRotation(forward, surfaceNormal);
        return true;
    }

    private void AlignPlacedTrapToSurface(Transform placedTrap, Vector3 surfacePoint, Vector3 surfaceNormal)
    {
        if (placedTrap == null)
        {
            return;
        }

        Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        if (!TryGetLowestMeshBoundsProjection(placedTrap, normal, out float lowestPoint)
            && !TryGetLowestRendererProjection(placedTrap, normal, out lowestPoint))
        {
            return;
        }

        float targetPoint = Vector3.Dot(surfacePoint + normal * placementYOffset, normal);
        placedTrap.position += normal * (targetPoint - lowestPoint);
    }

    private bool TryGetLowestMeshBoundsProjection(Transform root, Vector3 normal, out float lowestPoint)
    {
        lowestPoint = float.PositiveInfinity;

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            Mesh mesh = meshFilter.sharedMesh;
            Renderer renderer = meshFilter.GetComponent<Renderer>();
            if (mesh == null || renderer == null || !renderer.enabled)
            {
                continue;
            }

            IncludeLocalBounds(mesh.bounds, meshFilter.transform, normal, ref lowestPoint);
        }

        SkinnedMeshRenderer[] skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>();
        for (int i = 0; i < skinnedRenderers.Length; i++)
        {
            SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[i];
            if (!skinnedRenderer.enabled)
            {
                continue;
            }

            IncludeLocalBounds(skinnedRenderer.localBounds, skinnedRenderer.transform, normal, ref lowestPoint);
        }

        return !float.IsPositiveInfinity(lowestPoint);
    }

    private void IncludeLocalBounds(Bounds bounds, Transform boundsTransform, Vector3 normal, ref float lowestPoint)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 worldCorner = boundsTransform.TransformPoint(localCorner);
                    lowestPoint = Mathf.Min(lowestPoint, Vector3.Dot(worldCorner, normal));
                }
            }
        }
    }

    private bool TryGetLowestRendererProjection(Transform root, Vector3 normal, out float lowestPoint)
    {
        lowestPoint = float.PositiveInfinity;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds bounds = renderers[i].bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        lowestPoint = Mathf.Min(lowestPoint, Vector3.Dot(corner, normal));
                    }
                }
            }
        }

        return !float.IsPositiveInfinity(lowestPoint);
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
        minimumSurfaceUpDot = Mathf.Clamp01(minimumSurfaceUpDot);
        groundProbeHeight = Mathf.Max(0f, groundProbeHeight);
        groundProbeDistance = Mathf.Max(0.1f, groundProbeDistance);

        if (string.IsNullOrWhiteSpace(itemId))
        {
            itemId = "beartrap";
        }

        if (string.IsNullOrWhiteSpace(placementLayerName))
        {
            placementLayerName = "Ground";
        }
    }
}
