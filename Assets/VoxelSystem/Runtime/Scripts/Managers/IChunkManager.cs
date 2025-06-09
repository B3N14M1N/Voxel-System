using System;
using UnityEngine;
using VoxelSystem.Data.Chunk;
using VoxelSystem.Data.Structs;

namespace VoxelSystem.Managers
{
    /// <summary>
    /// Interface for chunk management systems that handle chunk lifecycle and operations.
    /// </summary>
    public interface IChunksManager : IDisposable
    {
        /// <summary>
        /// Gets the center position around which chunks are generated.
        /// </summary>
        Vector3 Center { get; }

        /// <summary>
        /// Checks which chunk positions need to be generated based on the current center.
        /// </summary>
        void GenerateChunksPositionsCheck();

        /// <summary>
        /// Updates the visible chunks around a new center position.
        /// </summary>
        /// <param name="center">The new center position</param>
        void UpdateChunks(Vector3 center);

        /// <summary>
        /// Retrieves a chunk at the specified position.
        /// </summary>
        /// <param name="pos">The position to get the chunk from</param>
        /// <returns>The chunk at the specified position, or null if none exists</returns>
        Chunk GetChunk(Vector3 pos);

        /// <summary>
        /// Marks a chunk position as being generated.
        /// </summary>
        /// <param name="pos">The position of the chunk</param>
        void SetChunkToGenerating(Vector3 pos);

        /// <summary>
        /// Marks a generating chunk as complete.
        /// </summary>
        /// <param name="pos">The position of the chunk</param>
        void CompleteGeneratingChunk(Vector3 pos);

        /// <summary>
        /// Gets statistics about chunk mesh and collider sizes.
        /// </summary>
        /// <returns>A tuple containing vertex and index counts for meshes and colliders</returns>
        (int, int, int, int, int) ChunksMeshAndColliderSize();

        /// <summary>
        /// Updates mesh size statistics when a chunk mesh changes.
        /// </summary>
        /// <param name="mV">Change in vertex count</param>
        /// <param name="mI">Change in index count</param>
        void UpdateChunkMeshSize(int mV, int mI);

        /// <summary>
        /// Updates collider size statistics when a chunk collider changes.
        /// </summary>
        /// <param name="cV">Change in vertex count</param>
        /// <param name="cI">Change in index count</param>
        void UpdateChunkColliderSize(int cV, int cI);

        /// <summary>
        /// Modifies a voxel at the specified world coordinates.
        /// </summary>
        /// <param name="x">The x coordinate in world space</param>
        /// <param name="y">The y coordinate in world space</param>
        /// <param name="z">The z coordinate in world space</param>
        /// <param name="voxel">The voxel to place</param>
        /// <returns>True if the voxel was modified successfully</returns>
        bool ModifyVoxel(int x, int y, int z, Voxel voxel);

        /// <summary>
        /// Modifies a voxel at the specified world position.
        /// </summary>
        /// <param name="worldPos">The position in world space</param>
        /// <param name="voxel">The voxel to place</param>
        /// <returns>True if the voxel was modified successfully</returns>
        bool ModifyVoxel(Vector3 worldPos, Voxel voxel);

        /// <summary>
        /// Retrieves a voxel at the specified world coordinates.
        /// </summary>
        /// <param name="x">The x coordinate in world space</param>
        /// <param name="y">The y coordinate in world space</param>
        /// <param name="z">The z coordinate in world space</param>
        /// <returns>The voxel at the specified coordinates</returns>
        Voxel GetVoxel(int x, int y, int z);

        /// <summary>
        /// Retrieves a voxel at the specified world position.
        /// </summary>
        /// <param name="worldPos">The position in world space</param>
        /// <returns>The voxel at the specified position</returns>
        Voxel GetVoxel(Vector3 worldPos);
    }
}