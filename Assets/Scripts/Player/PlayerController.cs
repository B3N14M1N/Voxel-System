using UnityEngine;

public enum PlayerControllerState
{
    FPSMode = 0,
    TPSMode = 1,
}

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public bool controlling = true;
    [Header("Setting Fields")]
    private CharacterController movementController;
    private Animator animator;

    [SerializeField]
    public PlayerControllerState State;
    public bool stateChanged = false;
    [SerializeField]
    public bool Fly = true;
    public GameObject mesh;

    #region CAMERA CONTROLS
    [Header("Camera Controls")]
    [SerializeField]
    public float MinX = -90f;
    [SerializeField]
    public float MaxX = 90f;
    [SerializeField]
    public Vector2Int minMaxZoom = new Vector2Int(-20, -1);
    private float zoom = -10f;
    [SerializeField]
    public float LookSensitivity = 1f;
    [SerializeField]
    public float ZoomSensitivity = 1f;
    [SerializeField]
    public Transform cameraPivot;
    [SerializeField]
    public Transform cameraTransform;
    [SerializeField]
    public Vector2 cameraYsmoothFollowHeigth = new Vector2(-1f, 1f);
    public float cameraYfollowSensitivity = 1f;

    private float yRot;
    private float xRot;
    #endregion

    #region MOVEMENT CONTROLS
    [Header("Movement Settings")]
    [SerializeField]
    public float fastSpeedMultiplier = 1.5f;
    public float slowSpeedMultiplier = 0.5f;
    public float multiplierGain = 1f;
    public float JumpPower = 10f;
    public float gravityMultiplier = 1.5f;

    public float multiplier = 1f;
    private Vector3 velocity;

    [Header("Walk")]
    public float walkBaseSpeed = 5f;
    [Header("Fly")]
    [SerializeField]
    public float flyBaseSpeed = 100f;
    #endregion





    public void Start()
    {
        movementController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        if(cameraPivot == null )
        {
            cameraTransform = Camera.main.transform;
            cameraPivot = cameraTransform.GetComponentInParent<Transform>();
        }
    }

    public void Update()
    {
        if (controlling)
        {
            if (Input.GetKeyDown(KeyCode.C))
                ChangeStateFields();

            if (Input.GetKeyDown(KeyCode.F))
                Fly = !Fly;

            Movement();
            CameraMovement();
        }
    }
    private void ChangeStateFields()
    {
        if (State == PlayerControllerState.TPSMode)
            State = PlayerControllerState.FPSMode;
        else State = PlayerControllerState.TPSMode;

        stateChanged = true;

        velocity = Vector3.zero;

        transform.eulerAngles = new Vector3(0.0f, yRot, 0.0f);

        if (State == PlayerControllerState.FPSMode)
        {
            cameraPivot.eulerAngles = new Vector3(transform.eulerAngles.x, yRot, 0.0f);
            cameraTransform.localPosition = Vector3.zero;
            mesh.SetActive(false);
        }
        if(State == PlayerControllerState.TPSMode)
        {
            cameraPivot.eulerAngles = new Vector3(transform.eulerAngles.x, 0.0f, 0.0f);
            cameraTransform.localPosition = new Vector3(0, 0, zoom);
            mesh.SetActive(true);
        }
        stateChanged = false;
    }
    private void Movement()
    {
        var direction = GetBaseInput().normalized;

        if (State == PlayerControllerState.TPSMode)
        {
            if (direction != Vector3.zero)
            {
                Vector3 newDirection = cameraPivot.TransformPoint(direction) - cameraPivot.position;
                newDirection.y = 0;
                direction = newDirection;
                transform.forward = direction;
            }
        }
        else
        {
            direction = transform.forward * direction.z + transform.right * direction.x;
        }
        var baseSpeed = Fly ? flyBaseSpeed : walkBaseSpeed;
        bool normalSeed = true;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            multiplier = Mathf.MoveTowards(multiplier, fastSpeedMultiplier, multiplierGain * Time.deltaTime);
            normalSeed = false;
        }
        if(Input.GetKey(KeyCode.LeftAlt))
        {
            multiplier = slowSpeedMultiplier;
            normalSeed = false;
        }
        if(normalSeed)
            multiplier = 1f;

        animator.SetBool("Speed", direction != Vector3.zero);
        animator.SetFloat("SpeedMultiplier", multiplier);

        movementController.Move(baseSpeed * multiplier * Time.deltaTime * direction);

        if (!Fly)
        {
            if (movementController.isGrounded)
            {
                velocity = Vector3.zero;
                if (Input.GetKey(KeyCode.Space))
                {
                    velocity.y = JumpPower;
                }
            }
            else
            {
                velocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
            }
            movementController.Move(velocity * Time.deltaTime);
        }
        else
        {
            velocity = Vector3.zero;
            if(Input.GetKey(KeyCode.Space))
            {
                movementController.Move(JumpPower * multiplier * gravityMultiplier * Time.deltaTime * Vector3.up);
            }
            if (Input.GetKey(KeyCode.LeftControl))
            {
                movementController.Move(-JumpPower * multiplier * gravityMultiplier * Time.deltaTime * Vector3.up);
            }
        }
    }

    private void CameraMovement()
    {
        // Camera Look

        if(State == PlayerControllerState.FPSMode || (State == PlayerControllerState.TPSMode && Input.GetMouseButton(1)))
        {
            yRot += Input.GetAxisRaw("Mouse X") * LookSensitivity;
            xRot -= Input.GetAxisRaw("Mouse Y") * LookSensitivity;

            yRot = ClampAngle(yRot, -360, 360);
            xRot = ClampAngle(xRot, MinX, MaxX);
        }
        cameraPivot.eulerAngles = new Vector3(xRot, yRot, 0.0f);

        if(State == PlayerControllerState.FPSMode)
        {
            transform.eulerAngles = new Vector3(0.0f, yRot, 0.0f);
            cameraPivot.transform.position = transform.position + movementController.center.y * movementController.height * Vector3.up;
        }

        if(State == PlayerControllerState.TPSMode)
        {
            var targetHeigth = transform.position.y + movementController.center.y * movementController.height;
            var currentheigth = Mathf.Clamp(cameraPivot.transform.position.y, targetHeigth + cameraYsmoothFollowHeigth.x, targetHeigth + cameraYsmoothFollowHeigth.y);
            cameraPivot.transform.position = new Vector3(transform.position.x, Mathf.MoveTowards(currentheigth, targetHeigth, cameraYfollowSensitivity * Time.deltaTime), transform.position.z);
            zoom += Input.mouseScrollDelta.y * ZoomSensitivity;
            zoom = Mathf.Clamp(zoom, minMaxZoom.x, minMaxZoom.y);
            cameraTransform.localPosition = new Vector3(0f, 0f, zoom);
        }
    }
    protected float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360)
            angle += 360;
        if (angle > 360)
            angle -= 360;

        return Mathf.Clamp(angle, min, max);
    }
    private Vector3 GetBaseInput()
    {
        Vector3 p_Velocity = new Vector3();
        if (Input.GetKey(KeyCode.W))
        {
            p_Velocity += new Vector3(0, 0, 1);
        }
        if (Input.GetKey(KeyCode.S))
        {
            p_Velocity += new Vector3(0, 0, -1);
        }
        if (Input.GetKey(KeyCode.A))
        {
            p_Velocity += new Vector3(-1, 0, 0);
        }
        if (Input.GetKey(KeyCode.D))
        {
            p_Velocity += new Vector3(1, 0, 0);
        }
        return p_Velocity;
    }

}