using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelSystem.Data.Chunk;
using VoxelSystem.Data.GenerationFlags;
using VoxelSystem.Data.Structs;
using VoxelSystem.Managers;
using VoxelSystem.Settings;

namespace VoxelSystem.Generators
{
    /// <summary>
    /// Mesh generator type options
    /// </summary>
    public enum MeshGeneratorType
    {
        /// <summary>
        /// Single-threaded mesh generator
        /// </summary>
        SingleThreaded,

        /// <summary>
        /// Parallel/multi-threaded mesh generator
        /// </summary>
        Parallel
    }

    /// <summary>
    /// Generates a chunk mesh using a single-threaded approach with Unity's job system and Burst compilation.
    /// </summary>
    public class ChunkMeshGenerator : IChunkMeshGenerator, IDisposable
    {
        /// <summary>
        /// Data describing what is being generated.
        /// </summary>
        public GenerationData GenerationData { get; private set; }

        /// <summary>
        /// Whether the mesh generation job has completed.
        /// </summary>
        public bool IsComplete => jobHandle.IsCompleted;

        private JobHandle jobHandle;
        private MeshDataStruct meshData;
        private readonly IChunksManager _chunksManager;

        /// <summary>
        /// The atlas block index map for the mesh generation.
        /// </summary>
        private static NativeParallelHashMap<int, int> AtlasIndexMap { get; set; }


        /// <summary>
        /// Sets the atlas block index map for the mesh generation.
        /// </summary>
        public static void SetAtlasIndexMap(NativeParallelHashMap<int, int> atlasIndexMap)
        {
            AtlasIndexMap = atlasIndexMap;
        }

        /// <summary>
        /// Static array of cube vertex positions.
        /// </summary>
        private static readonly NativeArray<float3> Vertices = new(8, Allocator.Persistent)
        {
            [0] = new float3(0, 1, 0), //0
            [1] = new float3(1, 1, 0), //1
            [2] = new float3(1, 1, 1), //2
            [3] = new float3(0, 1, 1), //3

            [4] = new float3(0, 0, 0), //4
            [5] = new float3(1, 0, 0), //5
            [6] = new float3(1, 0, 1), //6
            [7] = new float3(0, 0, 1) //7
        };

        /// <summary>
        /// Directions to check for adjacent faces.
        /// </summary>
        private static readonly NativeArray<float3> FaceCheck = new(6, Allocator.Persistent)
        {
            [0] = new float3(0, 0, -1), //back 0
            [1] = new float3(1, 0, 0), //right 1
            [2] = new float3(0, 0, 1), //front 2
            [3] = new float3(-1, 0, 0), //left 3
            [4] = new float3(0, 1, 0), //top 4
            [5] = new float3(0, -1, 0) //bottom 5
        };

        /// <summary>
        /// Indices into the Vertices array for each face.
        /// </summary>
        private static readonly NativeArray<int> FaceVerticeIndex = new(24, Allocator.Persistent)
        {
            [0] = 4,
            [1] = 5,
            [2] = 1,
            [3] = 0,    // back face
            [4] = 5,
            [5] = 6,
            [6] = 2,
            [7] = 1,    // right face
            [8] = 6,
            [9] = 7,
            [10] = 3,
            [11] = 2,  // front face
            [12] = 7,
            [13] = 4,
            [14] = 0,
            [15] = 3,// left face
            [16] = 0,
            [17] = 1,
            [18] = 2,
            [19] = 3,// top face
            [20] = 7,
            [21] = 6,
            [22] = 5,
            [23] = 4,// bottom face
        };

        /// <summary>
        /// UV coordinates for each vertex of a face.
        /// </summary>
        private static readonly NativeArray<float2> VerticeUVs = new(4, Allocator.Persistent)
        {
            [0] = new float2(0, 0),
            [1] = new float2(1, 0),
            [2] = new float2(1, 1),
            [3] = new float2(0, 1)
        };

        /// <summary>
        /// Triangle indices for a quad face.
        /// </summary>
        private static readonly NativeArray<int> FaceIndices = new(6, Allocator.Persistent)
        {
            [0] = 0,
            [1] = 3,
            [2] = 2,
            [3] = 0,
            [4] = 2,
            [5] = 1
        };

        /// <summary>
        /// Creates a new mesh generator for a chunk.
        /// </summary>
        /// <param name="generationData">Data describing what to generate</param>
        /// <param name="voxels">The voxel data array</param>
        /// <param name="map">The height map data array</param>
        /// <param name="chunksManager">The chunks manager</param>
        public ChunkMeshGenerator(
            GenerationData generationData,
            ref NativeArray<Voxel> voxels,
            ref NativeArray<HeightMap> map,
            IChunksManager chunksManager
            )
        {
            _chunksManager = chunksManager;
            GenerationData = generationData;
            var verticesSize = WorldSettings.RenderedVoxelsInChunk * 6 * 4 / 2;
            int indicesSize = WorldSettings.RenderedVoxelsInChunk * 6 * 6 / 2;
            meshData.Initialize(verticesSize, indicesSize);

            MeshGeneratorType type = PlayerSettings.MeshGeneratorType;

            switch (type)
            {
                case MeshGeneratorType.SingleThreaded:
                    var singleThreadedJob = new ChunkMeshJob()
                    {
                        Vertices = Vertices,
                        FaceCheck = FaceCheck,
                        FaceVerticeIndex = FaceVerticeIndex,
                        VerticeUVs = VerticeUVs,
                        FaceIndices = FaceIndices,
                        chunkWidth = WorldSettings.ChunkWidth,
                        chunkHeight = WorldSettings.ChunkHeight,
                        voxels = voxels,
                        heightMaps = map,
                        atlasIndexMap = AtlasIndexMap,
                        meshData = meshData
                    };

                    jobHandle = singleThreadedJob.Schedule();
                    break;

                default: // Default to Parallel
                case MeshGeneratorType.Parallel:
                    var parallelJob = new ChunkParallelMeshJob()
                    {
                        Vertices = Vertices,
                        FaceCheck = FaceCheck,
                        FaceVerticeIndex = FaceVerticeIndex,
                        VerticeUVs = VerticeUVs,
                        FaceIndices = FaceIndices,
                        chunkWidth = WorldSettings.ChunkWidth,
                        chunkHeight = WorldSettings.ChunkHeight,
                        voxels = voxels,
                        heightMaps = map,
                        atlasIndexMap = AtlasIndexMap,
                        meshData = meshData
                    };

                    jobHandle = parallelJob.Schedule(map.Length, WorldSettings.ChunkWidth);
                    break;
            }
        }

        /// <summary>
        /// Completes the mesh generation job and applies the mesh to the chunk.
        /// </summary>
        /// <returns>Updated generation data with new flags</returns>
        public GenerationData Complete()
        {
            if (IsComplete)
            {
                jobHandle.Complete();

                Chunk chunk = _chunksManager?.GetChunk(GenerationData.position);
                if (GenerationData == null)
                    Debug.LogWarning($"ChunkMeshGenerator: GenerationData is null.");

                if (chunk != null)
                {
                    var mesh = meshData.GenerateMesh();
                    chunk.UploadMesh(mesh);
                }
                else
                {
                    Dispose();
                    GenerationData.flags = ChunkGenerationFlags.Disposed;
                    return GenerationData;
                }
                
                GenerationData.flags &= ChunkGenerationFlags.Data | ChunkGenerationFlags.Collider;
                Dispose();
            }
            return GenerationData;
        }

        /// <summary>
        /// Releases resources used by this generator.
        /// </summary>
        public void Dispose()
        {
            jobHandle.Complete();
            meshData.Dispose();
        }

        /// <summary>
        /// Releases all static resources used by the mesh generators.
        /// </summary>
        public static void DisposeAll()
        {
            if (Vertices != null && Vertices.IsCreated) Vertices.Dispose();
            if (FaceCheck != null && FaceCheck.IsCreated) FaceCheck.Dispose();
            if (FaceVerticeIndex != null && FaceVerticeIndex.IsCreated) FaceVerticeIndex.Dispose();
            if (VerticeUVs != null && VerticeUVs.IsCreated) VerticeUVs.Dispose();
            if (FaceIndices != null && FaceIndices.IsCreated) FaceIndices.Dispose();
        }
    }
}