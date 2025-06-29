using System;
using Unity.Collections;
using VoxelSystem.Data.Structs;

namespace VoxelSystem.SaveSystem
{
    /// <summary>
    /// Represents the result of a chunk loading operation.
    /// </summary>
    public struct ChunkLoadResult : IDisposable
    {
        /// <summary>
        /// Whether the load operation was successful.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// The loaded voxel data.
        /// </summary>
        public NativeArray<Voxel> Voxels { get; }

        /// <summary>
        /// The loaded height map data.
        /// </summary>
        public NativeArray<HeightMap> HeightMap { get; }

        /// <summary>
        /// Creates a new successful load result with the provided data.
        /// </summary>
        public ChunkLoadResult(NativeArray<Voxel> voxels, NativeArray<HeightMap> heightMap)
        {
            Success = true;
            Voxels = voxels;
            HeightMap = heightMap;
        }

        /// <summary>
        /// Creates a new failed load result.
        /// </summary>
        public ChunkLoadResult(bool success)
        {
            Success = success;
            Voxels = default;
            HeightMap = default;
        }

        /// <summary>
        /// Disposes the native arrays if they are valid.
        /// </summary>
        public void Dispose()
        {
            if (Voxels.IsCreated)
                Voxels.Dispose();

            if (HeightMap.IsCreated)
                HeightMap.Dispose();
        }
    }
}
