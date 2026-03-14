using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActionsAsset;
    [SerializeField] private Transform moveOrientation;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerHand playerHand;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float jumpHeight = 1.3f;
    [SerializeField] private float gravity = -25f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool invertY;
    [SerializeField] private bool lockCursorOnStart = true;

    [Header("Crouch")]
    [SerializeField] private bool toggleCrouch = true;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchTransitionSpeed = 10f;

    private CharacterController characterController;
    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction crouchAction;
    private InputAction interactAction;

    private Vector2 moveInput;
    private float verticalVelocity;
    private bool jumpRequested;
    private bool isCrouched;

    private float standingHeight;
    private Vector3 standingCenter;
    private float standingCameraHeight;
    private float yaw;
    private float pitch;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        standingHeight = characterController.height;
        standingCenter = characterController.center;

        if (moveOrientation == null)
            moveOrientation = transform;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
        {
            standingCameraHeight = cameraTransform.localPosition.y;
            pitch = NormalizeAngle(cameraTransform.localEulerAngles.x);
        }

        if (playerHand == null)
            playerHand = GetComponentInChildren<PlayerHand>();

        yaw = transform.eulerAngles.y;

        ResolveActions();
    }

    private void OnEnable()
    {
        playerMap?.Enable();

        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (jumpAction != null) jumpAction.performed += OnJumpPerformed;
        if (crouchAction != null)
        {
            crouchAction.performed += OnCrouchPerformed;
            crouchAction.canceled += OnCrouchCanceled;
        }
        if (interactAction != null) interactAction.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        if (jumpAction != null) jumpAction.performed -= OnJumpPerformed;
        if (crouchAction != null)
        {
            crouchAction.performed -= OnCrouchPerformed;
            crouchAction.canceled -= OnCrouchCanceled;
        }
        if (interactAction != null) interactAction.performed -= OnInteractPerformed;

        playerMap?.Disable();

        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        HandleLook();
        UpdateMovementInput();
        HandleJumpAndGravity();
        MoveCharacter();
        UpdateCrouchState();
        UpdateHandState();
    }

    private void ResolveActions()
    {
        if (inputActionsAsset == null)
        {
            Debug.LogError("PlayerController is missing Input Actions Asset. Assign InputSystem_Actions.inputactions.", this);
            return;
        }

        playerMap = inputActionsAsset.FindActionMap("Player", true);
        moveAction = playerMap.FindAction("Move", true);
        lookAction = playerMap.FindAction("Look", true);
        sprintAction = playerMap.FindAction("Sprint", false);
        jumpAction = playerMap.FindAction("Jump", true);
        crouchAction = playerMap.FindAction("Crouch", true);
        interactAction = playerMap.FindAction("Interact", true);
    }

    private void HandleLook()
    {
        if (lookAction == null) return;

        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        yaw += lookInput.x * lookSensitivity;

        float yDelta = lookInput.y * lookSensitivity;
        pitch += invertY ? yDelta : -yDelta;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cameraTransform == null) return;

        if (cameraTransform.parent == transform)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        else
        {
            cameraTransform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    private void UpdateMovementInput()
    {
        if (moveAction != null)
        {
            moveInput = moveAction.ReadValue<Vector2>();
        }
    }

    private void HandleJumpAndGravity()
    {
        bool isGrounded = characterController.isGrounded;

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (jumpRequested && isGrounded && !isCrouched)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            playerHand?.TriggerJump();
        }

        jumpRequested = false;
        verticalVelocity += gravity * Time.deltaTime;
    }

    private void MoveCharacter()
    {
        Vector3 forward = moveOrientation.forward;
        Vector3 right = moveOrientation.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 horizontalMove = (right * moveInput.x + forward * moveInput.y);

        if (horizontalMove.sqrMagnitude > 1f)
        {
            horizontalMove.Normalize();
        }

        float moveSpeed = GetCurrentMoveSpeed(horizontalMove);
        Vector3 velocity = horizontalMove * moveSpeed + Vector3.up * verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }

    private float GetCurrentMoveSpeed(Vector3 horizontalMove)
    {
        if (isCrouched) return crouchSpeed;

        bool hasMoveInput = horizontalMove.sqrMagnitude > 0.0001f;
        bool isSprinting = sprintAction != null && sprintAction.IsPressed() && hasMoveInput;

        return isSprinting ? runSpeed : walkSpeed;
    }

    private void UpdateCrouchState()
    {
        // 1. Adjust Character Controller Height and Center
        float targetHeight = isCrouched ? crouchHeight : standingHeight;
        float newHeight = Mathf.Lerp(characterController.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        characterController.height = newHeight;

        float centerYOffset = (newHeight - standingHeight) * 0.5f;
        characterController.center = new Vector3(standingCenter.x, standingCenter.y + centerYOffset, standingCenter.z);

        // 2. Adjust Camera Position to match crouch
        if (cameraTransform != null)
        {
            float crouchCameraOffset = standingHeight - crouchHeight;
            float targetCameraY = isCrouched ? standingCameraHeight - crouchCameraOffset : standingCameraHeight;
            
            Vector3 camLocalPos = cameraTransform.localPosition;
            camLocalPos.y = Mathf.Lerp(camLocalPos.y, targetCameraY, crouchTransitionSpeed * Time.deltaTime);
            cameraTransform.localPosition = camLocalPos;
        }
    }

    private void UpdateHandState()
    {
        if (playerHand == null) return;

        bool hasMoveInput = moveInput.sqrMagnitude > 0.0001f;
        bool isRunning = !isCrouched && hasMoveInput && sprintAction != null && sprintAction.IsPressed();

        playerHand.SetMovementState(isRunning, isCrouched);
    }

    private void OnJumpPerformed(InputAction.CallbackContext context) => jumpRequested = true;

    private void OnCrouchPerformed(InputAction.CallbackContext context)
    {
        if (toggleCrouch)
        {
            isCrouched = !isCrouched;
            return;
        }
        isCrouched = true;
    }

    private void OnCrouchCanceled(InputAction.CallbackContext context)
    {
        if (!toggleCrouch) isCrouched = false;
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        playerHand?.TryTriggerInteract();
    }
}