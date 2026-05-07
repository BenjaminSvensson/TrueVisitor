using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class VisitorAI : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform[] searchPoints;
    [SerializeField] private bool autoFindSearchPoints = true;

    [Header("Senses")]
    [SerializeField] private float sightRange = 16f;
    [SerializeField] private float fieldOfView = 115f;
    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    [SerializeField] private float losePlayerAfter = 3f;

    [Header("Movement")]
    [SerializeField] private float searchSpeed = 2.2f;
    [SerializeField] private float chaseSpeed = 4.2f;
    [SerializeField] private float searchPointReachDistance = 1.2f;
    [SerializeField] private float searchPointWaitTime = 1f;

    [Header("Doors")]
    [SerializeField] private float doorCheckDistance = 1.4f;
    [SerializeField] private float doorCheckRadius = 0.35f;
    [SerializeField] private LayerMask doorCheckMask = ~0;
    [SerializeField] private float doorOpenCooldown = 0.5f;

    private NavMeshAgent agent;
    private int searchPointIndex;
    private float waitTimer;
    private float lastSawPlayerTime = -999f;
    private float lastDoorOpenTime = -999f;
    private bool trapped;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (player == null)
        {
            PlayerController playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                player = playerController.transform;
            }
        }

        if (autoFindSearchPoints && (searchPoints == null || searchPoints.Length == 0))
        {
            VisitorSearchPoint[] points = FindObjectsByType<VisitorSearchPoint>(FindObjectsSortMode.None);
            searchPoints = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                searchPoints[i] = points[i].transform;
            }
        }
    }

    private void OnEnable()
    {
        if (agent != null)
        {
            agent.isStopped = false;
        }
    }

    private void Update()
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        if (trapped)
        {
            agent.isStopped = true;
            return;
        }

        bool canSeePlayer = CanSeePlayer();
        if (canSeePlayer)
        {
            lastSawPlayerTime = Time.time;
        }

        if (player != null && Time.time - lastSawPlayerTime <= losePlayerAfter)
        {
            ChasePlayer();
        }
        else
        {
            Search();
        }

        TryOpenDoorAhead();
    }

    public void TrapForSeconds(float duration)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        StartCoroutine(TrapRoutine(duration));
    }

    private System.Collections.IEnumerator TrapRoutine(float duration)
    {
        trapped = true;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        yield return new WaitForSeconds(Mathf.Max(0f, duration));

        trapped = false;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    private bool CanSeePlayer()
    {
        if (player == null)
        {
            return false;
        }

        Vector3 eyes = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = target - eyes;
        float distance = toPlayer.magnitude;
        if (distance > sightRange)
        {
            return false;
        }

        Vector3 direction = toPlayer / distance;
        if (Vector3.Angle(transform.forward, direction) > fieldOfView * 0.5f)
        {
            return false;
        }

        if (Physics.Raycast(eyes, direction, out RaycastHit hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.GetComponentInParent<PlayerController>() != null;
        }

        return true;
    }

    private void ChasePlayer()
    {
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);
    }

    private void Search()
    {
        if (searchPoints == null || searchPoints.Length == 0)
        {
            agent.isStopped = true;
            return;
        }

        Transform target = searchPoints[searchPointIndex];
        if (target == null)
        {
            AdvanceSearchPoint();
            return;
        }

        agent.isStopped = false;
        agent.speed = searchSpeed;

        if (!agent.pathPending && agent.remainingDistance <= searchPointReachDistance)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= searchPointWaitTime)
            {
                AdvanceSearchPoint();
            }
        }
        else
        {
            waitTimer = 0f;
            agent.SetDestination(target.position);
        }
    }

    private void AdvanceSearchPoint()
    {
        waitTimer = 0f;
        if (searchPoints == null || searchPoints.Length == 0)
        {
            return;
        }

        searchPointIndex = (searchPointIndex + 1) % searchPoints.Length;
    }

    private void TryOpenDoorAhead()
    {
        if (Time.time - lastDoorOpenTime < doorOpenCooldown)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        if (!Physics.SphereCast(origin, doorCheckRadius, transform.forward, out RaycastHit hit, doorCheckDistance, doorCheckMask, QueryTriggerInteraction.Collide))
        {
            return;
        }

        DoorInteractable door = hit.collider.GetComponentInParent<DoorInteractable>();
        if (door == null || door.IsOpen)
        {
            return;
        }

        door.Open();
        lastDoorOpenTime = Time.time;
    }

    private void OnValidate()
    {
        sightRange = Mathf.Max(0f, sightRange);
        fieldOfView = Mathf.Clamp(fieldOfView, 0f, 360f);
        eyeHeight = Mathf.Max(0f, eyeHeight);
        losePlayerAfter = Mathf.Max(0f, losePlayerAfter);
        searchSpeed = Mathf.Max(0f, searchSpeed);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        searchPointReachDistance = Mathf.Max(0.1f, searchPointReachDistance);
        searchPointWaitTime = Mathf.Max(0f, searchPointWaitTime);
        doorCheckDistance = Mathf.Max(0f, doorCheckDistance);
        doorCheckRadius = Mathf.Max(0.01f, doorCheckRadius);
        doorOpenCooldown = Mathf.Max(0f, doorOpenCooldown);
    }
}
