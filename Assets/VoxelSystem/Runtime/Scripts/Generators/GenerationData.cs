using UnityEngine;

namespace VoxelSystem.Data.GenerationFlags
{
    /// <summary>
    /// Contains data needed to generate a chunk, including position and generation flags.
    /// </summary>
    public class GenerationData
    {
        /// <summary>
        /// The position of the chunk to generate.
        /// </summary>
        public Vector3 position;

        /// <summary>
        /// Flags indicating which aspects of the chunk to generate.
        /// </summary>
        public ChunkGenerationFlags flags;
    }

    /// <summary>
    /// Flags that control what aspects of a chunk should be generated.
    /// Can be combined using bitwise operations.
    /// </summary>
    public enum ChunkGenerationFlags
    {
        /// <summary>
        /// No generation needed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Generate voxel data.
        /// </summary>
        Data = 1,

        /// <summary>
        /// Generate collider mesh.
        /// </summary>
        Collider = 2,

        /// <summary>
        /// Generate visual mesh.
        /// </summary>
        Mesh = 4,

        /// <summary>
        /// Indicates that the chunk is being disposed of.
        /// </summary>
        Disposed = 8
    };
}