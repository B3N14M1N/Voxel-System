using System; // Provides Action event type
using UnityEngine; // Provides MonoBehaviour, Camera, GameObject, etc.
using UnityEngine.EventSystems; // Provides EventSystem for UI interaction checks
using VoxelSystem.Data; // Provides Voxel struct/class (assumption)
using VoxelSystem.Data.Blocks; // Provides BlocksCatalogue, VoxelType (assumption)
using VoxelSystem.Managers; // Provides IChunksManager
using Zenject; // Provides dependency injection ([Inject])

/// <summary>
/// Manages placing and removing voxel blocks in the world based on player input.
/// Handles raycasting, preview display, state changes, and interaction with the ChunksManager.
/// </summary>
public class VoxelBlockManager : MonoBehaviour
{
    #region Dependencies and Configuration

    [Header("Dependencies")]
    [Tooltip("Reference to the main camera used for raycasting.")]
    [SerializeField] private Camera mainCamera; // Made private serialized field, assign in Inspector

    [Tooltip("Prefab used for the placement/removal preview.")]
    [SerializeField] private GameObject blockPrefab; // Made private serialized field

    [Tooltip("Layer mask containing the voxel terrain geometry for raycasting.")]
    [SerializeField] private LayerMask terrainLayer; // Made private serialized field

    // Injected dependencies
    [Inject] private readonly BlocksCatalogue _blocksCatalogue;
    [Inject] private readonly IChunksManager _chunksManager;

    #endregion

    #region Public Events

    /// <summary>
    /// Event invoked when the selected block type changes (including selecting removal or idle).
    /// </summary>
    public event Action OnSelected;

    #endregion

    #region Private State

    /// <summary>
    /// Represents the current action the manager is set up to perform.
    /// </summary>
    private enum PlacementState
    {
        None,    // Not actively placing or removing
        Placing, // Ready to place the selected block type
        Removing // Ready to remove a block
    }

    /// <summary>
    /// The current state of the placement manager.
    /// </summary>
    private PlacementState _currentState = PlacementState.None;

    /// <summary>
    /// The type of voxel currently selected for placement. Ignored when removing.
    /// </summary>
    private VoxelType _blockType; // Assuming VoxelType is an enum or similar identifier

    /// <summary>
    /// Instantiated preview object shown at the target location.
    /// </summary>
    private GameObject _previewObject;

    /// <summary>
    /// Renderer component of the preview object, cached for efficiency.
    /// </summary>
    private MeshRenderer _previewRenderer;

    // Cached shader property IDs for performance
    private int _placingShaderID;
    private int _removingShaderID;
    private int _renderBlockShaderID;
    private int _blockIndexShaderID;

    #endregion

    #region Unity Lifecycle Methods

    /// <summary>
    /// Called when the script instance is being loaded.
    /// Initializes the preview object and caches shader property IDs.
    /// </summary>
    private void Awake()
    {
        // Validate essential references assigned in the Inspector
        if (mainCamera == null)
        {
            Debug.LogError("VoxelBlockManager: Main Camera reference not set!", this);
            enabled = false; // Disable script if camera is missing
            return;
        }
        if (blockPrefab == null)
        {
            Debug.LogError("VoxelBlockManager: Block Prefab reference not set!", this);
            // Preview functionality will be disabled, but core logic might still work if needed.
        }
        else if (_previewObject == null) // Instantiate preview object if prefab is set and not already instantiated
        {
            _previewObject = Instantiate(blockPrefab);
            _previewObject.SetActive(false); // Start hidden
            // Cache the renderer component
            _previewObject.TryGetComponent<MeshRenderer>(out _previewRenderer);
            if (_previewRenderer == null)
            {
                Debug.LogWarning("VoxelBlockManager: Preview Prefab does not have a MeshRenderer component.", this);
            }
        }

        // Cache shader property IDs to avoid string lookups every frame/state change
        _placingShaderID = Shader.PropertyToID("_Placing");
        _removingShaderID = Shader.PropertyToID("_Removing");
        _renderBlockShaderID = Shader.PropertyToID("_Render_Block");
        _blockIndexShaderID = Shader.PropertyToID("_Block_Index");

        // Ensure initial state is idle
        SetIdleState();
    }

    /// <summary>
    /// Called every frame.
    /// Handles raycasting, preview updates, and input detection for placing/removing blocks.
    /// </summary>
    private void Update()
    {
        // Hide preview by default each frame; ShowPreview will activate it if needed.
        HidePreview();

        // Ignore if idle, or if the mouse pointer is over a UI element.
        if (_currentState == PlacementState.None || EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Cast a ray from the camera through the mouse position.
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // Perform the raycast against the specified terrain layer.
        // Also includes an original check `hit.normal.y < 1.0f` which might prevent placement on sheer vertical faces or overhangs?
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, terrainLayer) || hit.normal.y < 1.0f)
        {
            // No valid hit point found, exit.
            return;
        }

        // Calculate the target voxel position based on the hit point.
        Vector3 targetPosition = GetSnappedPosition(hit.point, hit.normal);

        // Show the preview object at the calculated position.
        ShowPreview(targetPosition);

        // Check for the primary mouse button click to perform the action.
        if (!Input.GetMouseButtonDown(0)) // Button 0 is typically the left mouse button
        {
            return; // No click detected this frame.
        }

        // Perform placing or removing based on the current state.
        if (_currentState == PlacementState.Placing)
        {
            PlaceBlock(targetPosition);
        }
        else if (_currentState == PlacementState.Removing)
        {
            RemoveBlock(targetPosition);
        }
    }

    /// <summary>
    /// Called when the GameObject is being destroyed.
    /// Cleans up the instantiated preview object.
    /// </summary>
    private void OnDestroy()
    {
        // Destroy the preview object if it exists to prevent memory leaks.
        if (_previewObject != null)
        {
            Destroy(_previewObject);
            _previewObject = null; // Clear reference
            _previewRenderer = null;
        }
    }

    #endregion

    #region Public State Control Methods

    /// <summary>
    /// Sets the manager state to 'Placing' using the provided block information.
    /// Updates the preview material.
    /// </summary>
    /// <param name="block">The BlockSelector providing the VoxelType to place.</param>
    public void SetPlacingState(BlockSelector block) // Assuming BlockSelector has a VoxelType property named 'Type'
    {
        if (block == null) return;

        _blockType = block.Type;
        OnSelected?.Invoke(); // Notify listeners (e.g., UI)
        block.SetSelectedState(); // Update the visual state of the selector

        // If the selected type is 'air' or equivalent, switch to removing state.
        if (_blockType == VoxelType.air) // Replace VoxelType.air with your actual representation of air/empty
        {
            SetRemovingState();
            return;
        }

        // Set the state to Placing.
        _currentState = PlacementState.Placing;

        // Update preview material for placing state.
        UpdatePreviewMaterial();
    }

    /// <summary>
    /// Sets the manager state to 'Removing'.
    /// Updates the preview material.
    /// </summary>
    public void SetRemovingState()
    {
        _currentState = PlacementState.Removing;
        _blockType = VoxelType.air; // Ensure block type reflects removal intention

        // Update preview material for removing state.
        UpdatePreviewMaterial();
    }

    /// <summary>
    /// Sets the manager state to 'None' (idle).
    /// Hides the preview and resets the preview material.
    /// </summary>
    public void SetIdleState()
    {
        _currentState = PlacementState.None;
        _blockType = VoxelType.air; // Reset selected type
        OnSelected?.Invoke(); // Notify listeners

        // Update preview material for idle state.
        UpdatePreviewMaterial();
        HidePreview(); // Ensure preview is hidden when idle
    }

    #endregion

    #region Core Logic Methods (Placing/Removing/Snapping)

    /// <summary>
    /// Calculates the integer grid position for the target voxel based on raycast hit.
    /// Adjusts position based on hit point and normal for placing/removing.
    /// </summary>
    /// <param name="hitPos">The precise point where the raycast hit the terrain.</param>
    /// <param name="hitNormal">The normal vector of the surface hit by the raycast.</param>
    /// <returns>The calculated integer coordinates of the target voxel.</returns>
    private Vector3 GetSnappedPosition(Vector3 hitPos, Vector3 hitNormal)
    {
        Vector3 targetPos;

        if (_currentState == PlacementState.Placing)
        {
            // For placing, offset slightly *away* from the surface normal before flooring.
            // This typically places the block adjacent to the hit surface.
            targetPos = hitPos + hitNormal * 0.5f;
        }
        else // Removing
        {
            // For removing, offset slightly *into* the surface normal before flooring.
            // This targets the block that was hit.
            targetPos = hitPos - hitNormal * 0.5f;
            // Original logic also included: hitPos += Vector3.down * .5f; - Keep if needed, but normal-based offset is more common.
            // Let's use the normal-based offset for consistency unless the Vector3.down logic was critical.
        }

        // Floor the coordinates to get the integer grid position of the voxel.
        // Add a small epsilon to Y when flooring to handle floating point inaccuracies near integer boundaries.
        Vector3 snapped = new Vector3(
            Mathf.FloorToInt(targetPos.x),
            Mathf.FloorToInt(targetPos.y + 0.001f),
            Mathf.FloorToInt(targetPos.z)
        );

        // Debug.LogWarning($"[VoxelBlockManager] Hit: {hitPos:F3}, Normal: {hitNormal:F3}, Target: {targetPos:F3}, Snapped: {snapped}");

        return snapped;
    }

    /// <summary>
    /// Attempts to place the currently selected block at the target position using the ChunksManager.
    /// </summary>
    /// <param name="position">The integer world coordinates where the block should be placed.</param>
    private void PlaceBlock(Vector3 position)
    {
        // Ensure we are actually in placing state and have a valid block type selected.
        if (_currentState != PlacementState.Placing || _blockType == VoxelType.air) // Replace VoxelType.air check if needed
        {
            return;
        }

        // Create the Voxel data structure for the selected block type.
        // ** Adapt this line based on your actual Voxel struct/class definition **
        Voxel voxelToPlace = new Voxel { ID = (byte)_blockType }; // Example assumption

        // Call the ChunksManager API to modify the voxel.
        bool success = _chunksManager.ModifyVoxel(position, voxelToPlace);

        if (success)
        {
            // Optional: Add sound effects, particle effects, etc.
            Debug.Log($"Placed block of type {_blockType} at {position}");
        }
        else
        {
            // Optional: Provide feedback if placement failed (e.g., trying to place in air, blocked by other logic)
            Debug.LogWarning($"Failed to place block of type {_blockType} at {position}. ModifyVoxel returned false.");
        }
    }

    /// <summary>
    /// Attempts to remove the block at the target position using the ChunksManager.
    /// </summary>
    /// <param name="position">The integer world coordinates where the block should be removed.</param>
    private void RemoveBlock(Vector3 position)
    {
        // Ensure we are actually in removing state.
        if (_currentState != PlacementState.Removing)
        {
            return;
        }

        // Get the representation for an empty voxel.
        // ** Adapt this line based on your actual Voxel struct/class definition **
        Voxel emptyVoxel = Voxel.Empty; // Example assumption (assuming a static Voxel.Empty exists)
        // Or: Voxel emptyVoxel = new Voxel { ID = 0 };

        // Call the ChunksManager API to modify the voxel (setting it to empty).
        bool success = _chunksManager.ModifyVoxel(position, emptyVoxel);

        if (success)
        {
            // Optional: Add sound effects, particle effects, etc.
            Debug.Log($"Removed block at {position}");
        }
        else
        {
            // Optional: Provide feedback if removal failed (e.g., trying to remove bedrock, out of bounds)
            Debug.LogWarning($"Failed to remove block at {position}. ModifyVoxel returned false.");
        }
    }

    #endregion

    #region Preview Object Handling

    /// <summary>
    /// Shows the preview object at the specified position.
    /// </summary>
    /// <param name="position">The world position to place the preview object.</param>
    private void ShowPreview(Vector3 position)
    {
        if (_previewObject == null) return;

        _previewObject.transform.position = position;
        _previewObject.SetActive(true);
    }

    /// <summary>
    /// Hides the preview object.
    /// </summary>
    private void HidePreview()
    {
        if (_previewObject == null) return;

        _previewObject.SetActive(false);
    }

    /// <summary>
    /// Updates the material properties of the preview object based on the current state (_currentState, _blockType).
    /// Uses cached shader property IDs for efficiency.
    /// </summary>
    private void UpdatePreviewMaterial()
    {
        // Ensure preview object and renderer exist.
        if (_previewRenderer == null || _previewRenderer.sharedMaterial == null) return;

        Material previewMat = _previewRenderer.sharedMaterial; // Use sharedMaterial if the preview doesn't need unique material instances

        // Reset all state flags initially
        previewMat.SetFloat(_placingShaderID, 0f);
        previewMat.SetFloat(_removingShaderID, 0f);
        previewMat.SetFloat(_renderBlockShaderID, 0f);
        previewMat.SetFloat(_blockIndexShaderID, 0f); // Default to 0 or an invalid index

        // Apply state-specific flags
        switch (_currentState)
        {
            case PlacementState.Placing:
                previewMat.SetFloat(_placingShaderID, 1f);
                // Try to get the texture index for the selected block type
                if (_blocksCatalogue != null && _blocksCatalogue.TextureMapping.TryGetValue((int)_blockType, out var index))
                {
                    previewMat.SetFloat(_renderBlockShaderID, 1f); // Enable texture rendering
                    previewMat.SetFloat(_blockIndexShaderID, index); // Set texture index
                }
                else
                {
                    // Optionally handle cases where the block type has no texture mapping
                    Debug.LogWarning($"No texture mapping found in BlocksCatalogue for VoxelType: {_blockType}");
                }
                break;

            case PlacementState.Removing:
                previewMat.SetFloat(_removingShaderID, 1f);
                // No specific block texture needed for removal preview
                break;

            case PlacementState.None:
                // All flags already reset, do nothing further
                break;
        }
    }

    #endregion
}
