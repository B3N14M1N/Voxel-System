using Unity.Collections;
using VoxelSystem.Data.Structs;
using UnityEngine;

namespace VoxelSystem.SaveSystem
{
    /// <summary>
    /// Represents the result of an asynchronous chunk data loading operation.
    /// </summary>
    public struct ChunkDataLoadResult
    {
        /// <summary>
        /// Whether the data was loaded successfully.
        /// </summary>
        public bool Success { get; }
        
        /// <summary>
        /// The loaded voxel data, if successful.
        /// </summary>
        public NativeArray<Voxel> Voxels { get; }
        
        /// <summary>
        /// The loaded height map data, if successful.
        /// </summary>
        public NativeArray<HeightMap> HeightMap { get; }
        
        /// <summary>
        /// The position of the chunk that was loaded.
        /// </summary>
        public Vector3 Position { get; }
        
        /// <summary>
        /// Creates a new successful load result.
        /// </summary>
        public ChunkDataLoadResult(Vector3 position, NativeArray<Voxel> voxels, NativeArray<HeightMap> heightMap)
        {
            Position = position;
            Success = true;
            Voxels = voxels;
            HeightMap = heightMap;
        }
        
        /// <summary>
        /// Creates a new failed load result.
        /// </summary>
        public ChunkDataLoadResult(Vector3 position)
        {
            Position = position;
            Success = false;
            Voxels = default;
            HeightMap = default;
        }
    }
}
