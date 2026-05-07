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
    [SerializeField] private float losePlayerAfter = 3f;

    [Header("Movement")]
    [SerializeField] private float searchSpeed = 2.2f;
    [SerializeField] private float chaseSpeed = 4.2f;
    [SerializeField] private float searchPointReachDistance = 1.2f;
    [SerializeField] private float searchPointWaitTime = 1f;
    [SerializeField] private float navMeshSnapDistance = 4f;
    [SerializeField] private float fallbackRoamRadius = 8f;
    [SerializeField] private float fallbackRoamWaitTime = 1.5f;
    [SerializeField] private float playerContactResetDistance = 0.75f;

    [Header("Doors")]
    [SerializeField] private float doorCheckDistance = 1.4f;
    [SerializeField] private float doorCheckRadius = 0.35f;
    [SerializeField] private LayerMask doorCheckMask = ~0;
    [SerializeField] private float doorOpenCooldown = 0.5f;

    [Header("Traps")]
    [SerializeField] private float trapCheckRadius = 0.65f;
    [SerializeField] private float trapCheckVerticalTolerance = 1.2f;
    [SerializeField] private LayerMask trapCheckMask = ~0;

    [Header("Audio")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip footstepLoopClip;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.65f;
    [SerializeField] private float footstepMinDistance = 1.5f;
    [SerializeField] private float footstepMaxDistance = 18f;
    [SerializeField] private float footstepSpeedThreshold = 0.15f;
    [SerializeField] private AudioSource breathingAudioSource;
    [SerializeField] private AudioClip breathingLoopClip;
    [SerializeField, Range(0f, 1f)] private float breathingVolume = 0.45f;
    [SerializeField] private float breathingMinDistance = 1.5f;
    [SerializeField] private float breathingMaxDistance = 14f;

    private NavMeshAgent agent;
    private int searchPointIndex;
    private int[] searchOrder;
    private float waitTimer;
    private float lastSawPlayerTime = -999f;
    private float lastDoorOpenTime = -999f;
    private Vector3 spawnPosition;
    private Vector3 fallbackRoamDestination;
    private Vector3 currentSearchDestination;
    private bool hasFallbackRoamDestination;
    private bool hasSearchDestination;
    private bool trapped;
    private bool sceneResetTriggered;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        spawnPosition = transform.position;

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
            agent.isStopped = true;
            UpdateLoopingAudio();
            return;
        }

        TryResetSceneFromPlayerProximity();
        TryTriggerTrapUnderfoot();

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
        if (sceneResetTriggered || other == null)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        sceneResetTriggered = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void TryResetSceneFromPlayerProximity()
    {
        if (sceneResetTriggered || player == null)
        {
            return;
        }

        Vector3 visitorPosition = transform.position;
        Vector3 playerPosition = player.position;
        visitorPosition.y = 0f;
        playerPosition.y = 0f;

        if (Vector3.Distance(visitorPosition, playerPosition) <= playerContactResetDistance)
        {
            sceneResetTriggered = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
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

        StartCoroutine(TrapRoutine(duration, trapPosition));
    }

    private System.Collections.IEnumerator TrapRoutine(float duration, Vector3 trapPosition)
    {
        trapped = true;
        if (IsAgentReady())
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;

            if (NavMesh.SamplePosition(trapPosition, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0f, duration));

        trapped = false;
        if (IsAgentReady())
        {
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
        agent.speed = chaseSpeed;
        if (NavMesh.SamplePosition(player.position, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
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
            agent.SetDestination(currentSearchDestination);
        }
        else
        {
            AdvanceSearchPoint();
        }
    }

    private void FallbackRoam()
    {
        agent.isStopped = false;
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
                agent.SetDestination(fallbackRoamDestination);
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

    private bool IsAgentReady()
    {
        return agent != null && agent.enabled && agent.isOnNavMesh;
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

        door.Open();
        lastDoorOpenTime = Time.time;
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
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = volume;
        source.clip = clip;

        return source;
    }

    private void UpdateLoopingAudio()
    {
        UpdateBreathingAudio();
        UpdateFootstepAudio();
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
        losePlayerAfter = Mathf.Max(0f, losePlayerAfter);
        searchSpeed = Mathf.Max(0f, searchSpeed);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        searchPointReachDistance = Mathf.Max(0.1f, searchPointReachDistance);
        searchPointWaitTime = Mathf.Max(0f, searchPointWaitTime);
        navMeshSnapDistance = Mathf.Max(0f, navMeshSnapDistance);
        fallbackRoamRadius = Mathf.Max(0f, fallbackRoamRadius);
        fallbackRoamWaitTime = Mathf.Max(0f, fallbackRoamWaitTime);
        playerContactResetDistance = Mathf.Max(0.01f, playerContactResetDistance);
        doorCheckDistance = Mathf.Max(0f, doorCheckDistance);
        doorCheckRadius = Mathf.Max(0.01f, doorCheckRadius);
        doorOpenCooldown = Mathf.Max(0f, doorOpenCooldown);
        trapCheckRadius = Mathf.Max(0.01f, trapCheckRadius);
        trapCheckVerticalTolerance = Mathf.Max(0f, trapCheckVerticalTolerance);
        footstepMinDistance = Mathf.Max(0f, footstepMinDistance);
        footstepMaxDistance = Mathf.Max(footstepMinDistance, footstepMaxDistance);
        footstepSpeedThreshold = Mathf.Max(0f, footstepSpeedThreshold);
        breathingMinDistance = Mathf.Max(0f, breathingMinDistance);
        breathingMaxDistance = Mathf.Max(breathingMinDistance, breathingMaxDistance);
    }
}
