using System;
using UnityEngine;
using Unity.Collections;
using VoxelSystem.Data.Structs;
using VoxelSystem.Settings;

namespace VoxelSystem.Data.Chunk
{
    /// <summary>
    /// Manages the voxel and height map data for a chunk, handling operations to access and modify the data.
    /// </summary>
    public class ChunkDataHandler : IDisposable
    {
        private static int _width = WorldSettings.ChunkWidth;
        private static int _height = WorldSettings.ChunkHeight;
        private static int _paddedWidth = WorldSettings.ChunkWidth + 2;

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

        /// <summary>
        /// Creates a new instance of the ChunkDataHandler.
        /// </summary>
        public ChunkDataHandler()
        {
            IsDataGenerated = false;
            IsDirty = false;
        }

        /// <summary>
        /// Takes ownership of generated data arrays, validating sizes. Disposes previous data.
        /// </summary>
        /// <param name="newVoxels">The new voxel data to use</param>
        /// <param name="newHeightMap">The new height map data to use</param>
        public void UploadData(ref NativeArray<Voxel> newVoxels, ref NativeArray<HeightMap> newHeightMap)
        {
            Dispose();
            _width = WorldSettings.ChunkWidth;
            _height = WorldSettings.ChunkHeight;
            _paddedWidth = _width + 2;
            int expectedVoxelLength = _width * _height * _width;
            int expectedHeightMapLength = _paddedWidth * _paddedWidth;

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
        /// Checks if the specified local chunk coordinates (0 to Width-1 / 0 to Height-1) are valid.
        /// </summary>
        /// <param name="pos">The position within the chunk</param>
        /// <returns>True if the position is valid, false otherwise</returns>
        public bool IsVoxelPositionValid(Vector3 pos)
        {
            return IsVoxelPositionValid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
        }

        /// <summary>
        /// Checks if the specified local chunk coordinates (0 to Width-1 / 0 to Height-1) are valid.
        /// </summary>
        /// <param name="x">The x coordinate within the chunk</param>
        /// <param name="y">The y coordinate within the chunk</param>
        /// <param name="z">The z coordinate within the chunk</param>
        public bool IsVoxelPositionValid(int x, int y, int z)
        {
            if (x < 0 || x >= _width ||
                y <= 0 || y >= _height ||
                z < 0 || z >= _width)
            {
                Debug.LogWarning($"IsVoxelPositionValid called with out-of-bounds local coordinates ({x},{y},{z}). Chunk range is 0-{_width - 1} / 0-{_height - 1}.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the voxel data is generated and valid.
        /// </summary>
        public bool IsDataGeneratedAndValid()
        {
            if (!IsDataGenerated || !_voxels.IsCreated || !_heightMap.IsCreated)
            {
                Debug.LogWarning($"SetVoxel failed: Data not generated.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the voxel at the specified local chunk coordinates (0 to Width-1 / 0 to Height-1).
        /// </summary>
        /// <param name="x">The x coordinate within the chunk</param>
        /// <param name="y">The y coordinate within the chunk</param>
        /// <param name="z">The z coordinate within the chunk</param>
        /// <returns>The voxel at the specified coordinates, or an empty voxel if out of range</returns>
        public Voxel GetVoxel(int x, int y, int z)
        {
            if (!IsDataGeneratedAndValid() || !IsVoxelPositionValid(x, y, z)) return Voxel.Empty;

            return _voxels[GetVoxelIndex(x, y, z)];
        }

        /// <summary>
        /// Gets the voxel at the specified local chunk position.
        /// </summary>
        /// <param name="localPos">The position within the chunk</param>
        /// <returns>The voxel at the specified position</returns>
        public Voxel GetVoxel(Vector3 localPos)
        {
            return GetVoxel(Mathf.FloorToInt(localPos.x), Mathf.FloorToInt(localPos.y), Mathf.FloorToInt(localPos.z));
        }

        /// <summary>
        /// Sets the voxel at the specified local chunk coordinates (0 to Width-1 / 0 to Height-1). Updates heightmap. Marks chunk dirty.
        /// </summary>
        /// <param name="voxel">The voxel to set</param>
        /// <param name="x">The x coordinate within the chunk</param>
        /// <param name="y">The y coordinate within the chunk</param>
        /// <param name="z">The z coordinate within the chunk</param>
        /// <returns>True if set successfully, false otherwise.</returns>
        public bool SetVoxel(Voxel voxel, int x, int y, int z)
        {
            if (!IsDataGeneratedAndValid() || !IsVoxelPositionValid(x, y, z)) return false;

            int voxelIndex = GetVoxelIndex(x, y, z);
            int heightMapIndex = GetMapIndex(x + 1, z + 1);

            Voxel currentVoxel = _voxels[voxelIndex];
            HeightMap currentHeight = _heightMap[heightMapIndex];

            bool wasSolid = !currentVoxel.IsEmpty;
            bool placeSolid = !voxel.IsEmpty;

            if (wasSolid == placeSolid)
            {
                Debug.LogWarning($"Block {currentVoxel.Get()} already placed at ({x}, {y}, {z}).");
                return false;
            }

            int currentHeightValue = currentHeight.GetSolid();

            if (currentHeightValue <= 1 && !placeSolid)
            {
                Debug.LogWarning($"Heightmap underflow detected at ({x + 1},{z + 1}).");
                return false;
            }

            int newSolidHeight = currentHeightValue + (placeSolid ? 1 : -1);

            if (newSolidHeight > WorldSettings.ChunkHeight && !placeSolid)
            {
                Debug.LogWarning($"Heightmap overflow detected at ({x + 1},{z + 1}).");
                return false;
            }

            _voxels[voxelIndex] = voxel;
            currentHeight.SetSolid(newSolidHeight);
            _heightMap[heightMapIndex] = currentHeight;
            IsDirty = true;

            return true;
        }

        /// <summary>
        /// Sets the voxel at the specified local chunk position. Updates heightmap. Marks chunk dirty.
        /// </summary>
        /// <param name="voxel">The voxel to set</param>
        /// <param name="pos">The position within the chunk</param>
        /// <returns>True if set successfully, false otherwise.</returns>
        public bool SetVoxel(Voxel voxel, Vector3 pos)
        {
            return SetVoxel(voxel, Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
        }

        /// <summary>
        /// Updates the heightmap count at a specific padded coordinate, typically for border adjustments between chunks.
        /// </summary>
        /// <param name="x">The x coordinate in the padded grid</param>
        /// <param name="z">The z coordinate in the padded grid</param>
        /// <param name="isRemoving">Whether a voxel is being removed (true) or added (false)</param>
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
            int currentSolidHeight = currentHeightData.GetSolid();
            int newSolidHeight = currentSolidHeight + (isRemoving ? -1 : 1);

            if (newSolidHeight > WorldSettings.ChunkHeight && isRemoving)
            {
                newSolidHeight = 0;
                Debug.LogWarning($"Heightmap underflow detected during border update at ({x},{z}). Clamping height to 0.");
            }

            HeightMap newHeight = new(newSolidHeight);
            _heightMap[heightMapIndex] = newHeight;

            IsDirty = true;
            return true;
        }

        /// <summary>
        /// Updates the heightmap count at a specific padded position, typically for border adjustments between chunks.
        /// </summary>
        /// <param name="pos">The position in the padded grid</param>
        /// <param name="isRemoving">Whether a voxel is being removed (true) or added (false)</param>
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
        /// <param name="x">The x coordinate</param>
        /// <param name="y">The y coordinate</param>
        /// <param name="z">The z coordinate</param>
        /// <returns>The index in the 1D voxel array</returns>
        private int GetVoxelIndex(int x, int y, int z)
        {
            return z + (y * _width) + (x * _width * _height);
        }

        /// <summary>
        /// Calculates the 1D index for the heightmap array based on padded coordinates. Assumes X-major, then Z layout.
        /// </summary>
        /// <param name="x">The x coordinate</param>
        /// <param name="z">The z coordinate</param>
        /// <returns>The index in the 1D height map array</returns>
        private int GetMapIndex(int x, int z)
        {
            return z + (x * _paddedWidth);
        }
    }
}