using System;
using Unity.Collections;
using UnityEngine;
using VoxelSystem.Data; // For Voxel, HeightMap structs

public class ChunkDataHandler : IDisposable
{
    private NativeArray<Voxel> _voxels;
    private NativeArray<HeightMap> _heightMap;

    // Calculate dimensions including padding once for clarity
    private readonly int _paddedWidth = WorldSettings.ChunkWidth + 2;
    private readonly int _height = WorldSettings.ChunkHeight;
    private readonly int _paddedWidthSq = (WorldSettings.ChunkWidth + 2) * (WorldSettings.ChunkWidth + 2); // For heightmap indexing

    public bool IsDataGenerated { get; private set; }
    public bool IsDirty { get; set; } // True on successful SetVoxel

    // Provide direct access only if absolutely necessary for external generators.
    public ref NativeArray<Voxel> VoxelsRef => ref _voxels;
    public ref NativeArray<HeightMap> HeightMapRef => ref _heightMap;

    public ChunkDataHandler()
    {
        IsDataGenerated = false;
        IsDirty = false;
        // NativeArrays are typically created/assigned in UploadData
    }

    /// <summary>
    /// Takes ownership of newly generated data arrays. Validates array sizes.
    /// </summary>
    public void UploadData(ref NativeArray<Voxel> newVoxels, ref NativeArray<HeightMap> newHeightMap)
    {
        DisposeBuffers(); // Dispose previous arrays

        // Validate incoming array sizes against expected padded dimensions
        int expectedVoxelLength = _paddedWidth * _height * _paddedWidth; // Based on user index formula layout X->Y->Z
        int expectedHeightMapLength = _paddedWidthSq;

        bool sizeMismatch = false;
        if (!newVoxels.IsCreated || newVoxels.Length != expectedVoxelLength)
        {
            Debug.LogError($"ChunkDataHandler: Incorrect voxel array length provided or array not created. Expected {expectedVoxelLength}, got {newVoxels.Length}.");
            sizeMismatch = true;
        }
        if (!newHeightMap.IsCreated || newHeightMap.Length != expectedHeightMapLength)
        {
            Debug.LogError($"ChunkDataHandler: Incorrect heightmap array length provided or array not created. Expected {expectedHeightMapLength}, got {newHeightMap.Length}.");
            sizeMismatch = true;
        }

        if (sizeMismatch)
        {
            // Dispose potentially invalid incoming arrays if they were created
            if (newVoxels.IsCreated) newVoxels.Dispose();
            if (newHeightMap.IsCreated) newHeightMap.Dispose();
            IsDataGenerated = false;
            IsDirty = false;
            return;
        }

        // Sizes are correct, take ownership
        _voxels = newVoxels;
        _heightMap = newHeightMap;

        IsDataGenerated = true;
        IsDirty = false; // Freshly uploaded data is not considered dirty
    }

    /// <summary>
    /// Gets the voxel at the specified local chunk coordinates (0 to Width-1 / 0 to Height-1).
    /// Handles mapping to the internal padded array.
    /// </summary>
    public Voxel GetVoxel(int x, int y, int z)
    {
        if (!IsDataGenerated || !_voxels.IsCreated) return Voxel.Empty; // Data not ready

        // Validate input coordinates are within chunk bounds (0 to Dim-1)
        if (x < 0 || x >= WorldSettings.ChunkWidth ||
            y < 0 || y >= WorldSettings.ChunkHeight ||
            z < 0 || z >= WorldSettings.ChunkWidth)
        {
            Debug.LogWarning($"GetVoxel called with out-of-bounds local coordinates ({x},{y},{z}). Chunk range is 0-{WorldSettings.ChunkWidth - 1} / 0-{WorldSettings.ChunkHeight - 1}. Returning Empty.");
            return Voxel.Empty;
        }
        // Access padded array using internal indexer with +1 offset
        return _voxels[GetVoxelIndex(x, y, z)];
    }

    public Voxel GetVoxel(Vector3 localPos)
    {
        return GetVoxel(Mathf.FloorToInt(localPos.x), Mathf.FloorToInt(localPos.y), Mathf.FloorToInt(localPos.z));
    }

    /// <summary>
    /// Sets the voxel at the specified local chunk coordinates (0 to Width-1 / 0 to Height-1).
    /// Updates the heightmap accordingly using GetSolid/SetSolid. Marks the chunk as dirty on success.
    /// </summary>
    /// <returns>True if the voxel was successfully set, false otherwise.</returns>
    public bool SetVoxel(Voxel voxel, int x, int y, int z)
    {
        if (!IsDataGenerated || !_voxels.IsCreated || !_heightMap.IsCreated)
        {
            Debug.LogWarning($"SetVoxel failed: Data not generated for chunk.");
            return false; // Data not ready
        }

        // Validate input coordinates are within chunk bounds (0 to Dim-1)
        if (x < 0 || x >= WorldSettings.ChunkWidth ||
            y < 2 || y >= WorldSettings.ChunkHeight ||
            z < 0 || z >= WorldSettings.ChunkWidth)
        {
            Debug.LogError($"SetVoxel called with out-of-bounds local coordinates ({x},{y},{z}). Cannot modify.");
            return false;
        }

        // --- Calculate Indices (using +1 offset for padded array) ---
        int voxelIndex = GetVoxelIndex(x + 1, y, z + 1);
        int heightMapIndex = GetMapIndex(x + 1, z + 1);

        // --- Optional: Add game logic checks (e.g., bedrock) before modification ---
        // Voxel existingVoxel = _voxels[voxelIndex];
        // if (existingVoxel.IsIndestructible) return false;

        // --- Check if value is actually changing (Requires Voxel implementing IEquatable) ---
        // if (_voxels[voxelIndex] == voxel) return true; // No change needed

        // --- Apply Voxel Change ---
        _voxels[voxelIndex] = voxel;
        bool placedSolid = !voxel.IsEmpty; // Use IsEmpty property from Voxel struct

        // --- Update Heightmap (Solid Height) ---
        uint currentSolidHeight = _heightMap[heightMapIndex].GetSolid(); // Get current solid height

        HeightMap newHeight = new();
        newHeight.SetSolid((uint)(currentSolidHeight + (placedSolid ? 1 : -1)));
        _heightMap[heightMapIndex] = newHeight;
        // NOTE: Liquid height updates are not handled here, assuming ModifyVoxel only affects solid blocks directly.

        // --- Mark Dirty ---
        IsDirty = true; // Mark as dirty on successful modification
        return true;
    }

    public bool SetVoxel(Voxel voxel, Vector3 pos)
    {
        return SetVoxel(voxel, Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
    }

    public bool UpdateHeightMapBorder(Vector2 pos, bool isRemoving)
    {
        return UpdateHeightMapBorder(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), isRemoving);
    }

    public bool UpdateHeightMapBorder(int x, int z, bool isRemoving)
    {
        if (!IsDataGenerated || !_voxels.IsCreated || !_heightMap.IsCreated)
        {
            Debug.LogWarning($"Height Map change failed: Data not generated for chunk.");
            return false; // Data not ready
        }

        // Validate input coordinates are within chunk bounds (0 to Dim-1)
        if (x < 0 || x >= WorldSettings.ChunkWidth + 2 ||
            z < 0 || z >= WorldSettings.ChunkWidth + 2)
        {
            Debug.LogError($"Height Map called with out-of-bounds local coordinates ({x},{z}). Cannot modify.");
            return false;
        }

        int heightMapIndex = GetMapIndex(x, z);

        // --- Update Heightmap (Solid Height) ---
        uint currentSolidHeight = _heightMap[heightMapIndex].GetSolid(); // Get current solid height
        //Debug.LogError($"Current Height: {currentSolidHeight} for map [{x}, {z}]");
        currentSolidHeight = (isRemoving ? currentSolidHeight - 1 : currentSolidHeight + 1);
        //Debug.LogError($"New Height: {currentSolidHeight} for map [{x}, {z}]");

        HeightMap newHeight = new();
        newHeight.SetSolid(currentSolidHeight);
        _heightMap[heightMapIndex] = newHeight;

        // NOTE: Liquid height updates are not handled here, assuming ModifyVoxel only affects solid blocks directly.

        // --- Mark Dirty ---
        IsDirty = true; // Mark as dirty on successful modification
        return true;
    }
    private int GetVoxelIndex(int x, int y, int z)
    {
        return z + (y * (WorldSettings.ChunkWidth + 2)) + (x * (WorldSettings.ChunkWidth + 2) * WorldSettings.ChunkHeight);
    }

    private int GetMapIndex(int x, int z)
    {
        return z + (x * (WorldSettings.ChunkWidth + 2));
    }

    /// <summary>
    /// Disposes internal buffers and resets state. Called when chunk is reused or disposed.
    /// </summary>
    public void ClearData()
    {
        DisposeBuffers(); // Dispose arrays
        IsDataGenerated = false;
        // Keep IsDirty state when clearing (unload != save)
    }

    /// <summary>
    /// Disposes the NativeArray buffers if they are created.
    /// </summary>
    private void DisposeBuffers()
    {
        if (_voxels.IsCreated)
        {
            _voxels.Dispose();
            // Optional: _voxels = default; to clear the struct state
        }
        if (_heightMap.IsCreated)
        {
            _heightMap.Dispose();
            // Optional: _heightMap = default;
        }
        IsDataGenerated = false; // Ensure flag is reset after disposal
    }

    /// <summary>
    /// Disposes managed resources (NativeArrays).
    /// </summary>
    public void Dispose()
    {
        DisposeBuffers();
    }
}