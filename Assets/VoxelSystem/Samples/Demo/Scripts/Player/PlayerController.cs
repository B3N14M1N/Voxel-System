using UnityEngine;
using Zenject;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Inject] private readonly PlayerStateManager _stateManager;

    [Header("Player Settings")]
    [SerializeField] private GameObject _characterMesh;
    [SerializeField] private GameObject _fpsDot;
    [SerializeField] private KeyCode _toggleViewKey = KeyCode.C;
    [SerializeField] private KeyCode _toggleFlyKey = KeyCode.F;
    [field: SerializeField] public KeyCode JumpKey { get; private set; } = KeyCode.Space;
    [field: SerializeField] public KeyCode CrouchKey { get; private set; } = KeyCode.LeftControl;

    private CharacterController _characterController;
    private Animator _animator;
    private Vector3 _velocity;
    private float _speedMultiplier = 1f;
    private float _zoom = -10f;
    private float _previousZoom;

    [Header("Camera Settings")]
    [SerializeField] private Transform _cameraPivot;
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private float _lookSensitivity = 1f;
    [SerializeField] private float _zoomSensitivity = 1f;
    [SerializeField] private Vector2 _zoomLimits = new Vector2(-200, -1);
    [SerializeField] private Vector2 _xRotationLimits = new Vector2(-89f, 89f);
    [SerializeField] private Vector2 _cameraYOffset = new Vector2(-1f, 1f);
    [SerializeField] private float _cameraFollowSpeed = 1f;

    private float _yRotation;
    private float _xRotation;

    [Header("Movement Settings")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _flySpeed = 200f;
    [SerializeField] private float _runMultiplier = 1.5f;
    [SerializeField] private float _crouchMultiplier = 0.5f;
    [SerializeField] private float _jumpPower = 7.5f;
    [SerializeField] private float _gravityMultiplier = 2f;
    [SerializeField] private float _groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask _groundLayer = new() { value = 1 << 0 };
    
    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponentInChildren<Animator>();

        if (_cameraPivot == null)
        {
            _cameraTransform = Camera.main.transform;
            _cameraPivot = _cameraTransform.parent;
        }
        
        // Update UI based on initial state
        UpdateViewState(_stateManager.ViewState);
        
        // Subscribe to state changes
        _stateManager.OnViewStateChanged += UpdateViewState;
    }
    
    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (_stateManager != null)
        {
            _stateManager.OnViewStateChanged -= UpdateViewState;
        }
    }

    private void Update()
    {
        if (!_stateManager.CanControl) return;

        HandleInput();
        HandleMovement();
        HandleCamera();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(_toggleViewKey)) _stateManager.ToggleViewState();
        if (Input.GetKeyDown(_toggleFlyKey)) _stateManager.ToggleFlyState();
    }

    private void HandleMovement()
    {
        Vector3 direction = GetMovementInput().normalized;
        if (_stateManager.ViewState == PlayerState.TPS && direction != Vector3.zero)
        {
            direction = AdjustDirectionToCamera(direction);
            transform.forward = direction;
        }
        else
        {
            direction = transform.right * direction.x + transform.forward * direction.z;
        }

        AdjustSpeedMultiplier();
        _animator.SetBool("Speed", direction != Vector3.zero);
        _animator.SetFloat("SpeedMultiplier", _speedMultiplier);

        float moveSpeed = _stateManager.CanFly ? _flySpeed : _walkSpeed;
        _characterController.Move(moveSpeed * _speedMultiplier * Time.deltaTime * direction);

        if (!_stateManager.CanFly) ApplyGravity();
        else HandleFlight();
    }
    
    private void ApplyGravity()
    {
        Vector3 groundCheckOrigin = transform.position + Vector3.up * 0.1f; // Start slightly above the feet
        bool isGrounded = _characterController.isGrounded || Physics.Raycast(groundCheckOrigin, Vector3.down, _groundCheckDistance, _groundLayer);

        if (isGrounded)
        {
            if (_velocity.y < 0) _velocity.y = -2f; // Small downward force to keep grounded
            if (Input.GetKey(JumpKey)) _velocity.y = _jumpPower;
        }
        else _velocity.y += Physics.gravity.y * _gravityMultiplier * Time.deltaTime;

        _characterController.Move(_velocity * Time.deltaTime);
    }

    private void HandleFlight()
    {
        _velocity = Vector3.zero;
        if (Input.GetKey(JumpKey)) _characterController.Move(_jumpPower * Time.deltaTime * Vector3.up);
        if (Input.GetKey(CrouchKey)) _characterController.Move(_jumpPower * Time.deltaTime * Vector3.down);
    }

    private void HandleCamera()
    {
        if (_stateManager.ViewState == PlayerState.FPS || (_stateManager.ViewState == PlayerState.TPS && Input.GetMouseButton(1)))
        {
            _yRotation += Input.GetAxisRaw("Mouse X") * _lookSensitivity;
            _xRotation -= Input.GetAxisRaw("Mouse Y") * _lookSensitivity;
            _xRotation = Mathf.Clamp(_xRotation, _xRotationLimits.x, _xRotationLimits.y);
        }
        _cameraPivot.rotation = Quaternion.Euler(_xRotation, _yRotation, 0);

        if (_stateManager.ViewState == PlayerState.FPS)
        {
            transform.rotation = Quaternion.Euler(0, _yRotation, 0);
            _cameraPivot.position = transform.position + 0.9f * _characterController.height * Vector3.up;
            _cameraTransform.localPosition = new Vector3(0, 0, 0); // Reset _zoom
        }
        else if (_stateManager.ViewState == PlayerState.TPS)
        {
            float targetHeight = transform.position.y + _characterController.height * 0.5f;
            float smoothHeight = Mathf.Clamp(_cameraPivot.position.y, targetHeight + _cameraYOffset.x, targetHeight + _cameraYOffset.y);
            _cameraPivot.position = new Vector3(transform.position.x, Mathf.MoveTowards(smoothHeight, targetHeight, _cameraFollowSpeed * Time.deltaTime), transform.position.z);
            _zoom = Mathf.Clamp(_zoom + Input.mouseScrollDelta.y * _zoomSensitivity, _zoomLimits.x, _zoomLimits.y);
            _cameraTransform.localPosition = new Vector3(0, 0, _zoom);
        }
    }
    
    private Vector3 GetMovementInput()
    {
        Vector3 input = Vector3.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input += Vector3.forward;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input += Vector3.back;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input += Vector3.left;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input += Vector3.right;
        return input;
    }

    private Vector3 AdjustDirectionToCamera(Vector3 direction)
    {
        Vector3 adjustedDirection = _cameraPivot.TransformDirection(direction);
        adjustedDirection.y = 0;
        return adjustedDirection.normalized;
    }

    private void AdjustSpeedMultiplier()
    {
        if (Input.GetKey(KeyCode.LeftShift)) _speedMultiplier = _runMultiplier;
        else if (Input.GetKey(KeyCode.LeftAlt)) _speedMultiplier = _crouchMultiplier;
        else _speedMultiplier = 1f;
    }

    private void UpdateViewState(PlayerState state)
    {
        if (state == PlayerState.TPS)
        {
            _zoom = _previousZoom;
            ToggleCursor(false); // Unlock cursor in TPS mode
        }
        else
        {
            _previousZoom = _zoom;
            _zoom = 0;
            ToggleCursor(true); // Lock cursor in FPS mode
        }
        
        _characterMesh.SetActive(state == PlayerState.TPS);
        if (_fpsDot != null) _fpsDot.SetActive(state == PlayerState.FPS);
    }

    private void ToggleCursor(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }
}
