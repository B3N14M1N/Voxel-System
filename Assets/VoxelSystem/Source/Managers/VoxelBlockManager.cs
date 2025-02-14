using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VoxelBlockManager : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject blockPrefab;
    public LayerMask terrainLayer;

    private enum PlacementState { None, Placing, Removing }
    private PlacementState _currentState = PlacementState.None;

    private GameObject _previewObject;
    private VoxelType _blockType;
    public event Action OnSelected;

    void Update()
    {
        if (_currentState == PlacementState.None || EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, terrainLayer) && hit.normal.y == 1)
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
        {
            snapped -= Vector3.up;
        }

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
        if (_previewObject == null)
        {
            _previewObject = Instantiate(blockPrefab);
        }
        _previewObject.transform.position = position;
    }

    private void HidePreview()
    {
        if (_previewObject != null)
        {
            Destroy(_previewObject);
        }
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
    }

    public void SetRemovingState()
    {
        _currentState = PlacementState.Removing;

        // set material
    }

    public void SetIdleState()
    {
        _blockType = VoxelType.air;
        OnSelected?.Invoke();
        _currentState = PlacementState.None;
        HidePreview();
    }
}