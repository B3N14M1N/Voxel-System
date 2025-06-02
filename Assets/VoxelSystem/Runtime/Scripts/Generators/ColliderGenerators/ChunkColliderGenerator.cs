using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using VoxelSystem.Data.Chunk;
using VoxelSystem.Data.GenerationFlags;
using VoxelSystem.Data.Structs;
using VoxelSystem.Managers;
using VoxelSystem.Settings;

namespace VoxelSystem.Generators
{
    /// <summary>
    /// Generates simplified collision meshes for chunks based on height map data.
    /// </summary>
    public class ChunkColliderGenerator: IDisposable
    {
        /// <summary>
        /// Data describing what is being generated.
        /// </summary>
        public GenerationData GenerationData { get; private set; }

        /// <summary>
        /// The mesh data structure for the collider.
        /// </summary>
        public MeshDataStruct colliderData;

        private JobHandle jobHandle;

        /// <summary>
        /// Whether the collider generation job has completed.
        /// </summary>
        public bool IsComplete => jobHandle.IsCompleted;

        private readonly IChunksManager _chunksManager;

        /// <summary>
        /// Creates a new collider generator for a chunk.
        /// </summary>
        /// <param name="generationData">Data describing what to generate</param>
        /// <param name="heightMap">The height map data array</param>
        /// <param name="chunksManager">The chunks manager</param>
        public ChunkColliderGenerator(GenerationData generationData,
            ref NativeArray<HeightMap> heightMap,
            IChunksManager chunksManager)
        {
            GenerationData = generationData;
            var verticesSize = WorldSettings.ChunkWidth * 4 * (WorldSettings.ChunkWidth + 4);
            int indicesSize = WorldSettings.ChunkWidth * (WorldSettings.ChunkWidth * 3 + 2) * 6;
            colliderData.Initialize(verticesSize, indicesSize);
            _chunksManager = chunksManager;
            var dataJob = new ChunkColliderJob()
            {
                chunkWidth = WorldSettings.ChunkWidth,
                chunkHeight = WorldSettings.ChunkHeight,
                heightMap = heightMap,
                colliderData = colliderData,
            };
            jobHandle = dataJob.Schedule();
        }

        /// <summary>
        /// Completes the collider generation job and applies the collider to the chunk.
        /// </summary>
        /// <returns>Updated generation data with new flags</returns>
        public GenerationData Complete()
        {
            if (IsComplete)
            {
                jobHandle.Complete();
                Chunk chunk = _chunksManager?.GetChunk(GenerationData.position);
                if (GenerationData == null)
                    Debug.LogWarning($"ChunkColliderGenerator: GenerationData is null.");
                    
                if (chunk != null)
                {
                    var collider = colliderData.GenerateMesh();
                    chunk.UploadCollider(collider);
                }
                else
                {
                    Dispose();
                    GenerationData.flags = ChunkGenerationFlags.Disposed;
                    return GenerationData;
                }
                Dispose();
                GenerationData.flags &= ChunkGenerationFlags.Mesh | ChunkGenerationFlags.Data;
            }
            return GenerationData;
        }

        /// <summary>
        /// Releases resources used by this generator.
        /// </summary>
        public void Dispose()
        {
            jobHandle.Complete();
            colliderData.Dispose();
        }
    }
}