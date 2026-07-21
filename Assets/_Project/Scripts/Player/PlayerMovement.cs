using UnityEngine;

namespace _Project.Scripts.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraHolder;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 6.5f;
        [SerializeField] private float sprintSpeed = 15f;
        [SerializeField] private float speedChangeRate = 30f;

        [Header("Jumping / Gravity")]
        [SerializeField] private float jumpHeight = 2.0f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float terminalVelocity = -42f;
        [SerializeField] private float fallMultiplier = 2.0f; // extra gravity while falling

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float bottomClamp = -80f;
        [SerializeField] private float topClamp = 80f;
        
        [Header("Camera FOV")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float baseFov = 60f;
        [SerializeField] private float sprintFov = 72f;
        [SerializeField] private float fovChangeSpeed = 8f;
        
        [Header("Camera Bob")]
        [SerializeField] private float bobFrequency = 10f;
        [SerializeField] private float walkBobAmount = 0.05f;
        [SerializeField] private float sprintBobAmount = 0.1f;
        
        [Header("Landing Dip")]
        [SerializeField] private float landingDipAmount = 0.15f;
        [SerializeField] private float landingDipRecoverySpeed = 10f;

        private bool _wasGroundedLastFrame = true;
        private float _landingDipOffset;
        private float _landingDipVelocity;

        private float _bobTimer;
        private Vector3 _cameraHolderStartLocalPos;

        private CharacterController _controller;
        private PlayerControls _controls;

        private Vector3 _velocity;
        private float _currentSpeed;
        private float _pitch;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _controls = new PlayerControls();
            
            _cameraHolderStartLocalPos = cameraHolder.localPosition;
        }

        private void OnEnable()
        {
            _controls.Enable();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnDisable()
        {
            _controls.Disable();
        }

        private void Update()
        {
            HandleLook();
            HandleMovement();
        }

        private void HandleLook()
        {
            Vector2 lookInput = _controls.Player.Look.ReadValue<Vector2>();

            // Yaw rotates the whole player body left/right
            transform.Rotate(Vector3.up * (lookInput.x * mouseSensitivity));

            // Pitch rotates only the camera holder up/down
            _pitch -= lookInput.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, bottomClamp, topClamp);
            cameraHolder.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            Vector2 moveInput = _controls.Player.Move.ReadValue<Vector2>();
            bool isSprinting = _controls.Player.Sprint.IsPressed();
            bool jumpPressed = _controls.Player.Jump.WasPressedThisFrame();
            
            bool isActuallyMoving = moveInput != Vector2.zero;
            float targetFov = (isSprinting && isActuallyMoving) ? sprintFov : baseFov;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, fovChangeSpeed * Time.deltaTime); 

            float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
            if (moveInput == Vector2.zero) targetSpeed = 0f;

            // Smoothly accelerate/decelerate toward target speed instead of snapping
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * speedChangeRate);

            Vector3 moveDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
            Vector3 horizontalMove = moveDirection * _currentSpeed;
            
            bool isGroundedNow = _controller.isGrounded;
            if (isGroundedNow && !_wasGroundedLastFrame)
            {
                // Just landed — trigger dip proportional to how fast we were falling
                float fallSpeedFactor = Mathf.Clamp01(Mathf.Abs(_velocity.y) / Mathf.Abs(terminalVelocity));
                _landingDipOffset = -landingDipAmount * Mathf.Lerp(0.4f, 1f, fallSpeedFactor);
            }

            // Ground check via CharacterController's built-in flag
            if (isGroundedNow)
            {
                if (_velocity.y < 0f)
                    _velocity.y = -2f; // small downward force to keep controller grounded

                if (jumpPressed)
                {
                    _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            float appliedGravity = _velocity.y < 0f ? gravity * fallMultiplier : gravity;
            _velocity.y += appliedGravity * Time.deltaTime;
            _velocity.y = Mathf.Max(_velocity.y, terminalVelocity);

            Vector3 finalMove = horizontalMove + Vector3.up * _velocity.y;
            _controller.Move(finalMove * Time.deltaTime);
            
            HandleCameraBob(isActuallyMoving, isSprinting, isGroundedNow);
            
            _landingDipOffset = Mathf.SmoothDamp(_landingDipOffset, 0f, ref _landingDipVelocity, 1f / landingDipRecoverySpeed);
            cameraHolder.localPosition += Vector3.up * _landingDipOffset;

            _wasGroundedLastFrame = isGroundedNow;
        }
        
        private void HandleCameraBob(bool isMoving, bool isSprinting, bool isGrounded)
        {
            if (isMoving && isGrounded)
            {
                float bobAmount = isSprinting ? sprintBobAmount : walkBobAmount;
                _bobTimer += Time.deltaTime * bobFrequency;

                float bobOffsetY = Mathf.Sin(_bobTimer) * bobAmount;
                float bobOffsetX = Mathf.Cos(_bobTimer * 0.5f) * bobAmount * 0.5f;

                cameraHolder.localPosition = _cameraHolderStartLocalPos + new Vector3(bobOffsetX, bobOffsetY, 0f);
            }
            else
            {
                _bobTimer = 0f;
                cameraHolder.localPosition = Vector3.Lerp(cameraHolder.localPosition, _cameraHolderStartLocalPos, Time.deltaTime * 8f);
            }
        }
    }
}