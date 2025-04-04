using System;
using Unity.Collections;
using UnityEngine;
using VoxelSystem.Data; // Your Voxel, HeightMap structs

public class ChunkDataHandler : IDisposable
{
    private NativeArray<Voxel> _voxels;
    private NativeArray<HeightMap> _heightMap;

    public bool IsDataGenerated { get; private set; }
    public bool IsDirty { get; set; } // Can be set externally if needed, true on Upload/Set

    // Give access via read-only properties or methods if needed by ChunkJob's generators
    public NativeArray<Voxel> Voxels => _voxels;
    public NativeArray<HeightMap> HeightMap => _heightMap;

    // Provide ref access specifically for the generators if absolutely required by their current design
    public ref NativeArray<Voxel> VoxelsRef => ref _voxels;
    public ref NativeArray<HeightMap> HeightMapRef => ref _heightMap;

    public ChunkDataHandler()
    {
        IsDataGenerated = false;
        IsDirty = false;
    }

    public void UploadData(ref NativeArray<Voxel> newVoxels, ref NativeArray<HeightMap> newHeightMap)
    {
        // Dispose previous arrays if they exist
        DisposeBuffers();

        // Take ownership of the new arrays
        // Consider if the incoming arrays need copying instead of taking ownership
        _voxels = newVoxels;
        _heightMap = newHeightMap;

        IsDataGenerated = true;
        IsDirty = false; // New data is not considered dirty
    }

    public Voxel GetVoxel(int x, int y, int z)
    {
        if (!IsDataGenerated || !_voxels.IsCreated) return Voxel.Empty; // Handle cases where data isn't ready
        return _voxels[GetVoxelIndex(x, y, z)];
    }

    public Voxel GetVoxel(Vector3 localPos)
    {
        if (!IsDataGenerated || !_voxels.IsCreated) return Voxel.Empty;
        return _voxels[GetVoxelIndex((int)localPos.x, (int)localPos.y, (int)localPos.z)];
    }

    public bool SetVoxel(Voxel voxel, int x, int y, int z)
    {
        if (!IsDataGenerated || !_voxels.IsCreated) return false;

        int index = GetVoxelIndex(x, y, z);
        // Original logic check: only allow setting if target is empty
        if (voxel.IsEmpty || _voxels[index].IsEmpty)
        {
            // Optional: Check if value is actually changing
            // if (_voxels[index].Equals(voxel)) return true; // Requires IEquatable

            _voxels[index] = voxel;
            IsDirty = true; // Mark as dirty on modification
            return true;
        }
        Debug.Log($"Cannot swap Voxels at local coords: ({x},{y},{z})");
        return false; // Cannot place non-empty in non-empty
    }

    public bool SetVoxel(Voxel voxel, Vector3 pos)
    {
        return SetVoxel(voxel, (int)pos.x, (int)pos.y, (int)pos.z);
    }

    private int GetVoxelIndex(int x, int y, int z)
    {
        // Ensure consistent index calculation
        return z + (y * (WorldSettings.ChunkWidth + 2)) + (x * (WorldSettings.ChunkWidth + 2) * WorldSettings.ChunkHeight);
    }

    public void ClearData()
    {
        DisposeBuffers();
        IsDataGenerated = false;
        // Should IsDirty be reset? Depends on if clearing means "saved" or just unloaded.
        // Let's assume clearing means unloaded, keep IsDirty state if it was dirty.
    }

    private void DisposeBuffers()
    {
        if (_voxels.IsCreated) _voxels.Dispose();
        if (_heightMap.IsCreated) _heightMap.Dispose();
    }

    public void Dispose()
    {
        DisposeBuffers();
    }
}