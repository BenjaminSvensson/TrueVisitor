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

    [Header("Camera Feel")]
    [SerializeField] private float lookSmooth = 18f;
    [SerializeField] private float cameraShakeAmount = 0.05f;
    [SerializeField] private float cameraShakeSpeed = 2f;
    [SerializeField] private float cameraSwayAmount = 0.2f;
    [SerializeField] private float cameraSwaySmooth = 12f;

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

    private float targetYaw;
    private float targetPitch;

    private float shakeTime;
    private Vector2 sway;

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
            targetPitch = pitch;
        }

        if (playerHand == null)
            playerHand = GetComponentInChildren<PlayerHand>();

        yaw = transform.eulerAngles.y;
        targetYaw = yaw;

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

        if (interactAction != null)
            interactAction.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        if (jumpAction != null) jumpAction.performed -= OnJumpPerformed;

        if (crouchAction != null)
        {
            crouchAction.performed -= OnCrouchPerformed;
            crouchAction.canceled -= OnCrouchCanceled;
        }

        if (interactAction != null)
            interactAction.performed -= OnInteractPerformed;

        playerMap?.Disable();
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

    private void HandleLook()
    {
        if (lookAction == null) return;

        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        targetYaw += lookInput.x * lookSensitivity;

        float yDelta = lookInput.y * lookSensitivity;
        targetPitch += invertY ? yDelta : -yDelta;
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        yaw = Mathf.Lerp(yaw, targetYaw, lookSmooth * Time.deltaTime);
        pitch = Mathf.Lerp(pitch, targetPitch, lookSmooth * Time.deltaTime);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cameraTransform == null) return;

        sway = Vector2.Lerp(
            sway,
            lookInput * cameraSwayAmount,
            cameraSwaySmooth * Time.deltaTime
        );

        shakeTime += Time.deltaTime * cameraShakeSpeed;

        float shakeX =
            (Mathf.PerlinNoise(shakeTime, 0f) - 0.5f)
            * cameraShakeAmount;

        float shakeY =
            (Mathf.PerlinNoise(0f, shakeTime) - 0.5f)
            * cameraShakeAmount;

        float finalPitch = pitch + sway.y + shakeY;
        float finalYaw = sway.x + shakeX;

        cameraTransform.localRotation =
            Quaternion.Euler(finalPitch, finalYaw, 0f);
    }

    private void UpdateMovementInput()
    {
        if (moveAction != null)
            moveInput = moveAction.ReadValue<Vector2>();
    }

    private void HandleJumpAndGravity()
    {
        bool grounded = characterController.isGrounded;

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (jumpRequested && grounded && !isCrouched)
        {
            verticalVelocity =
                Mathf.Sqrt(jumpHeight * -2f * gravity);

            playerHand?.TriggerJump();
        }

        jumpRequested = false;

        verticalVelocity += gravity * Time.deltaTime;
    }

    private void MoveCharacter()
    {
        Vector3 forward = moveOrientation.forward;
        Vector3 right = moveOrientation.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 move =
            right * moveInput.x +
            forward * moveInput.y;

        if (move.sqrMagnitude > 1)
            move.Normalize();

        float speed = GetSpeed(move);

        Vector3 velocity =
            move * speed +
            Vector3.up * verticalVelocity;

        characterController.Move(
            velocity * Time.deltaTime
        );
    }

    private float GetSpeed(Vector3 move)
    {
        if (isCrouched) return crouchSpeed;

        bool moving = move.sqrMagnitude > 0.01f;

        bool sprint =
            sprintAction != null &&
            sprintAction.IsPressed() &&
            moving;

        return sprint ? runSpeed : walkSpeed;
    }

    private void UpdateCrouchState()
    {
        float targetHeight =
            isCrouched ? crouchHeight : standingHeight;

        float newHeight =
            Mathf.Lerp(
                characterController.height,
                targetHeight,
                crouchTransitionSpeed * Time.deltaTime
            );

        characterController.height = newHeight;

        float centerOffset =
            (newHeight - standingHeight) * 0.5f;

        characterController.center =
            new Vector3(
                standingCenter.x,
                standingCenter.y + centerOffset,
                standingCenter.z
            );

        if (cameraTransform != null)
        {
            float offset =
                standingHeight - crouchHeight;

            float targetY =
                isCrouched
                ? standingCameraHeight - offset
                : standingCameraHeight;

            Vector3 pos =
                cameraTransform.localPosition;

            pos.y =
                Mathf.Lerp(
                    pos.y,
                    targetY,
                    crouchTransitionSpeed * Time.deltaTime
                );

            cameraTransform.localPosition = pos;
        }
    }

    private void UpdateHandState()
    {
        if (playerHand == null) return;

        bool moving =
            moveInput.sqrMagnitude > 0.01f;

        bool running =
            !isCrouched &&
            moving &&
            sprintAction != null &&
            sprintAction.IsPressed();

        playerHand.SetMovementState(
            running,
            isCrouched
        );
    }

    private void ResolveActions()
    {
        playerMap =
            inputActionsAsset.FindActionMap("Player", true);

        moveAction =
            playerMap.FindAction("Move", true);

        lookAction =
            playerMap.FindAction("Look", true);

        sprintAction =
            playerMap.FindAction("Sprint", false);

        jumpAction =
            playerMap.FindAction("Jump", true);

        crouchAction =
            playerMap.FindAction("Crouch", true);

        interactAction =
            playerMap.FindAction("Interact", true);
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        jumpRequested = true;
    }

    private void OnCrouchPerformed(InputAction.CallbackContext ctx)
    {
        if (toggleCrouch)
            isCrouched = !isCrouched;
        else
            isCrouched = true;
    }

    private void OnCrouchCanceled(InputAction.CallbackContext ctx)
    {
        if (!toggleCrouch)
            isCrouched = false;
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        playerHand?.TryTriggerInteract();
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}