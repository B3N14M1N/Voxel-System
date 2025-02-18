using System;
using UnityEngine;
using UnityEngine.EventSystems;
using VoxelSystem.Data.Blocks;
using Zenject;

public class VoxelBlockManager : MonoBehaviour
{
    [Inject] private readonly BlocksCatalogue _blocksCatalogue;

    public Camera mainCamera;
    public GameObject blockPrefab;
    public LayerMask terrainLayer;
    public event Action OnSelected;

    private GameObject _previewObject;
    private enum PlacementState { None, Placing, Removing }
    private PlacementState _currentState = PlacementState.None;
    private VoxelType _blockType;

    private void Awake()
    {
        if (_previewObject == null && blockPrefab != null)
            _previewObject = Instantiate(blockPrefab);
    }

    void Update()
    {
        if (_currentState == PlacementState.None || EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, terrainLayer) && hit.normal.y >= 1.0f)
        {
            Vector3 targetPosition = GetSnappedPosition(hit.point);

            ShowPreview(targetPosition);

            if (_currentState == PlacementState.Placing)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    PlaceCube(targetPosition);
                }
            }
            else if (_currentState == PlacementState.Removing && Input.GetMouseButtonDown(0))
            {
                RemoveCube(targetPosition);
            }
        }
        else
        {
            HidePreview();
        }
    }

    private Vector3 GetSnappedPosition(Vector3 hitPos)
    {
        Vector3 snapped = new(
            Mathf.FloorToInt(hitPos.x),
            Mathf.FloorToInt(hitPos.y),
            Mathf.FloorToInt(hitPos.z)
        );

        if (_currentState == PlacementState.Removing)
            snapped += Vector3.down;

        return snapped;
    }

    private void PlaceCube(Vector3 position)
    {
        Debug.Log("Placed Block");
    }

    private void RemoveCube(Vector3 position)
    {
        Debug.Log("Removed Block");
    }

    private void ShowPreview(Vector3 position)
    {
        if (_previewObject == null) return;

        _previewObject.transform.position = position;
        _previewObject.SetActive(true);
    }

    private void HidePreview()
    {
        if (_previewObject == null) return;
           
        _previewObject.SetActive(false);
    }

    public void SetPlacingState(BlockSelector block)
    {
        _blockType = block.Type;
        OnSelected?.Invoke();
        block.SetSelectedState();

        if (_blockType == VoxelType.air)
        {
            SetRemovingState();
            return;
        }

        _currentState = PlacementState.Placing;

        // set material
        if(_previewObject.TryGetComponent<MeshRenderer>(out var renderer))
        {
            renderer.sharedMaterial.SetFloat("_Placing", 1f);
            renderer.sharedMaterial.SetFloat("_Removing", 0f);

            if (_blocksCatalogue.TextureMapping.TryGetValue((int)_blockType, out var index))
            {
                renderer.sharedMaterial.SetFloat("_Render_Block", 1f);
                renderer.sharedMaterial.SetFloat("_Block_Index", index);
            }
        }
    }

    public void SetRemovingState()
    {
        _currentState = PlacementState.Removing;

        // set material
        if (_previewObject.TryGetComponent<MeshRenderer>(out var renderer))
        {
            renderer.sharedMaterial.SetFloat("_Removing", 1f);
            renderer.sharedMaterial.SetFloat("_Placing", 0f);
            renderer.sharedMaterial.SetFloat("_Render_Block", 0f);
            renderer.sharedMaterial.SetFloat("_Block_Index", 0f);
        }
    }

    public void SetIdleState()
    {
        _blockType = VoxelType.air;
        OnSelected?.Invoke();
        _currentState = PlacementState.None;

        // set material
        if (_previewObject.TryGetComponent<MeshRenderer>(out var renderer))
        {
            renderer.sharedMaterial.SetFloat("_Render_Block", 0f);
            renderer.sharedMaterial.SetFloat("_Block_Index", 0f);
            renderer.sharedMaterial.SetFloat("_Placing", 0f);
            renderer.sharedMaterial.SetFloat("_Removing", 0f);
        }

        HidePreview();
    }

    private void OnDestroy()
    {
        if (_previewObject != null)
        {
            Destroy(_previewObject);
        }
    }
}