using UnityEngine;

public class PlayerHand : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer handMesh;
    [SerializeField] private Animator handAnimator;
    [SerializeField] private Camera playerCamera;

    [Header("Hand Images")]
    [SerializeField] private Texture normalHandImage;
    [SerializeField] private Texture interactHandImage;
    [SerializeField] private string textureProperty = "_BaseMap";
    [SerializeField] private string fallbackTextureProperty = "_MainTex";

    [Header("Interact Detection")]
    [SerializeField] private LayerMask interactibleMask;
    [SerializeField] private float interactCheckDistance = 3f;

    // Converted to Animator Hashes for better performance
    private readonly int isRunningHash = Animator.StringToHash("IsRunning");
    private readonly int isCrouchingHash = Animator.StringToHash("IsCrouching");
    private readonly int interactTriggerHash = Animator.StringToHash("Interact");
    private readonly int jumpTriggerHash = Animator.StringToHash("Jump");

    private bool isRunning;
    private bool isCrouching;
    private bool isLookingAtInteractible;
    private bool showingInteractHand;
    private bool hasAppliedHandVisual;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        ApplyHandVisual(false);
    }

    private void Update()
    {
        UpdateInteractLookState();
        UpdateAnimator();
        ApplyHandVisual(isLookingAtInteractible);
    }

    public void SetMovementState(bool running, bool crouching)
    {
        isRunning = running;
        isCrouching = crouching;
    }

    public void TryTriggerInteract()
    {
        if (!isLookingAtInteractible) return;

        if (handAnimator != null)
        {
            handAnimator.SetTrigger(interactTriggerHash);
        }
    }

    public void TriggerJump()
    {
        if (handAnimator != null)
        {
            handAnimator.SetTrigger(jumpTriggerHash);
        }
    }

    private void UpdateAnimator()
    {
        if (handAnimator == null) return;

        // Pass the booleans to the Animator, let the Animator handle transitions and loops!
        handAnimator.SetBool(isRunningHash, isRunning);
        handAnimator.SetBool(isCrouchingHash, isCrouching);
    }

    private void UpdateInteractLookState()
    {
        if (playerCamera == null)
        {
            isLookingAtInteractible = false;
            return;
        }

        Transform camTransform = playerCamera.transform;
        isLookingAtInteractible = Physics.Raycast(
            camTransform.position,
            camTransform.forward,
            interactCheckDistance,
            interactibleMask,
            QueryTriggerInteraction.Ignore);
    }

    private void ApplyHandVisual(bool useInteractVisual)
    {
        if (handMesh == null) return;

        if (hasAppliedHandVisual && showingInteractHand == useInteractVisual)
            return;

        Texture nextTexture = useInteractVisual ? interactHandImage : normalHandImage;
        if (nextTexture == null) return;

        Material material = handMesh.material;

        if (material.HasProperty(textureProperty))
        {
            material.SetTexture(textureProperty, nextTexture);
        }
        else if (material.HasProperty(fallbackTextureProperty))
        {
            material.SetTexture(fallbackTextureProperty, nextTexture);
        }

        showingInteractHand = useInteractVisual;
        hasAppliedHandVisual = true;
    }
}