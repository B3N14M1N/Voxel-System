using System;
using Unity.Collections;
using UnityEngine;
using VoxelSystem.Data;
using VoxelSystem.Factory; // For Material Access
using VoxelSystem.Managers; // For IChunksManager

public class Chunk : IDisposable
{
    private readonly ChunkDataHandler _dataHandler;
    private readonly ChunkViewHandler _viewHandler;
    private readonly IChunksManager _chunksManager; // Keep reference if needed by handlers or logic here
    #region Properties (Delegated or Direct)

    public Vector3 Position { get; private set; }

    // Data properties delegated
    public bool DataGenerated => _dataHandler.IsDataGenerated;
    public bool Dirty
    {
        get => _dataHandler.IsDirty;
        set => _dataHandler.IsDirty = value; // Allow external setting if necessary
    }
    // Expose NativeArrays carefully if needed externally (e.g., by ChunkJob generators)
    public ref NativeArray<Voxel> Voxels => ref _dataHandler.VoxelsRef;
    public ref NativeArray<HeightMap> HeightMap => ref _dataHandler.HeightMapRef;

    // View properties delegated
    public bool MeshGenerated => _viewHandler.IsMeshGenerated;
    public bool ColliderGenerated => _viewHandler.IsColliderGenerated;
    public bool Active { get => _viewHandler.IsActive; set => _viewHandler.IsActive = value; }
    public bool Render { get => _viewHandler.IsRenderEnabled; set => _viewHandler.IsRenderEnabled = value; }

    // Simulation property - Uses original logic
    public bool Simulate
    {
        get { return ColliderGenerated; } // Depends on collider view state
        set
        {
            // Original logic: Clearing data when simulation stops
            if (value == false)
            {
                _dataHandler.Dispose();
            }
            // Add logic here if setting Simulate=true should trigger something (e.g., collider generation if not ready)
        }
    }

    // Voxel accessors delegated
    public Voxel this[Vector3 pos] => _dataHandler.GetVoxel(pos);
    public Voxel this[int x, int y, int z] => _dataHandler.GetVoxel(x, y, z);

    #endregion

    #region Constructor and Setup

    // Constructor now requires the manager
    public Chunk(Vector3 position, IChunksManager chunksManager, LayerMask layerMask = default, Transform defaultParent = null)
    {
        Position = position;
        _chunksManager = chunksManager;
        _dataHandler = new ChunkDataHandler();
        _viewHandler = new ChunkViewHandler(chunksManager); // Pass manager for callbacks

        // Create GameObject instance immediately or lazily? Original code created it immediately.
        _viewHandler.CreateInstance(position, defaultParent, layerMask);
    }

    // Used by ChunksManager when reusing pooled chunks
    public void Reuse(Vector3 newPosition)
    {
        Position = newPosition;
        // Reset state flags (data might still be dirty from previous use if not saved)
        _dataHandler.Dispose();
        _viewHandler.PrepareForPooling(); // Clears mesh/collider, resets name
        _viewHandler.UpdateInstanceTransform(newPosition); // Set new position/name
        Active = false; // Start inactive/invisible
        Render = false;
        // Do NOT reset IsDirty here - let manager decide if loading overwrites dirty state
    }

    #endregion

    #region Voxel Modification (Delegated)

    // Keep SetVoxel on Chunk as the public API, delegate to handler
    public bool SetVoxel(Voxel voxel, Vector3 pos)
    {
        // Original logging:
        // if (voxel.IsEmpty) Debug.Log($"Removing voxel at coords: {pos}");
        // else Debug.Log($"Placing voxel at coords: {pos}");

        // Delegate and return success/failure
        bool success = _dataHandler.SetVoxel(voxel, pos);
        if (success)
        {
            //Debug.Log($"Voxel set at {pos}, chunk marked Dirty.");
            Dirty = true;
        }

        else Debug.Log($"Failed to set voxel at {pos}.");
        return success;
    }

    public bool SetVoxel(Voxel voxel, int x, int y, int z)
    {
        return SetVoxel(voxel, new Vector3(x, y, z)); // Convert and call the other overload
    }

    public bool UpdateHeightMapBorder(int x, int z, bool isRemoving)
    {
        return _dataHandler.UpdateHeightMapBorder(x, z, isRemoving);
    }

    public bool UpdateHeightMapBorder(Vector2 pos, bool isRemoving)
    {
        return _dataHandler.UpdateHeightMapBorder(pos, isRemoving);
    }
    #endregion

    #region Mesh & Data Upload (Delegated)

    // Called by ChunkJob/Generators after completion
    public void UploadData(ref NativeArray<Voxel> voxels, ref NativeArray<HeightMap> heightMap)
    {
        _dataHandler.UploadData(ref voxels, ref heightMap);
    }

    // Called by ChunkJob/Generators after completion
    public void UploadMesh(Mesh mesh)
    {
        _viewHandler.UploadMesh(mesh);
        _viewHandler.ApplyMaterial(ChunkFactory.Instance.Material); // Ensure correct material - Requires ChunkFactory instance access
    }

    // Called by ChunkJob/Generators after completion
    public void UploadCollider(Mesh mesh)
    {
        _viewHandler.UploadColliderMesh(mesh);
    }

    // Method to update material if changed externally
    public void UpdateMaterial(Material material)
    {
        _viewHandler.ApplyMaterial(material);
    }

    #endregion

    #region Clear & Disposing (Orchestrated)

    // Called by ChunksManager when reusing/pooling
    public void ClearChunk()
    {
        // Check dirty flag before clearing data
        if (Dirty)
            Debug.Log($"Chunk {Position} needs saving before clearing."); // Add saving logic hook here

        _dataHandler.Dispose();
        _viewHandler.PrepareForPooling(); // Clear meshes, set inactive, reset name
    }

    // Called by ChunksManager when destroying permanently
    public void Dispose()
    {
        // Ensure handlers dispose their resources (NativeArrays, GameObject)
        _dataHandler.Dispose();
        _viewHandler.Dispose();
        //Null references to help GC (optional)
        //_chunksManager = null;
    }

    #endregion
}