using UnityEngine;

public class ShopKeeperAnimationController : MonoBehaviour
{
    private const string IdleStateName = "Idle";
    private const string SeeingPlayerStateName = "SeeingPlayer";
    private const string HappyStateName = "Happy";

    [SerializeField] private Animator animator;
    [SerializeField] private Transform player;
    [SerializeField] private float playerNearDistance = 5f;
    [SerializeField] private float crossFadeDuration = 0.15f;
    [SerializeField] private float seeingPlayerDuration = 2.5f;
    [SerializeField] private float happyDuration = 2.5f;

    private bool hasSeenPlayer;
    private float seeingPlayerUntilTime;
    private float happyUntilTime;
    private string currentStateName;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void OnEnable()
    {
        BuyInteractable.Purchased += HandlePurchased;
        Play(IdleStateName);
    }

    private void OnDisable()
    {
        BuyInteractable.Purchased -= HandlePurchased;
    }

    private void Update()
    {
        if (Time.time < happyUntilTime)
        {
            Play(HappyStateName);
            return;
        }

        if (Time.time < seeingPlayerUntilTime)
        {
            Play(SeeingPlayerStateName);
            return;
        }

        if (!hasSeenPlayer && IsPlayerNear())
        {
            hasSeenPlayer = true;
            seeingPlayerUntilTime = Time.time + seeingPlayerDuration;
            Play(SeeingPlayerStateName);
            return;
        }

        Play(IdleStateName);
    }

    public void PlayHappy()
    {
        happyUntilTime = Time.time + happyDuration;
        Play(HappyStateName);
    }

    private void HandlePurchased(BuyInteractable buyInteractable)
    {
        PlayHappy();
    }

    private bool IsPlayerNear()
    {
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null)
        {
            return false;
        }

        float nearDistanceSquared = playerNearDistance * playerNearDistance;
        return (playerTransform.position - transform.position).sqrMagnitude <= nearDistanceSquared;
    }

    private Transform GetPlayerTransform()
    {
        if (player != null)
        {
            return player;
        }

        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null)
        {
            player = playerController.transform;
        }

        return player;
    }

    private void Play(string stateName)
    {
        if (animator == null || currentStateName == stateName)
        {
            return;
        }

        currentStateName = stateName;
        animator.CrossFadeInFixedTime(stateName, crossFadeDuration);
    }
}
