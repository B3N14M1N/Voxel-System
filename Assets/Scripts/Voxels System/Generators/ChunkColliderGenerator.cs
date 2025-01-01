using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Mesh;

public class ChunkColliderGenerator
{
    public GenerationData GenerationData { get; private set; }
    public MeshColliderDataStruct colliderData;
    private JobHandle jobHandle;
    public bool IsComplete => jobHandle.IsCompleted;

    public ChunkColliderGenerator(GenerationData generationData, ref NativeArray<HeightMap> heightMap)
    {
        GenerationData = generationData;
        colliderData.Initialize();
        var dataJob = new ChunkColliderJob()
        {
            chunkWidth = WorldSettings.ChunkWidth,
            chunkHeight = WorldSettings.ChunkHeight,
            heightMaps = heightMap,
            colliderData = colliderData,
        };
        jobHandle = dataJob.Schedule();
    }

    public GenerationData Complete()
    {
        if (IsComplete)
        {
            jobHandle.Complete();
            Chunk chunk = ChunksManager.Instance.GetChunk(GenerationData.position);
            if (chunk != null)
            {
                var collider = colliderData.GenerateMesh();
                chunk.UpdateCollider(ref collider);
            }
            else
            {
                Dispose();
                return null;
            }
            Dispose();
            GenerationData.flags &= ChunkGenerationFlags.Mesh | ChunkGenerationFlags.Data;
        }
        return GenerationData;
    }

    public void Dispose()
    {
        jobHandle.Complete();
        colliderData.Dispose();
    }
}

public struct MeshColliderDataStruct
{
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<float3> vertices;
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> indices;

    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> count;

    public bool Initialized;
    public void Initialize()
    {
        if (Initialized)
            Dispose();

        count = new NativeArray<int>(2, Allocator.Persistent);
        vertices = new NativeArray<float3>(WorldSettings.ChunkWidth * 4 * (WorldSettings.ChunkWidth + 4), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        int size = WorldSettings.ChunkWidth * (WorldSettings.ChunkWidth * 3 + 2);
        indices = new NativeArray<int>(size * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        Initialized = true;
    }

    public void Dispose()
    {
        Initialized = false;
        if (vertices.IsCreated) vertices.Dispose();
        if (indices.IsCreated) indices.Dispose();
        if (count.IsCreated) count.Dispose();
    }

    public Mesh GenerateMesh()
    {
        if (Initialized)
        {
            Mesh mesh = new Mesh() { indexFormat = IndexFormat.UInt32 };
            var flags = MeshUpdateFlags.DontRecalculateBounds & MeshUpdateFlags.DontValidateIndices & MeshUpdateFlags.DontNotifyMeshUsers;
            mesh.SetVertices(vertices.Reinterpret<Vector3>(), 0, count[0], flags);
            mesh.SetIndices(indices, 0, count[1], MeshTopology.Triangles, 0, false);
            mesh.bounds = WorldSettings.ChunkBounds;
            return mesh;
        }
        return null;
    }
}

[BurstCompile]
public struct ChunkColliderJob : IJob
{
    #region Input

    [ReadOnly]
    public int chunkWidth;
    [ReadOnly]
    public int chunkHeight;
    [ReadOnly]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<HeightMap> heightMaps;
    #endregion

    #region Output
    [NativeDisableContainerSafetyRestriction]
    public MeshColliderDataStruct colliderData;
    #endregion

    #region Methods

    private readonly int GetMapIndex(int x, int z)
    {
        return z + (x * (chunkWidth + 2));
    }
    #endregion

    #region Execution

    public void Execute()
    {
        #region Allocations
        NativeArray<float3> Vertices = new NativeArray<float3>(8, Allocator.Temp)
        {
            // top
            [0] = new float3(0, 1, 0), //0
            [1] = new float3(1, 1, 0), //1
            [2] = new float3(1, 1, 1), //2
            [3] = new float3(0, 1, 1), //3

            // bottom
            [4] = new float3(0, 0, 0), //4
            [5] = new float3(1, 0, 0), //5
            [6] = new float3(1, 0, 1), //6
            [7] = new float3(0, 0, 1) //7
        };


        NativeArray<int> FaceVerticeIndex = new NativeArray<int>(24, Allocator.Temp)
        {
            [0] = 4,
            [1] = 5,
            [2] = 1,
            [3] = 0,
            [4] = 5,
            [5] = 6,
            [6] = 2,
            [7] = 1,
            [8] = 6,
            [9] = 7,
            [10] = 3,
            [11] = 2,
            [12] = 7,
            [13] = 4,
            [14] = 0,
            [15] = 3,
            [16] = 0,
            [17] = 1,
            [18] = 2,
            [19] = 3,
            [20] = 7,
            [21] = 6,
            [22] = 5,
            [23] = 4,
        };


        NativeArray<int> FaceIndices = new NativeArray<int>(6, Allocator.Temp)
        {
            [0] = 0,
            [1] = 3,
            [2] = 2,
            [3] = 0,
            [4] = 2,
            [5] = 1
        };

        NativeArray<int> SidesNeighbourVerticesIndices = new NativeArray<int>(8, Allocator.Temp)
        {
            // on z axis
            [0] = -4 * (chunkWidth) + 3,
            [1] = -4 * (chunkWidth) + 2,
            [2] = 4 * (chunkWidth),
            [3] = 4 * (chunkWidth) + 1,

            // on x axis
            [4] = -3,
            [5] = 4,
            [6] = 7,
            [7] = -2,

        };

        NativeArray<int> SideFaceIndices = new NativeArray<int>(24, Allocator.Temp)
        {
            //back
            [0] = SidesNeighbourVerticesIndices[0],
            [1] = 0,
            [2] = 1,
            [3] = SidesNeighbourVerticesIndices[0],
            [4] = 1,
            [5] = SidesNeighbourVerticesIndices[1],

            // right
            [6] = SidesNeighbourVerticesIndices[5],
            [7] = 1,
            [8] = 2,
            [9] = SidesNeighbourVerticesIndices[5],
            [10] = 2,
            [11] = SidesNeighbourVerticesIndices[6],

            //front
            [12] = SidesNeighbourVerticesIndices[1],
            [13] = 0,
            [14] = 0,
            [15] = SidesNeighbourVerticesIndices[1],
            [16] = 0,
            [17] = SidesNeighbourVerticesIndices[0],

            //left
            [18] = 0,
            [19] = SidesNeighbourVerticesIndices[5],
            [20] = SidesNeighbourVerticesIndices[6],
            [21] = 0,
            [22] = SidesNeighbourVerticesIndices[6],
            [23] = 0
        };
        #endregion

        #region Execution

        colliderData.count[0] = 0;
        colliderData.count[1] = 0;

        for (int z = 1; z <= chunkWidth; z++)
        {
            for (int x = 1; x <= chunkWidth; x++)
            {
                float3 voxelPos = new float3(x - 1, (int)(RWStructs.GetSolid(heightMaps[GetMapIndex(x, z)])) - 1, z - 1);

                #region Top Face

                int i = 4; // top face index

                for (int j = 0; j < 4; j++)
                {
                    colliderData.vertices[colliderData.count[0] + j] = Vertices[FaceVerticeIndex[i * 4 + j]] + voxelPos;
                }
                for (int k = 0; k < 6; k++)
                {
                    colliderData.indices[colliderData.count[1] + k] = colliderData.count[0] + FaceIndices[k];
                }
                colliderData.count[0] += 4;
                colliderData.count[1] += 6;
                #endregion

                #region Sides

                // 0 back 1 right 2 front 3 left
                for (i = 0; i < 2; i++)
                {
                    if ((z > 1 && i == 0) || (x < chunkWidth && i == 1))
                    {
                        for (int k = 0; k < 6; k++)
                        {
                            colliderData.indices[colliderData.count[1] + k] = colliderData.count[0] - 4 + SideFaceIndices[i * 6 + k];
                        }
                        colliderData.count[1] += 6;
                    }
                }
                #endregion
            }
        }
        #endregion

        #region Deallocations

        if (Vertices.IsCreated) Vertices.Dispose();
        if (FaceVerticeIndex.IsCreated) FaceVerticeIndex.Dispose();
        if (FaceIndices.IsCreated) FaceIndices.Dispose();
        if(SidesNeighbourVerticesIndices.IsCreated) SidesNeighbourVerticesIndices.Dispose();
        if (SideFaceIndices.IsCreated) SideFaceIndices.Dispose();
        #endregion
    }
    #endregion
}