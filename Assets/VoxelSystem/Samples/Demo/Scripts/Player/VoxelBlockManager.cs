using System;
using UnityEngine;
using UnityEngine.EventSystems;
using VoxelSystem.Data;
using VoxelSystem.Data.Blocks;
using VoxelSystem.Managers;
using Zenject;

public class VoxelBlockManager : MonoBehaviour
{
    /// <summary>
    /// Defines the possible states for the voxel placement/removal operations.
    /// </summary>
    private enum PlacementState
    {
        None,
        Placing,
        Removing
    }

    [SerializeField] private Camera _mainCamera;
    [SerializeField] private GameObject _blockPrefab;
    [SerializeField] private LayerMask _terrainLayer;
    [SerializeField] private Vector3 _previewColliderSize = Vector3.one;

    [Inject] private readonly BlocksCatalogue _blocksCatalogue;
    [Inject] private readonly IChunksManager _chunksManager;

    private PlacementState _currentState = PlacementState.None;
    private VoxelType _blockType;
    private GameObject _previewObject;
    private MeshRenderer _previewRenderer;
    private int _placingShaderID;
    private int _removingShaderID;
    private int _renderBlockShaderID;
    private int _blockIndexShaderID;
    private int _blockedShaderID;
    private bool _isBlocked;

    /// <summary>
    /// Invoked when the selected block or placement state changes, primarily used by UI elements.
    /// </summary>
    public event Action Selected;

    /// <summary>
    /// Initializes references, instantiates the preview object, and caches shader property IDs.
    /// Disables the component if essential references are missing.
    /// </summary>
    private void Awake()
    {
        if (_mainCamera == null)
        {
            Debug.LogError("VoxelBlockManager: Main Camera reference not set!", this);
            enabled = false;
            return;
        }
        if (_blockPrefab == null)
        {
            Debug.LogError("VoxelBlockManager: Block Prefab reference not set!", this);
        }
        else if (_previewObject == null)
        {
            _previewObject = Instantiate(_blockPrefab);
            _previewObject.SetActive(false);
            _previewObject.TryGetComponent<MeshRenderer>(out _previewRenderer);
            if (_previewRenderer != null)
            {
                // Ensure the preview object has a unique material instance to avoid modifying the original prefab
                _previewRenderer.sharedMaterial = new Material(_previewRenderer.sharedMaterial);
            }
            else
            {
                Debug.LogWarning("VoxelBlockManager: Preview Prefab does not have a MeshRenderer component.", this);
            }
        }

        _placingShaderID = Shader.PropertyToID("_Placing");
        _removingShaderID = Shader.PropertyToID("_Removing");
        _renderBlockShaderID = Shader.PropertyToID("_Render_Block");
        _blockIndexShaderID = Shader.PropertyToID("_Block_Index");
        _blockedShaderID = Shader.PropertyToID("_Blocked");

        SetIdleState();
    }

    /// <summary>
    /// Handles player input for placing or removing blocks each frame.
    /// Performs raycasting, updates the preview position and state, and triggers block modification.
    /// </summary>
    private void Update()
    {
        HidePreview();

        if (_currentState == PlacementState.None || EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _terrainLayer) || hit.normal.y < 1.0f)
        {
            return;
        }

        Vector3 targetPosition = GetSnappedPosition(hit.point, hit.normal);
        _isBlocked = CheckForCollisions(targetPosition + new Vector3(0.5f, 0.5f, 0.5f));
        ShowPreview(targetPosition, _isBlocked);

        if (_isBlocked || !Input.GetMouseButtonDown(0))
        {
            return;
        }

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
    /// Cleans up the instantiated preview object when the manager is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (_previewObject != null)
        {
            // Destroy the material instance created for the preview object
            if(_previewRenderer != null && _previewRenderer.sharedMaterial != null)
            {
                Destroy(_previewRenderer.sharedMaterial);
                _previewRenderer.sharedMaterial = null;
                _previewRenderer = null;
            }

            Destroy(_previewObject);
            _previewObject = null;
        }
    }

    /// <summary>
    /// Sets the manager state to 'Placing' for the specified block type.
    /// Updates the preview material and notifies listeners via the Selected event.
    /// </summary>
    /// <param name="block">The BlockSelector providing the VoxelType to place.</param>
    public void SetPlacingState(BlockSelector block)
    {
        if (block == null) return;

        _blockType = block.Type;
        Selected?.Invoke();
        block.SetSelectedState();

        if (_blockType == VoxelType.air)
        {
            SetRemovingState();
            return;
        }

        _currentState = PlacementState.Placing;
        UpdatePreviewMaterial();
    }

    /// <summary>
    /// Sets the manager state to 'Removing'.
    /// Updates the preview material to indicate removal mode.
    /// </summary>
    public void SetRemovingState()
    {
        _currentState = PlacementState.Removing;
        _blockType = VoxelType.air;
        UpdatePreviewMaterial();
    }

    /// <summary>
    /// Sets the manager state to 'None' (idle).
    /// Hides the placement preview and notifies listeners via the Selected event.
    /// </summary>
    public void SetIdleState()
    {
        _currentState = PlacementState.None;
        _blockType = VoxelType.air;
        Selected?.Invoke();
        UpdatePreviewMaterial();
        HidePreview();
    }

    /// <summary>
    /// Calculates the target integer voxel grid position based on the raycast hit point and normal.
    /// The position is offset based on whether placing or removing.
    /// </summary>
    /// <param name="hitPos">The precise world position of the raycast hit.</param>
    /// <param name="hitNormal">The normal vector of the surface hit by the raycast.</param>
    /// <returns>The calculated integer grid position (snapped) for the voxel modification.</returns>
    private Vector3 GetSnappedPosition(Vector3 hitPos, Vector3 hitNormal)
    {
        Vector3 targetPos;

        if (_currentState == PlacementState.Placing)
        {
            targetPos = hitPos + hitNormal * 0.5f;
        }
        else
        {
            targetPos = hitPos - hitNormal * 0.5f;
        }

        Vector3 snapped = new Vector3(
            Mathf.FloorToInt(targetPos.x),
            Mathf.FloorToInt(targetPos.y + 0.001f),
            Mathf.FloorToInt(targetPos.z)
        );

        return snapped;
    }

    /// <summary>
    /// Attempts to place a block of the currently selected type at the specified grid position.
    /// Does nothing if not in the 'Placing' state, the selected type is 'air', or the position is blocked.
    /// </summary>
    /// <param name="position">The integer grid position where the block should be placed.</param>
    private void PlaceBlock(Vector3 position)
    {
        if (_currentState != PlacementState.Placing || _blockType == VoxelType.air || _isBlocked)
        {
            return;
        }

        Voxel voxelToPlace = new((byte)_blockType);
        bool success = _chunksManager.ModifyVoxel(position, voxelToPlace);

        if (success)
        {
            // Optional: Add sound effects, particle effects, etc.
            //Debug.Log($"Placed block of type {_blockType} at {position}");
        }
        else
        {
            // Optional: Provide feedback if placement failed (e.g., trying to place in air, blocked by other logic)
            //Debug.LogWarning($"Failed to place block of type {_blockType} at {position}. ModifyVoxel returned false.");
        }
    }

    /// <summary>
    /// Attempts to remove a block (place 'air') at the specified grid position.
    /// Does nothing if not in the 'Removing' state.
    /// </summary>
    /// <param name="position">The integer grid position where the block should be removed.</param>
    private void RemoveBlock(Vector3 position)
    {
        if (_currentState != PlacementState.Removing)
        {
            return;
        }

        Voxel emptyVoxel = Voxel.Empty;
        bool success = _chunksManager.ModifyVoxel(position, emptyVoxel);

        if (success)
        {
            // Optional: Add sound effects, particle effects, etc.
            //Debug.Log($"Removed block at {position}");
        }
        else
        {
            // Optional: Provide feedback if removal failed (e.g., trying to remove bedrock, out of bounds)
            //Debug.LogWarning($"Failed to remove block at {position}. ModifyVoxel returned false.");
        }
    }

    /// <summary>
    /// Activates and positions the preview object at the target location.
    /// Updates the preview material to indicate if the placement is currently blocked.
    /// </summary>
    /// <param name="position">The world position where the preview should be displayed.</param>
    /// <param name="isBlocked">Whether the target placement position is currently obstructed.</param>
    private void ShowPreview(Vector3 position, bool isBlocked)
    {
        if (_previewObject == null || _previewRenderer == null) return;

        Material previewMat = _previewRenderer.sharedMaterial;
        if (previewMat != null)
        {
            previewMat.SetFloat(_blockedShaderID, isBlocked ? 1f : 0f);
        }

        _previewObject.transform.position = position;
        _previewObject.SetActive(true);
    }

    /// <summary>
    /// Deactivates the preview object, hiding it from view.
    /// </summary>
    private void HidePreview()
    {
        if (_previewObject == null) return;
        _previewObject.SetActive(false);
    }

    /// <summary>
    /// Updates the shader properties of the preview object's material based on the current placement state (None, Placing, Removing) and block type.
    /// </summary>
    private void UpdatePreviewMaterial()
    {
        if (_previewRenderer == null || _previewRenderer.sharedMaterial == null) return;

        Material previewMat = _previewRenderer.sharedMaterial;

        previewMat.SetFloat(_placingShaderID, 0f);
        previewMat.SetFloat(_removingShaderID, 0f);
        previewMat.SetFloat(_renderBlockShaderID, 0f);
        previewMat.SetFloat(_blockIndexShaderID, 0f);
        previewMat.SetFloat(_blockedShaderID, 0f);

        switch (_currentState)
        {
            case PlacementState.Placing:
                previewMat.SetFloat(_placingShaderID, 1f);
                if (_blocksCatalogue != null && _blocksCatalogue.TextureMapping.TryGetValue((int)_blockType, out var index))
                {
                    previewMat.SetFloat(_renderBlockShaderID, 1f);
                    previewMat.SetFloat(_blockIndexShaderID, index);
                }
                else if (_blockType != VoxelType.air)
                {
                    Debug.LogWarning($"No texture mapping found in BlocksCatalogue for VoxelType: {_blockType}");
                }
                previewMat.SetFloat(_blockedShaderID, _isBlocked ? 1f : 0f);
                break;

            case PlacementState.Removing:
                previewMat.SetFloat(_removingShaderID, 1f);
                break;

            case PlacementState.None:
                break;
        }
    }

    /// <summary>
    /// Checks for any colliders overlapping the potential placement area using Physics.CheckBox.
    /// Ignores colliders on the terrain layer itself.
    /// </summary>
    /// <param name="centerPosition">The center point of the box check (typically the center of the target voxel).</param>
    /// <returns>True if any colliders (excluding terrain) are found within the box, false otherwise.</returns>
    private bool CheckForCollisions(Vector3 centerPosition)
    {
        return Physics.CheckBox(centerPosition, _previewColliderSize / 2, Quaternion.identity, ~_terrainLayer);
    }
}
