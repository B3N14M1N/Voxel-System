using System;
using Unity.Collections;
using UnityEngine;
using VoxelSystem.Data;

public class ChunkDataHandler : IDisposable
{
    private static int _paddedWidth = WorldSettings.ChunkWidth + 2;
    private static int _height = WorldSettings.ChunkHeight;
    private static int _paddedWidthSq = (WorldSettings.ChunkWidth + 2) * (WorldSettings.ChunkWidth + 2);

    private NativeArray<Voxel> _voxels;
    private NativeArray<HeightMap> _heightMap;

    /// <summary>
    /// Gets a value indicating whether the core voxel and heightmap data has been generated and assigned.
    /// </summary>
    public bool IsDataGenerated { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the chunk data has been modified since generation.
    /// </summary>
    public bool IsDirty { get; set; }

    /// <summary>
    /// Provides direct reference access to the internal voxel data array. Use with caution.
    /// </summary>
    public ref NativeArray<Voxel> VoxelsRef => ref _voxels;

    /// <summary>
    /// Provides direct reference access to the internal heightmap data array. Use with caution.
    /// </summary>
    public ref NativeArray<HeightMap> HeightMapRef => ref _heightMap;

    public ChunkDataHandler()
    {
        IsDataGenerated = false;
        IsDirty = false;
    }

    // --- Public Methods ---

    /// <summary>
    /// Takes ownership of generated data arrays, validating sizes. Disposes previous data.
    /// </summary>
    public void UploadData(ref NativeArray<Voxel> newVoxels, ref NativeArray<HeightMap> newHeightMap)
    {
        Dispose();
        _paddedWidth = WorldSettings.ChunkWidth + 2;
        _height = WorldSettings.ChunkHeight;
        _paddedWidthSq = (WorldSettings.ChunkWidth + 2) * (WorldSettings.ChunkWidth + 2);
        int expectedVoxelLength = _paddedWidth * _height * _paddedWidth;
        int expectedHeightMapLength = _paddedWidthSq;

        bool sizeMismatch = false;

        if (!newVoxels.IsCreated || newVoxels.Length != expectedVoxelLength)
        {
            Debug.LogError($"ChunkDataHandler: Incorrect voxel array length. Expected {expectedVoxelLength}, got {newVoxels.Length}.");
            sizeMismatch = true;
        }
        if (!newHeightMap.IsCreated || newHeightMap.Length != expectedHeightMapLength)
        {
            Debug.LogError($"ChunkDataHandler: Incorrect heightmap array length. Expected {expectedHeightMapLength}, got {newHeightMap.Length}.");
            sizeMismatch = true;
        }

        if (sizeMismatch)
        {
            if (newVoxels.IsCreated) newVoxels.Dispose();
            if (newHeightMap.IsCreated) newHeightMap.Dispose();
            IsDataGenerated = false;
            IsDirty = false;
            return;
        }

        _voxels = newVoxels;
        _heightMap = newHeightMap;
        IsDataGenerated = true;
        IsDirty = false;
    }

    /// <summary>
    /// Gets the voxel at the specified local chunk coordinates (0 to Width-1 / 0 to Height-1).
    /// </summary>
    public Voxel GetVoxel(int x, int y, int z)
    {
        if (!IsDataGenerated || !_voxels.IsCreated) return Voxel.Empty;

        if (x < 0 || x >= WorldSettings.ChunkWidth ||
            y < 0 || y >= WorldSettings.ChunkHeight ||
            z < 0 || z >= WorldSettings.ChunkWidth)
        {
            Debug.LogWarning($"GetVoxel called with out-of-bounds local coordinates ({x},{y},{z}). Chunk range is 0-{WorldSettings.ChunkWidth - 1} / 0-{WorldSettings.ChunkHeight - 1}. Returning Empty.");
            return Voxel.Empty;
        }

        return _voxels[GetVoxelIndex(x + 1, y, z + 1)];
    }

    /// <summary>
    /// Gets the voxel at the specified local chunk position.
    /// </summary>
    public Voxel GetVoxel(Vector3 localPos)
    {
        return GetVoxel(Mathf.FloorToInt(localPos.x), Mathf.FloorToInt(localPos.y), Mathf.FloorToInt(localPos.z));
    }

    /// <summary>
    /// Sets the voxel at the specified local chunk coordinates (0 to Width-1 / 0 to Height-1). Updates heightmap. Marks chunk dirty.
    /// </summary>
    /// <returns>True if set successfully, false otherwise.</returns>
    public bool SetVoxel(Voxel voxel, int x, int y, int z)
    {
        if (!IsDataGenerated || !_voxels.IsCreated || !_heightMap.IsCreated)
        {
            Debug.LogWarning($"SetVoxel failed: Data not generated.");
            return false;
        }

        if (x < 0 || x >= WorldSettings.ChunkWidth ||
            y < 0 || y >= WorldSettings.ChunkHeight ||
            z < 0 || z >= WorldSettings.ChunkWidth)
        {
            Debug.LogWarning($"SetVoxel called with out-of-bounds local coordinates ({x},{y},{z}). Cannot modify.");
            return false;
        }

        int voxelIndex = GetVoxelIndex(x + 1, y, z + 1);
        int heightMapIndex = GetMapIndex(x + 1, z + 1);

        Voxel currentVoxel = _voxels[voxelIndex];
        HeightMap currentHeight = _heightMap[heightMapIndex];

        bool wasSolid = !currentVoxel.IsEmpty;
        bool placedSolid = !voxel.IsEmpty;

        if (wasSolid == placedSolid)
            return false;

        uint newSolidHeight = currentHeight.GetSolid() + (uint)(placedSolid ? 1 : -1);

        if (newSolidHeight > WorldSettings.ChunkHeight && !placedSolid)
        {
            Debug.LogWarning($"Heightmap underflow detected at ({x + 1},{z + 1}). Clamping height to 0.");
            return false;
        }

        _voxels[voxelIndex] = voxel;
        currentHeight.SetSolid((byte)newSolidHeight);
        _heightMap[heightMapIndex] = currentHeight;
        IsDirty = true;

        return true;
    }

    /// <summary>
    /// Sets the voxel at the specified local chunk position. Updates heightmap. Marks chunk dirty.
    /// </summary>
    /// <returns>True if set successfully, false otherwise.</returns>
    public bool SetVoxel(Voxel voxel, Vector3 pos)
    {
        return SetVoxel(voxel, Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
    }

    /// <summary>
    /// Updates the heightmap count at a specific padded coordinate, typically for border adjustments between chunks.
    /// </summary>
    /// <returns>True if updated successfully, false otherwise.</returns>
    public bool UpdateHeightMapBorder(int x, int z, bool isRemoving)
    {
        if (!IsDataGenerated || !_heightMap.IsCreated)
        {
            Debug.LogWarning($"Height Map border update failed: Data not generated.");
            return false;
        }

        if (x < 0 || x >= _paddedWidth || z < 0 || z >= _paddedWidth)
        {
            Debug.LogWarning($"UpdateHeightMapBorder called with out-of-bounds padded coordinates ({x},{z}). Cannot modify.");
            return false;
        }

        int heightMapIndex = GetMapIndex(x, z);
        HeightMap currentHeightData = _heightMap[heightMapIndex];
        uint currentSolidHeight = currentHeightData.GetSolid();
        uint newSolidHeight = currentSolidHeight + (uint)(isRemoving ? -1 : 1);

        if (newSolidHeight > WorldSettings.ChunkHeight && isRemoving)
        {
            newSolidHeight = 0;
            Debug.LogWarning($"Heightmap underflow detected during border update at ({x},{z}). Clamping height to 0.");
        }

        HeightMap newHeight = new((byte)newSolidHeight);
        _heightMap[heightMapIndex] = newHeight;

        IsDirty = true;
        return true;
    }

    /// <summary>
    /// Updates the heightmap count at a specific padded position, typically for border adjustments between chunks.
    /// </summary>
    /// <returns>True if updated successfully, false otherwise.</returns>
    public bool UpdateHeightMapBorder(Vector2 pos, bool isRemoving)
    {
        return UpdateHeightMapBorder(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), isRemoving);
    }

    /// <summary>
    /// Disposes the NativeArray buffers. Part of the IDisposable pattern.
    /// </summary>
    public void Dispose()
    {
        if (_voxels.IsCreated)
        {
            _voxels.Dispose();
            _voxels = default;
        }

        if (_heightMap.IsCreated)
        {
            _heightMap.Dispose();
            _heightMap = default;
        }

        IsDataGenerated = false;
    }

    /// <summary>
    /// Calculates the 1D index for the voxel array based on padded coordinates. Assumes X-major, then Y, then Z layout.
    /// </summary>
    private int GetVoxelIndex(int paddedX, int y, int paddedZ)
    {
        return paddedZ + (y * _paddedWidth) + (paddedX * _paddedWidth * _height);
    }

    /// <summary>
    /// Calculates the 1D index for the heightmap array based on padded coordinates. Assumes X-major, then Z layout.
    /// </summary>
    private int GetMapIndex(int paddedX, int paddedZ)
    {
        return paddedZ + (paddedX * _paddedWidth);
    }
}