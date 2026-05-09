using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class VisitorAI : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform[] searchPoints;
    [SerializeField] private bool autoFindSearchPoints = true;
    [SerializeField] private bool randomizeSearchPointOrder = true;

    [Header("Senses")]
    [SerializeField] private float sightRange = 16f;
    [SerializeField] private float fieldOfView = 115f;
    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    [SerializeField] private float playerVisionBodyHeight = 1.05f;
    [SerializeField] private float playerVisionBodyWidth = 0.45f;
    [SerializeField] private float playerVisionBodyVerticalSpread = 0.35f;
    [SerializeField] private int requiredVisibleBodySamples = 3;
    [SerializeField] private float spotPlayerDelay = 0.45f;
    [SerializeField] private float losePlayerAfter = 3f;

    [Header("Movement")]
    [SerializeField] private float searchSpeed = 2.2f;
    [SerializeField] private float chaseSpeed = 4.2f;
    [SerializeField] private float chaseStoppingDistance = 1.1f;
    [SerializeField] private float chaseAcceleration = 18f;
    [SerializeField] private float chaseAngularSpeed = 720f;
    [SerializeField] private float searchPointReachDistance = 1.2f;
    [SerializeField] private float searchPointWaitTime = 1f;
    [SerializeField] private float navMeshSnapDistance = 4f;
    [SerializeField] private float fallbackRoamRadius = 8f;
    [SerializeField] private float fallbackRoamWaitTime = 1.5f;
    [SerializeField] private float playerContactResetDistance = 1.6f;
    [SerializeField] private float playerContactVerticalTolerance = 1.4f;
    [SerializeField] private float stuckVelocityThreshold = 0.05f;
    [SerializeField] private float stuckRecoveryDelay = 2f;
    [SerializeField] private float noiseInvestigateDuration = 6f;
    [SerializeField] private float noiseReachableSearchRadius = 12f;

    [Header("Doors")]
    [SerializeField] private float doorCheckDistance = 1.4f;
    [SerializeField] private float doorCheckRadius = 0.35f;
    [SerializeField] private LayerMask doorCheckMask = ~0;
    [SerializeField] private float doorOpenCooldown = 0.5f;
    [SerializeField] private float suspiciousOpenDoorSightDistance = 8f;
    [SerializeField] private float suspiciousOpenDoorInvestigationDuration = 5f;
    [SerializeField] private float suspiciousOpenDoorRecheckDelay = 12f;

    [Header("Traps")]
    [SerializeField] private float trapCheckRadius = 0.65f;
    [SerializeField] private float trapCheckVerticalTolerance = 1.2f;
    [SerializeField] private LayerMask trapCheckMask = ~0;
    [SerializeField] private float trapAvoidanceRadius = 1.4f;
    [SerializeField] private float trapAvoidanceDestinationSearchRadius = 3f;

    [Header("Audio")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip footstepLoopClip;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 1f;
    [SerializeField] private float footstepMinDistance = 8f;
    [SerializeField] private float footstepMaxDistance = 55f;
    [SerializeField] private float footstepSpeedThreshold = 0.15f;
    [SerializeField] private AudioSource breathingAudioSource;
    [SerializeField] private AudioClip breathingLoopClip;
    [SerializeField, Range(0f, 1f)] private float breathingVolume = 1f;
    [SerializeField] private float breathingMinDistance = 7f;
    [SerializeField] private float breathingMaxDistance = 45f;
    [SerializeField] private AudioSource spottedAudioSource;
    [SerializeField] private AudioClip spottedClip;
    [SerializeField, Range(0f, 1f)] private float spottedVolume = 1f;
    [SerializeField] private float spottedMinDistance = 8f;
    [SerializeField] private float spottedMaxDistance = 55f;
    [SerializeField] private AudioSource chaseAudioSource;
    [SerializeField] private AudioClip chaseLoopClip;
    [SerializeField, Range(0f, 1f)] private float chaseVolume = 1f;
    [SerializeField] private float chaseMinDistance = 8f;
    [SerializeField] private float chaseMaxDistance = 60f;
    [SerializeField] private AudioSource beartrappedAudioSource;
    [SerializeField] private AudioClip beartrappedClip;
    [SerializeField, Range(0f, 1f)] private float beartrappedVolume = 1f;
    [SerializeField] private float beartrappedMinDistance = 8f;
    [SerializeField] private float beartrappedMaxDistance = 55f;
    [SerializeField] private bool loopBeartrappedAudio;

    private NavMeshAgent agent;
    private int searchPointIndex;
    private int[] searchOrder;
    private float waitTimer;
    private float lastSawPlayerTime = -999f;
    private float playerVisibleSinceTime = -999f;
    private float lastDoorOpenTime = -999f;
    private Vector3 spawnPosition;
    private Vector3 fallbackRoamDestination;
    private Vector3 currentSearchDestination;
    private Vector3 noiseInvestigationPosition;
    private Vector3 noiseInvestigationDestination;
    private Vector3 lastPosition;
    private float stuckTimer;
    private bool hasFallbackRoamDestination;
    private bool hasSearchDestination;
    private bool hasNoiseInvestigationDestination;
    private bool trapped;
    private float trappedUntilTime;
    private bool sceneResetTriggered;
    private bool wasChasingPlayer;
    private float noiseInvestigationEndTime = -999f;
    private DoorInteractable suspiciousOpenDoor;
    private Vector3 suspiciousOpenDoorDestination;
    private float suspiciousOpenDoorInvestigationEndTime = -999f;
    private readonly HashSet<DoorInteractable> doorsOpenedByVisitor = new HashSet<DoorInteractable>();
    private readonly Dictionary<DoorInteractable, float> suspiciousOpenDoorIgnoreUntil = new Dictionary<DoorInteractable, float>();
    private float defaultStoppingDistance;
    private float defaultAcceleration;
    private float defaultAngularSpeed;
    private bool defaultAutoBraking;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        CacheAgentMovementSettings();
        spawnPosition = transform.position;
        lastPosition = transform.position;

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

        BuildSearchOrder();
        SetupLoopingAudio();
    }

    private void OnEnable()
    {
        TryPlaceOnNavMesh();

        if (IsAgentReady())
        {
            agent.isStopped = false;
        }
    }

    private void Update()
    {
        if (!IsAgentReady())
        {
            TryPlaceOnNavMesh();
            UpdateLoopingAudio();
            return;
        }

        if (trapped)
        {
            UpdateTrapState();
            UpdateLoopingAudio();
            return;
        }

        TryResetSceneFromPlayerProximity();
        TryTriggerTrapUnderfoot();

        bool canSeePlayer = CanSeePlayer();
        if (canSeePlayer)
        {
            if (playerVisibleSinceTime < 0f)
            {
                playerVisibleSinceTime = Time.time;
            }

            if (wasChasingPlayer || Time.time - playerVisibleSinceTime >= spotPlayerDelay)
            {
                lastSawPlayerTime = Time.time;
            }
        }
        else
        {
            playerVisibleSinceTime = -999f;
        }

        bool shouldChase = player != null && Time.time - lastSawPlayerTime <= losePlayerAfter;
        if (shouldChase && !wasChasingPlayer)
        {
            PlaySpottedAudio();
        }

        wasChasingPlayer = shouldChase;

        if (shouldChase)
        {
            ChasePlayer();
        }
        else if (IsInvestigatingNoise())
        {
            InvestigateNoise();
        }
        else if (IsInvestigatingSuspiciousOpenDoor())
        {
            InvestigateSuspiciousOpenDoor();
        }
        else if (TryStartSuspiciousOpenDoorInvestigation())
        {
            InvestigateSuspiciousOpenDoor();
        }
        else
        {
            Search();
        }

        TryOpenDoorAhead();
        RecoverIfStuck();
        UpdateLoopingAudio();
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryResetSceneFromContact(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryResetSceneFromContact(other);
    }

    private void TryResetSceneFromContact(Collider other)
    {
        if (sceneResetTriggered || other == null || !wasChasingPlayer)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        ResetActiveScene();
    }

    private void TryResetSceneFromPlayerProximity()
    {
        if (sceneResetTriggered || player == null || !wasChasingPlayer)
        {
            return;
        }

        Vector3 visitorPosition = transform.position;
        Vector3 playerPosition = player.position;
        if (Mathf.Abs(visitorPosition.y - playerPosition.y) > playerContactVerticalTolerance)
        {
            return;
        }

        visitorPosition.y = 0f;
        playerPosition.y = 0f;

        if (Vector3.Distance(visitorPosition, playerPosition) <= playerContactResetDistance)
        {
            ResetActiveScene();
        }
    }

    private void ResetActiveScene()
    {
        if (sceneResetTriggered)
        {
            return;
        }

        sceneResetTriggered = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void TrapForSeconds(float duration)
    {
        TrapForSeconds(duration, transform.position);
    }

    public void TrapForSeconds(float duration, Vector3 trapPosition)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        trapped = true;
        trappedUntilTime = Mathf.Max(trappedUntilTime, Time.time + Mathf.Max(0f, duration));
        wasChasingPlayer = false;
        StopAgentAt(trapPosition);
        PlayBeartrappedAudio();
    }

    public void AlertToNoise(Vector3 noisePosition)
    {
        AlertToNoise(noisePosition, noiseInvestigateDuration);
    }

    public void AlertToNoise(Vector3 noisePosition, float duration)
    {
        if (trapped)
        {
            return;
        }

        bool wasAlreadyInvestigatingNoise = IsInvestigatingNoise();
        noiseInvestigationPosition = noisePosition;
        noiseInvestigationEndTime = Time.time + Mathf.Max(0f, duration);
        hasNoiseInvestigationDestination = false;
        hasSearchDestination = false;
        hasFallbackRoamDestination = false;
        waitTimer = 0f;

        if (IsAgentReady() && TryFindReachableNavMeshPointNear(noiseInvestigationPosition, noiseReachableSearchRadius, out noiseInvestigationDestination))
        {
            hasNoiseInvestigationDestination = true;
            agent.isStopped = false;
            ApplyDefaultMovementSettings();
            agent.speed = chaseSpeed;
            noiseInvestigationDestination = SetTrapAwareDestination(noiseInvestigationDestination);
        }

        if (!wasAlreadyInvestigatingNoise)
        {
            PlaySpottedAudio();
        }
    }

    private void UpdateTrapState()
    {
        if (Time.time < trappedUntilTime)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            return;
        }

        ReleaseFromTrap();
    }

    private void StopAgentAt(Vector3 position)
    {
        if (!IsAgentReady())
        {
            TryPlaceOnNavMesh();
        }

        if (IsAgentReady())
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
    }

    private void ReleaseFromTrap()
    {
        trapped = false;
        hasSearchDestination = false;
        hasFallbackRoamDestination = false;
        StopBeartrappedAudio();
        waitTimer = 0f;

        TryPlaceOnNavMesh();

        if (IsAgentReady())
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
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
        Vector3 bodyCenter = player.position + Vector3.up * playerVisionBodyHeight;
        Vector3 toPlayerCenter = bodyCenter - eyes;
        if (toPlayerCenter.magnitude > sightRange)
        {
            return false;
        }

        Vector3 side = Vector3.Cross(Vector3.up, toPlayerCenter);
        if (side.sqrMagnitude <= 0.0001f)
        {
            side = player.right;
        }
        side.Normalize();

        Vector3[] sightPoints =
        {
            bodyCenter,
            bodyCenter + side * playerVisionBodyWidth,
            bodyCenter - side * playerVisionBodyWidth,
            bodyCenter + Vector3.up * playerVisionBodyVerticalSpread,
            bodyCenter - Vector3.up * playerVisionBodyVerticalSpread
        };

        if (!CanSeePlayerSightPoint(eyes, sightPoints[0]))
        {
            return false;
        }

        int visibleSamples = 1;
        for (int i = 1; i < sightPoints.Length; i++)
        {
            if (CanSeePlayerSightPoint(eyes, sightPoints[i]))
            {
                visibleSamples++;
            }
        }

        return visibleSamples >= requiredVisibleBodySamples;
    }

    private bool CanSeePlayerSightPoint(Vector3 eyes, Vector3 target)
    {
        Vector3 toTarget = target - eyes;
        float distance = toTarget.magnitude;
        if (distance > sightRange || distance <= 0.001f)
        {
            return false;
        }

        Vector3 direction = toTarget / distance;
        if (Vector3.Angle(transform.forward, direction) > fieldOfView * 0.5f)
        {
            return false;
        }

        if (Physics.Raycast(eyes, direction, out RaycastHit hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<VisitorAI>() == this)
            {
                return HasClearLineOfSightPastSelf(eyes, direction, distance);
            }

            return hit.collider.GetComponentInParent<PlayerController>() != null;
        }

        return true;
    }

    private bool HasClearLineOfSightPastSelf(Vector3 origin, Vector3 direction, float distance)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, lineOfSightMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.GetComponentInParent<VisitorAI>() == this)
            {
                continue;
            }

            return hitCollider.GetComponentInParent<PlayerController>() != null;
        }

        return true;
    }

    private void ChasePlayer()
    {
        agent.isStopped = false;
        ApplyChaseMovementSettings();
        agent.speed = chaseSpeed;
        if (NavMesh.SamplePosition(player.position, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas))
        {
            SetTrapAwareDestination(hit.position);
        }
    }

    private bool IsInvestigatingNoise()
    {
        return Time.time <= noiseInvestigationEndTime;
    }

    private bool IsInvestigatingSuspiciousOpenDoor()
    {
        return suspiciousOpenDoor != null && Time.time <= suspiciousOpenDoorInvestigationEndTime;
    }

    private void InvestigateNoise()
    {
        agent.isStopped = false;
        ApplyDefaultMovementSettings();
        agent.speed = chaseSpeed;

        if (!hasNoiseInvestigationDestination)
        {
            if (TryFindReachableNavMeshPointNear(noiseInvestigationPosition, noiseReachableSearchRadius, out noiseInvestigationDestination))
            {
                hasNoiseInvestigationDestination = true;
                noiseInvestigationDestination = SetTrapAwareDestination(noiseInvestigationDestination);
            }
            else
            {
                noiseInvestigationEndTime = Time.time;
                return;
            }
        }

        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = noiseInvestigationDestination;
        currentPosition.y = 0f;
        targetPosition.y = 0f;

        if (Vector3.Distance(currentPosition, targetPosition) <= searchPointReachDistance)
        {
            noiseInvestigationEndTime = Time.time;
            hasNoiseInvestigationDestination = false;
        }
    }

    private bool TryStartSuspiciousOpenDoorInvestigation()
    {
        DoorInteractable door = FindVisibleSuspiciousOpenDoor();
        if (door == null)
        {
            return false;
        }

        if (!TryFindReachableNavMeshPointNear(door.transform.position, noiseReachableSearchRadius, out suspiciousOpenDoorDestination))
        {
            suspiciousOpenDoorIgnoreUntil[door] = Time.time + suspiciousOpenDoorRecheckDelay;
            return false;
        }

        suspiciousOpenDoor = door;
        suspiciousOpenDoorInvestigationEndTime = Time.time + suspiciousOpenDoorInvestigationDuration;
        hasSearchDestination = false;
        hasFallbackRoamDestination = false;
        agent.isStopped = false;
        ApplyDefaultMovementSettings();
        agent.speed = chaseSpeed;
        suspiciousOpenDoorDestination = SetTrapAwareDestination(suspiciousOpenDoorDestination);
        return true;
    }

    private void InvestigateSuspiciousOpenDoor()
    {
        if (suspiciousOpenDoor == null)
        {
            suspiciousOpenDoorInvestigationEndTime = Time.time;
            return;
        }

        agent.isStopped = false;
        ApplyDefaultMovementSettings();
        agent.speed = chaseSpeed;

        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = suspiciousOpenDoorDestination;
        currentPosition.y = 0f;
        targetPosition.y = 0f;

        if (!agent.hasPath)
        {
            suspiciousOpenDoorDestination = SetTrapAwareDestination(suspiciousOpenDoorDestination);
        }

        if (Vector3.Distance(currentPosition, targetPosition) <= searchPointReachDistance || Time.time > suspiciousOpenDoorInvestigationEndTime)
        {
            suspiciousOpenDoorIgnoreUntil[suspiciousOpenDoor] = Time.time + suspiciousOpenDoorRecheckDelay;
            suspiciousOpenDoor = null;
            suspiciousOpenDoorInvestigationEndTime = Time.time;
        }
    }

    private void Search()
    {
        if (searchPoints == null || searchPoints.Length == 0)
        {
            FallbackRoam();
            return;
        }

        Transform target = GetCurrentSearchPoint();
        if (target == null)
        {
            AdvanceSearchPoint();
            return;
        }

        agent.isStopped = false;
        ApplyDefaultMovementSettings();
        agent.speed = searchSpeed;
        SetSearchDestination(target.position);

        if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            AdvanceSearchPoint();
            return;
        }

        if (!agent.pathPending && HasReachedCurrentSearchDestination())
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
        }
    }

    private bool HasReachedCurrentSearchDestination()
    {
        if (!hasSearchDestination)
        {
            return false;
        }

        Vector3 currentPosition = transform.position;
        currentPosition.y = 0f;

        Vector3 destination = currentSearchDestination;
        destination.y = 0f;

        return Vector3.Distance(currentPosition, destination) <= searchPointReachDistance;
    }

    private void AdvanceSearchPoint()
    {
        waitTimer = 0f;
        hasSearchDestination = false;
        if (searchPoints == null || searchPoints.Length == 0)
        {
            return;
        }

        searchPointIndex = (searchPointIndex + 1) % searchPoints.Length;
        if (searchPointIndex == 0 && randomizeSearchPointOrder)
        {
            ShuffleSearchOrder();
        }
    }

    private Transform GetCurrentSearchPoint()
    {
        if (searchPoints == null || searchPoints.Length == 0)
        {
            return null;
        }

        if (searchOrder == null || searchOrder.Length != searchPoints.Length)
        {
            BuildSearchOrder();
        }

        int clampedIndex = Mathf.Clamp(searchPointIndex, 0, searchPoints.Length - 1);
        int pointIndex = searchOrder != null && searchOrder.Length > clampedIndex ? searchOrder[clampedIndex] : clampedIndex;
        if (pointIndex < 0 || pointIndex >= searchPoints.Length)
        {
            return null;
        }

        return searchPoints[pointIndex];
    }

    private void BuildSearchOrder()
    {
        if (searchPoints == null)
        {
            searchOrder = null;
            return;
        }

        searchOrder = new int[searchPoints.Length];
        for (int i = 0; i < searchOrder.Length; i++)
        {
            searchOrder[i] = i;
        }

        if (randomizeSearchPointOrder)
        {
            ShuffleSearchOrder();
        }
    }

    private void ShuffleSearchOrder()
    {
        if (searchOrder == null)
        {
            return;
        }

        for (int i = searchOrder.Length - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            int temporary = searchOrder[i];
            searchOrder[i] = searchOrder[randomIndex];
            searchOrder[randomIndex] = temporary;
        }
    }

    private void SetSearchDestination(Vector3 destination)
    {
        if (hasSearchDestination && Vector3.SqrMagnitude(currentSearchDestination - destination) <= 0.05f)
        {
            return;
        }

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas))
        {
            currentSearchDestination = hit.position;
            hasSearchDestination = true;
            currentSearchDestination = SetTrapAwareDestination(currentSearchDestination);
        }
        else
        {
            AdvanceSearchPoint();
        }
    }

    private void FallbackRoam()
    {
        agent.isStopped = false;
        ApplyDefaultMovementSettings();
        agent.speed = searchSpeed;

        if (!hasFallbackRoamDestination || (!agent.pathPending && agent.remainingDistance <= searchPointReachDistance))
        {
            waitTimer += Time.deltaTime;
            if (hasFallbackRoamDestination && waitTimer < fallbackRoamWaitTime)
            {
                return;
            }

            if (TryFindRandomNavMeshPoint(spawnPosition, fallbackRoamRadius, out fallbackRoamDestination))
            {
                hasFallbackRoamDestination = true;
                waitTimer = 0f;
                fallbackRoamDestination = SetTrapAwareDestination(fallbackRoamDestination);
            }
        }
    }

    private bool TryFindRandomNavMeshPoint(Vector3 center, float radius, out Vector3 point)
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(randomCircle.x, 0f, randomCircle.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }
        }

        point = center;
        return false;
    }

    private Vector3 SetTrapAwareDestination(Vector3 destination)
    {
        if (!TryGetTrapAwareDestination(destination, out Vector3 adjustedDestination))
        {
            adjustedDestination = destination;
        }

        agent.SetDestination(adjustedDestination);
        return adjustedDestination;
    }

    private bool TryGetTrapAwareDestination(Vector3 destination, out Vector3 adjustedDestination)
    {
        adjustedDestination = destination;

        if (trapAvoidanceRadius <= 0f || !HasActiveBeartraps())
        {
            return true;
        }

        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(destination, path))
        {
            return false;
        }

        if (!PathPassesNearActiveBeartrap(path))
        {
            return true;
        }

        bool foundCleanAlternative = false;
        float bestDistanceToOriginal = float.PositiveInfinity;
        Vector3 bestAlternative = destination;
        int ringCount = 3;
        int directionsPerRing = 12;
        float radiusStep = Mathf.Max(0.1f, trapAvoidanceDestinationSearchRadius / ringCount);

        for (int ring = 1; ring <= ringCount; ring++)
        {
            float radius = radiusStep * ring;
            for (int i = 0; i < directionsPerRing; i++)
            {
                float angle = (Mathf.PI * 2f * i) / directionsPerRing;
                Vector3 candidate = destination + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, radiusStep, NavMesh.AllAreas))
                {
                    continue;
                }

                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete || PathPassesNearActiveBeartrap(path))
                {
                    continue;
                }

                float distanceToOriginal = Vector3.SqrMagnitude(hit.position - destination);
                if (distanceToOriginal < bestDistanceToOriginal)
                {
                    foundCleanAlternative = true;
                    bestDistanceToOriginal = distanceToOriginal;
                    bestAlternative = hit.position;
                }
            }
        }

        if (!foundCleanAlternative)
        {
            return true;
        }

        adjustedDestination = bestAlternative;
        return true;
    }

    private bool HasActiveBeartraps()
    {
        IReadOnlyList<BeartrapTrap> traps = BeartrapTrap.ActiveTraps;
        for (int i = 0; i < traps.Count; i++)
        {
            BeartrapTrap trap = traps[i];
            if (trap != null && !trap.IsTriggered)
            {
                return true;
            }
        }

        return false;
    }

    private bool PathPassesNearActiveBeartrap(NavMeshPath path)
    {
        if (path == null || path.corners == null || path.corners.Length == 0)
        {
            return false;
        }

        IReadOnlyList<BeartrapTrap> traps = BeartrapTrap.ActiveTraps;
        float avoidanceRadiusSquared = trapAvoidanceRadius * trapAvoidanceRadius;

        for (int trapIndex = 0; trapIndex < traps.Count; trapIndex++)
        {
            BeartrapTrap trap = traps[trapIndex];
            if (trap == null || trap.IsTriggered)
            {
                continue;
            }

            Vector3 trapPosition = trap.transform.position;
            for (int cornerIndex = 0; cornerIndex < path.corners.Length; cornerIndex++)
            {
                Vector3 corner = path.corners[cornerIndex];
                if (Mathf.Abs(trapPosition.y - corner.y) <= trapCheckVerticalTolerance && DistanceToSegmentSquared2D(trapPosition, corner, corner) <= avoidanceRadiusSquared)
                {
                    return true;
                }
            }

            for (int cornerIndex = 0; cornerIndex < path.corners.Length - 1; cornerIndex++)
            {
                Vector3 start = path.corners[cornerIndex];
                Vector3 end = path.corners[cornerIndex + 1];
                if (Mathf.Abs(trapPosition.y - start.y) > trapCheckVerticalTolerance && Mathf.Abs(trapPosition.y - end.y) > trapCheckVerticalTolerance)
                {
                    continue;
                }

                if (DistanceToSegmentSquared2D(trapPosition, start, end) <= avoidanceRadiusSquared)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private float DistanceToSegmentSquared2D(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector2 point2D = new Vector2(point.x, point.z);
        Vector2 start2D = new Vector2(segmentStart.x, segmentStart.z);
        Vector2 end2D = new Vector2(segmentEnd.x, segmentEnd.z);
        Vector2 segment = end2D - start2D;
        float segmentLengthSquared = segment.sqrMagnitude;
        if (segmentLengthSquared <= Mathf.Epsilon)
        {
            return (point2D - start2D).sqrMagnitude;
        }

        float t = Mathf.Clamp01(Vector2.Dot(point2D - start2D, segment) / segmentLengthSquared);
        Vector2 closest = start2D + segment * t;
        return (point2D - closest).sqrMagnitude;
    }

    private bool TryFindReachableNavMeshPointNear(Vector3 target, float maxRadius, out Vector3 point)
    {
        NavMeshPath path = new NavMeshPath();
        float bestCompleteDistance = float.PositiveInfinity;
        float bestPartialDistance = float.PositiveInfinity;
        Vector3 bestCompletePoint = target;
        Vector3 bestPartialPoint = target;
        bool hasCompletePoint = false;
        bool hasPartialPoint = false;

        CheckReachableCandidate(target, navMeshSnapDistance, path, target, ref hasCompletePoint, ref bestCompleteDistance, ref bestCompletePoint, ref hasPartialPoint, ref bestPartialDistance, ref bestPartialPoint);

        int ringCount = 4;
        int directionsPerRing = 12;
        float radiusStep = Mathf.Max(0.1f, maxRadius / ringCount);

        for (int ring = 1; ring <= ringCount; ring++)
        {
            float radius = radiusStep * ring;
            for (int i = 0; i < directionsPerRing; i++)
            {
                float angle = (Mathf.PI * 2f * i) / directionsPerRing;
                Vector3 candidate = target + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                CheckReachableCandidate(candidate, radiusStep, path, target, ref hasCompletePoint, ref bestCompleteDistance, ref bestCompletePoint, ref hasPartialPoint, ref bestPartialDistance, ref bestPartialPoint);
            }
        }

        if (hasCompletePoint)
        {
            point = bestCompletePoint;
            return true;
        }

        if (hasPartialPoint)
        {
            point = bestPartialPoint;
            return true;
        }

        point = target;
        return false;
    }

    private void CheckReachableCandidate(
        Vector3 candidate,
        float sampleDistance,
        NavMeshPath path,
        Vector3 target,
        ref bool hasCompletePoint,
        ref float bestCompleteDistance,
        ref Vector3 bestCompletePoint,
        ref bool hasPartialPoint,
        ref float bestPartialDistance,
        ref Vector3 bestPartialPoint)
    {
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
        {
            return;
        }

        if (!agent.CalculatePath(hit.position, path))
        {
            return;
        }

        if (path.status == NavMeshPathStatus.PathComplete)
        {
            float distanceToNoise = Vector3.SqrMagnitude(hit.position - target);
            if (distanceToNoise < bestCompleteDistance)
            {
                hasCompletePoint = true;
                bestCompleteDistance = distanceToNoise;
                bestCompletePoint = hit.position;
            }
        }
        else if (path.status == NavMeshPathStatus.PathPartial && path.corners.Length > 0)
        {
            Vector3 partialEnd = path.corners[path.corners.Length - 1];
            float distanceToNoise = Vector3.SqrMagnitude(partialEnd - target);
            if (distanceToNoise < bestPartialDistance)
            {
                hasPartialPoint = true;
                bestPartialDistance = distanceToNoise;
                bestPartialPoint = partialEnd;
            }
        }
    }

    private bool IsAgentReady()
    {
        return agent != null && agent.enabled && agent.isOnNavMesh;
    }

    private void CacheAgentMovementSettings()
    {
        if (agent == null)
        {
            return;
        }

        defaultStoppingDistance = agent.stoppingDistance;
        defaultAcceleration = agent.acceleration;
        defaultAngularSpeed = agent.angularSpeed;
        defaultAutoBraking = agent.autoBraking;
    }

    private void ApplyChaseMovementSettings()
    {
        agent.stoppingDistance = chaseStoppingDistance;
        agent.acceleration = chaseAcceleration;
        agent.angularSpeed = chaseAngularSpeed;
        agent.autoBraking = true;
    }

    private void ApplyDefaultMovementSettings()
    {
        agent.stoppingDistance = defaultStoppingDistance;
        agent.acceleration = defaultAcceleration;
        agent.angularSpeed = defaultAngularSpeed;
        agent.autoBraking = defaultAutoBraking;
    }

    private void TryPlaceOnNavMesh()
    {
        if (agent == null || !agent.enabled || agent.isOnNavMesh)
        {
            return;
        }

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            spawnPosition = hit.position;
        }
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

        doorsOpenedByVisitor.Add(door);
        door.Open(this);
        lastDoorOpenTime = Time.time;
    }

    private DoorInteractable FindVisibleSuspiciousOpenDoor()
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        if (!Physics.SphereCast(origin, doorCheckRadius, transform.forward, out RaycastHit hit, suspiciousOpenDoorSightDistance, doorCheckMask, QueryTriggerInteraction.Collide))
        {
            return null;
        }

        DoorInteractable door = hit.collider.GetComponentInParent<DoorInteractable>();
        if (door == null || !door.IsOpen || doorsOpenedByVisitor.Contains(door))
        {
            return null;
        }

        if (suspiciousOpenDoorIgnoreUntil.TryGetValue(door, out float ignoreUntil) && Time.time < ignoreUntil)
        {
            return null;
        }

        return door;
    }

    private void RecoverIfStuck()
    {
        if (!IsAgentReady() || agent.isStopped || agent.pathPending)
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
            return;
        }

        bool wantsToMove = agent.hasPath && agent.remainingDistance > searchPointReachDistance;
        if (!wantsToMove)
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
            return;
        }

        float movedDistance = Vector3.Distance(transform.position, lastPosition);
        if (movedDistance <= stuckVelocityThreshold)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckRecoveryDelay)
            {
                RecoverNavigation();
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastPosition = transform.position;
    }

    private void RecoverNavigation()
    {
        stuckTimer = 0f;
        hasSearchDestination = false;
        hasFallbackRoamDestination = false;

        TryPlaceOnNavMesh();

        if (IsAgentReady())
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = false;
        }

        AdvanceSearchPoint();
    }

    private void TryTriggerTrapUnderfoot()
    {
        if (TryTriggerRegisteredTrap())
        {
            return;
        }

        Vector3 center = transform.position + Vector3.up * 0.25f;
        Collider[] hits = Physics.OverlapSphere(center, trapCheckRadius, trapCheckMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            BeartrapTrap trap = hits[i].GetComponentInParent<BeartrapTrap>();
            if (trap != null && trap.TryTrigger(this))
            {
                return;
            }
        }
    }

    private bool TryTriggerRegisteredTrap()
    {
        IReadOnlyList<BeartrapTrap> traps = BeartrapTrap.ActiveTraps;
        Vector3 visitorPosition = transform.position;

        for (int i = 0; i < traps.Count; i++)
        {
            BeartrapTrap trap = traps[i];
            if (trap == null || trap.IsTriggered)
            {
                continue;
            }

            Vector3 trapPosition = trap.transform.position;
            if (Mathf.Abs(visitorPosition.y - trapPosition.y) > trapCheckVerticalTolerance)
            {
                continue;
            }

            Vector2 visitorFlat = new Vector2(visitorPosition.x, visitorPosition.z);
            Vector2 trapFlat = new Vector2(trapPosition.x, trapPosition.z);
            if (Vector2.Distance(visitorFlat, trapFlat) <= trapCheckRadius)
            {
                return trap.TryTrigger(this);
            }
        }

        return false;
    }

    private void SetupLoopingAudio()
    {
        footstepAudioSource = SetupLoopingAudioSource(
            footstepAudioSource,
            "Visitor Footsteps Audio",
            footstepLoopClip,
            footstepVolume,
            footstepMinDistance,
            footstepMaxDistance
        );

        breathingAudioSource = SetupLoopingAudioSource(
            breathingAudioSource,
            "Visitor Breathing Audio",
            breathingLoopClip,
            breathingVolume,
            breathingMinDistance,
            breathingMaxDistance
        );

        spottedAudioSource = SetupOneShotAudioSource(
            spottedAudioSource,
            "Visitor Spotted Audio",
            spottedVolume,
            spottedMinDistance,
            spottedMaxDistance
        );

        chaseAudioSource = SetupLoopingAudioSource(
            chaseAudioSource,
            "Visitor Chase Audio",
            chaseLoopClip,
            chaseVolume,
            chaseMinDistance,
            chaseMaxDistance
        );

        beartrappedAudioSource = SetupLoopingAudioSource(
            beartrappedAudioSource,
            "Visitor Beartrapped Audio",
            beartrappedClip,
            beartrappedVolume,
            beartrappedMinDistance,
            beartrappedMaxDistance
        );
        if (beartrappedAudioSource != null)
        {
            beartrappedAudioSource.loop = loopBeartrappedAudio;
        }

        if (breathingAudioSource != null && breathingLoopClip != null && !breathingAudioSource.isPlaying)
        {
            breathingAudioSource.Play();
        }
    }

    private AudioSource SetupLoopingAudioSource(
        AudioSource source,
        string objectName,
        AudioClip clip,
        float volume,
        float minDistance,
        float maxDistance)
    {
        if (source == null)
        {
            Transform existing = transform.Find(objectName);
            if (existing != null)
            {
                source = existing.GetComponent<AudioSource>();
            }
        }

        if (source == null)
        {
            GameObject audioObject = new GameObject(objectName);
            audioObject.transform.SetParent(transform, false);
            source = audioObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = volume;
        source.clip = clip;

        return source;
    }

    private AudioSource SetupOneShotAudioSource(
        AudioSource source,
        string objectName,
        float volume,
        float minDistance,
        float maxDistance)
    {
        source = SetupLoopingAudioSource(source, objectName, null, volume, minDistance, maxDistance);
        source.loop = false;
        return source;
    }

    private void UpdateLoopingAudio()
    {
        UpdateBreathingAudio();
        UpdateFootstepAudio();
        UpdateChaseAudio();
    }

    private void UpdateBreathingAudio()
    {
        if (breathingAudioSource == null || breathingLoopClip == null)
        {
            return;
        }

        breathingAudioSource.volume = breathingVolume;
        breathingAudioSource.minDistance = breathingMinDistance;
        breathingAudioSource.maxDistance = breathingMaxDistance;

        if (!breathingAudioSource.isPlaying)
        {
            breathingAudioSource.Play();
        }
    }

    private void UpdateChaseAudio()
    {
        if (chaseAudioSource == null || chaseLoopClip == null)
        {
            return;
        }

        chaseAudioSource.volume = chaseVolume;
        chaseAudioSource.minDistance = chaseMinDistance;
        chaseAudioSource.maxDistance = chaseMaxDistance;

        bool shouldPlay = wasChasingPlayer && !trapped;
        if (shouldPlay && !chaseAudioSource.isPlaying)
        {
            chaseAudioSource.clip = chaseLoopClip;
            chaseAudioSource.Play();
        }
        else if (!shouldPlay && chaseAudioSource.isPlaying)
        {
            chaseAudioSource.Stop();
        }
    }

    private void PlaySpottedAudio()
    {
        if (spottedAudioSource == null || spottedClip == null)
        {
            return;
        }

        spottedAudioSource.volume = spottedVolume;
        spottedAudioSource.minDistance = spottedMinDistance;
        spottedAudioSource.maxDistance = spottedMaxDistance;
        spottedAudioSource.PlayOneShot(spottedClip, spottedVolume);
    }

    private void PlayBeartrappedAudio()
    {
        if (beartrappedAudioSource == null || beartrappedClip == null)
        {
            return;
        }

        beartrappedAudioSource.volume = beartrappedVolume;
        beartrappedAudioSource.minDistance = beartrappedMinDistance;
        beartrappedAudioSource.maxDistance = beartrappedMaxDistance;
        beartrappedAudioSource.clip = beartrappedClip;
        beartrappedAudioSource.loop = loopBeartrappedAudio;

        if (loopBeartrappedAudio)
        {
            beartrappedAudioSource.Play();
        }
        else
        {
            beartrappedAudioSource.PlayOneShot(beartrappedClip, beartrappedVolume);
        }
    }

    private void StopBeartrappedAudio()
    {
        if (beartrappedAudioSource != null && loopBeartrappedAudio && beartrappedAudioSource.isPlaying)
        {
            beartrappedAudioSource.Stop();
        }
    }

    private void UpdateFootstepAudio()
    {
        if (footstepAudioSource == null || footstepLoopClip == null)
        {
            return;
        }

        footstepAudioSource.volume = footstepVolume;
        footstepAudioSource.minDistance = footstepMinDistance;
        footstepAudioSource.maxDistance = footstepMaxDistance;

        bool shouldPlay = !trapped && IsAgentReady() && agent.velocity.sqrMagnitude >= footstepSpeedThreshold * footstepSpeedThreshold;
        if (shouldPlay && !footstepAudioSource.isPlaying)
        {
            footstepAudioSource.Play();
        }
        else if (!shouldPlay && footstepAudioSource.isPlaying)
        {
            footstepAudioSource.Stop();
        }
    }

    private void OnValidate()
    {
        sightRange = Mathf.Max(0f, sightRange);
        fieldOfView = Mathf.Clamp(fieldOfView, 0f, 360f);
        eyeHeight = Mathf.Max(0f, eyeHeight);
        playerVisionBodyHeight = Mathf.Max(0f, playerVisionBodyHeight);
        playerVisionBodyWidth = Mathf.Max(0f, playerVisionBodyWidth);
        playerVisionBodyVerticalSpread = Mathf.Max(0f, playerVisionBodyVerticalSpread);
        requiredVisibleBodySamples = Mathf.Clamp(requiredVisibleBodySamples, 1, 5);
        spotPlayerDelay = Mathf.Max(0f, spotPlayerDelay);
        losePlayerAfter = Mathf.Max(0f, losePlayerAfter);
        searchSpeed = Mathf.Max(0f, searchSpeed);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        chaseStoppingDistance = Mathf.Max(0f, chaseStoppingDistance);
        chaseAcceleration = Mathf.Max(0.01f, chaseAcceleration);
        chaseAngularSpeed = Mathf.Max(0f, chaseAngularSpeed);
        searchPointReachDistance = Mathf.Max(0.1f, searchPointReachDistance);
        searchPointWaitTime = Mathf.Max(0f, searchPointWaitTime);
        navMeshSnapDistance = Mathf.Max(0f, navMeshSnapDistance);
        fallbackRoamRadius = Mathf.Max(0f, fallbackRoamRadius);
        fallbackRoamWaitTime = Mathf.Max(0f, fallbackRoamWaitTime);
        playerContactResetDistance = Mathf.Max(0.01f, playerContactResetDistance);
        playerContactVerticalTolerance = Mathf.Max(0f, playerContactVerticalTolerance);
        stuckVelocityThreshold = Mathf.Max(0f, stuckVelocityThreshold);
        stuckRecoveryDelay = Mathf.Max(0f, stuckRecoveryDelay);
        noiseInvestigateDuration = Mathf.Max(0f, noiseInvestigateDuration);
        noiseReachableSearchRadius = Mathf.Max(0f, noiseReachableSearchRadius);
        doorCheckDistance = Mathf.Max(0f, doorCheckDistance);
        doorCheckRadius = Mathf.Max(0.01f, doorCheckRadius);
        doorOpenCooldown = Mathf.Max(0f, doorOpenCooldown);
        suspiciousOpenDoorSightDistance = Mathf.Max(0f, suspiciousOpenDoorSightDistance);
        suspiciousOpenDoorInvestigationDuration = Mathf.Max(0f, suspiciousOpenDoorInvestigationDuration);
        suspiciousOpenDoorRecheckDelay = Mathf.Max(0f, suspiciousOpenDoorRecheckDelay);
        trapCheckRadius = Mathf.Max(0.01f, trapCheckRadius);
        trapCheckVerticalTolerance = Mathf.Max(0f, trapCheckVerticalTolerance);
        trapAvoidanceRadius = Mathf.Max(0f, trapAvoidanceRadius);
        trapAvoidanceDestinationSearchRadius = Mathf.Max(0f, trapAvoidanceDestinationSearchRadius);
        footstepMinDistance = Mathf.Max(0f, footstepMinDistance);
        footstepMaxDistance = Mathf.Max(footstepMinDistance, footstepMaxDistance);
        footstepSpeedThreshold = Mathf.Max(0f, footstepSpeedThreshold);
        breathingMinDistance = Mathf.Max(0f, breathingMinDistance);
        breathingMaxDistance = Mathf.Max(breathingMinDistance, breathingMaxDistance);
        spottedMinDistance = Mathf.Max(0f, spottedMinDistance);
        spottedMaxDistance = Mathf.Max(spottedMinDistance, spottedMaxDistance);
        chaseMinDistance = Mathf.Max(0f, chaseMinDistance);
        chaseMaxDistance = Mathf.Max(chaseMinDistance, chaseMaxDistance);
        beartrappedMinDistance = Mathf.Max(0f, beartrappedMinDistance);
        beartrappedMaxDistance = Mathf.Max(beartrappedMinDistance, beartrappedMaxDistance);
    }
}
