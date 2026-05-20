using UnityEngine;

public class ShopKeeperAnimationController : MonoBehaviour
{
    private const int IdleState = 0;
    private const int SeeingPlayerState = 1;
    private const int HappyState = 2;
    private static readonly int ShopKeeperStateHash = Animator.StringToHash("ShopKeeperState");

    [SerializeField] private Animator animator;
    [SerializeField, HideInInspector] private Transform player;
    [SerializeField] private float playerNearDistance = 5f;
    [SerializeField] private float seeingPlayerDuration = 2.5f;
    [SerializeField] private float happyDuration = 2.5f;

    private bool hasSeenPlayer;
    private float seeingPlayerUntilTime;
    private float happyUntilTime;
    private int currentState = -1;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        SetAnimatorState(IdleState);
    }

    private void OnEnable()
    {
        BuyInteractable.Purchased += HandlePurchased;
        SetAnimatorState(IdleState);
    }

    private void OnDisable()
    {
        BuyInteractable.Purchased -= HandlePurchased;
    }

    private void Update()
    {
        if (Time.time < happyUntilTime)
        {
            SetAnimatorState(HappyState);
            return;
        }

        if (Time.time < seeingPlayerUntilTime)
        {
            SetAnimatorState(SeeingPlayerState);
            return;
        }

        if (!hasSeenPlayer && IsPlayerNear())
        {
            hasSeenPlayer = true;
            seeingPlayerUntilTime = Time.time + seeingPlayerDuration;
            SetAnimatorState(SeeingPlayerState);
            return;
        }

        SetAnimatorState(IdleState);
    }

    public void PlayHappy()
    {
        happyUntilTime = Time.time + happyDuration;
        SetAnimatorState(HappyState);
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

    private void SetAnimatorState(int state)
    {
        if (animator == null || currentState == state)
        {
            return;
        }

        currentState = state;
        animator.SetInteger(ShopKeeperStateHash, state);
    }
}
