using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine;
using System.Threading;
using VoxelSystem.Managers;

public class ChunkMeshGenerator
{
    public GenerationData GenerationData { get; private set; }
    private JobHandle jobHandle;
    private MeshDataStruct meshData;
    public bool IsComplete => jobHandle.IsCompleted;
    private readonly IChunksManager _chunksManager;

    public ChunkMeshGenerator(GenerationData generationData,
        ref NativeArray<Voxel> voxels,
        ref NativeArray<HeightMap> map,
        IChunksManager chunksManager)
    {
        _chunksManager = chunksManager;
        GenerationData = generationData;
        meshData.Initialize();

        var meshJob = new ChunkMeshJob()
        {
            chunkWidth = WorldSettings.ChunkWidth,
            chunkHeight = WorldSettings.ChunkHeight,
            voxels = voxels,
            heightMaps = map,
            meshData = meshData
        };

        jobHandle = meshJob.Schedule();
    }

    public GenerationData Complete()
    {
        if (IsComplete)
        {
            jobHandle.Complete();
            Chunk chunk = _chunksManager?.GetChunk(GenerationData.position);
            if (chunk != null)
            {
                var mesh = meshData.GenerateMesh();
                //Debug.Log(mesh.vertexCount);
                chunk.UploadMesh(ref mesh);
            }
            else
            {
                Dispose();
                return null;
            }
            GenerationData.flags &= ChunkGenerationFlags.Data | ChunkGenerationFlags.Collider;
            Dispose();
        }
        return GenerationData;
    }

    public void Dispose()
    {
        jobHandle.Complete();
        meshData.Dispose();
    }
}

[Serializable]
public struct MeshDataStruct
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
        Initialized = true;
        count = new NativeArray<int>(2, Allocator.Persistent);
        //divide by 2 -> cant be more vertices & faces than half of the Voxels.
        vertices = new NativeArray<float3>(WorldSettings.RenderedVoxelsInChunk * 6 * 4 / 2, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        indices = new NativeArray<int>(WorldSettings.RenderedVoxelsInChunk * 6 * 6 / 2, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        count[0] = count[1] = 0;
    }
    public void Dispose()
    {
        Initialized = false;
        if (vertices.IsCreated) vertices.Dispose();
        if (indices.IsCreated) indices.Dispose();
        if (count.IsCreated) count.Dispose();
    }

    public void UploadToMesh(ref Mesh mesh)
    {
        if (Initialized && mesh != null)
        {
            var flags = MeshUpdateFlags.DontRecalculateBounds & MeshUpdateFlags.DontValidateIndices & MeshUpdateFlags.DontNotifyMeshUsers;
            mesh.SetVertices(vertices.Reinterpret<Vector3>(), 0, count[0], flags);
            mesh.SetIndices(indices, 0, count[1], MeshTopology.Triangles, 0, false);
            mesh.bounds = WorldSettings.ChunkBounds;
        }
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
public struct ChunkMeshJob : IJob
{
    #region Input

    [ReadOnly]
    public int chunkWidth;
    [ReadOnly]
    public int chunkHeight;
    [ReadOnly]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<Voxel> voxels;
    [ReadOnly]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<HeightMap> heightMaps;
    #endregion

    #region Output
    [NativeDisableContainerSafetyRestriction]
    public MeshDataStruct meshData;
    #endregion

    #region Methods

    private readonly int GetVoxelIndex(int x, int y, int z)
    {
        return z + (y * (chunkWidth + 2)) + (x * (chunkWidth + 2) * chunkHeight);
    }

    private readonly int GetMapIndex(int x, int z)
    {
        return z + (x * (chunkWidth + 2));
    }
    #endregion

    #region Execution
    public float3 PackVertexData(float3 position, int normalIndex, int uvIndex, int id)
    {
        int x = uvIndex;
        x <<= 3;
        x += normalIndex & 0x7;
        x <<= 8;
        x += (byte)position.z;
        x <<= 8;
        x += (byte)position.y;
        x <<= 8;
        x += (byte)position.x;

        float y = id / (chunkHeight + 1.0f);
        float z = 0;
        return new float3(BitConverter.Int32BitsToSingle(x), y, z);
    }

    public void Execute()
    {
        #region Allocations
        NativeArray<float3> Vertices = new NativeArray<float3>(8, Allocator.Temp)
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

        NativeArray<float3> FaceCheck = new NativeArray<float3>(6, Allocator.Temp)
        {
            [0] = new float3(0, 0, -1), //back 0
            [1] = new float3(1, 0, 0), //right 1
            [2] = new float3(0, 0, 1), //front 2
            [3] = new float3(-1, 0, 0), //left 3
            [4] = new float3(0, 1, 0), //top 4
            [5] = new float3(0, -1, 0) //bottom 5
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

        NativeArray<float2> VerticeUVs = new NativeArray<float2>(5, Allocator.Temp)
        {
            [0] = new float2(0, 0),
            [1] = new float2(1, 0),
            [2] = new float2(1, 1),
            [3] = new float2(0, 1)
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
        #endregion

        #region Execution

        for (int x = 1; x <= chunkWidth; x++)
        {
            for (int z = 1; z <= chunkWidth; z++)
            {
                int maxHeight = (int)(RWStructs.GetSolid(heightMaps[GetMapIndex(x, z)])) - 1;


                for (int y = maxHeight; y >= 0; y--)
                //for (int y = chunkHeight - 1; y >= 0; y--)
                {
                    Voxel voxel = voxels[GetVoxelIndex(x, y, z)];

                    if (voxel.IsEmpty)
                        continue;

                    float3 voxelPos = new float3(x - 1, y, z - 1);

                    bool surrounded = true;

                    for (int i = 0; i < 6; i++)
                    {
                        float3 face = FaceCheck[i];

                        if (!(y == chunkHeight - 1 && i == 4)) // highest and top face
                        {
                            if (y == 0 && i == 5) // lowest and bottom face
                                continue;
                            int faceCheckIndex = GetVoxelIndex(x + (int)face.x, y + (int)face.y, z + (int)face.z);
                            if (RWStructs.GetVoxelType(voxels[faceCheckIndex]) != 0)
                                continue;
                        }
                        surrounded = false;
                        for (int j = 0; j < 4; j++)
                        {
                            meshData.vertices[meshData.count[0] + j] = PackVertexData(Vertices[FaceVerticeIndex[i * 4 + j]] + voxelPos, i, j, y);
                        }
                        for (int k = 0; k < 6; k++)
                        {
                            meshData.indices[meshData.count[1] + k] = meshData.count[0] + FaceIndices[k];
                        }
                        meshData.count[0] += 4;
                        meshData.count[1] += 6;
                    }
                    if (surrounded)
                        y = -1;
                }
            }
        }
        #endregion

        #region Deallocations

        if (Vertices.IsCreated) Vertices.Dispose();
        if (FaceCheck.IsCreated) FaceCheck.Dispose();
        if (FaceVerticeIndex.IsCreated) FaceVerticeIndex.Dispose();
        if (VerticeUVs.IsCreated) VerticeUVs.Dispose();
        if (FaceIndices.IsCreated) FaceIndices.Dispose();
        #endregion
    }
    #endregion
}

public class ChunkParallelMeshGenerator
{
    public GenerationData GenerationData { get; private set; }
    private JobHandle jobHandle;
    private MeshDataStruct meshData;
    public bool IsComplete => jobHandle.IsCompleted;
    private readonly IChunksManager _chunksManager;

    #region Allocations
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    static readonly NativeArray<float3> Vertices = new(8, Allocator.Persistent)
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

    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    static readonly NativeArray<float3> FaceCheck = new(6, Allocator.Persistent)
    {
        [0] = new float3(0, 0, -1), //back 0
        [1] = new float3(1, 0, 0), //right 1
        [2] = new float3(0, 0, 1), //front 2
        [3] = new float3(-1, 0, 0), //left 3
        [4] = new float3(0, 1, 0), //top 4
        [5] = new float3(0, -1, 0) //bottom 5
    };

    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    static readonly NativeArray<int> FaceVerticeIndex = new(24, Allocator.Persistent)
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

    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    static readonly NativeArray<float2> VerticeUVs = new(5, Allocator.Persistent)
    {
        [0] = new float2(0, 0),
        [1] = new float2(1, 0),
        [2] = new float2(1, 1),
        [3] = new float2(0, 1)
    };

    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    static readonly NativeArray<int> FaceIndices = new(6, Allocator.Persistent)
    {
        [0] = 0,
        [1] = 3,
        [2] = 2,
        [3] = 0,
        [4] = 2,
        [5] = 1
    };
    #endregion

    public ChunkParallelMeshGenerator(GenerationData generationData,
        ref NativeArray<Voxel> voxels,
        ref NativeArray<HeightMap> map,
        IChunksManager chunksManager)
    {
        _chunksManager = chunksManager;
        GenerationData = generationData;
        meshData.Initialize();

        var meshJob = new ChunkParallelMeshJob()
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
            meshData = meshData
        };

        jobHandle = meshJob.Schedule(map.Length, WorldSettings.ChunkWidth);
    }

    public GenerationData Complete()
    {
        if (IsComplete)
        {
            jobHandle.Complete();
            Chunk chunk = _chunksManager?.GetChunk(GenerationData.position);
            if (chunk != null)
            {
                var mesh = meshData.GenerateMesh();
                chunk.UploadMesh(ref mesh);
            }
            else
            {
                Dispose();
                return null;
            }
            GenerationData.flags &= ChunkGenerationFlags.Data | ChunkGenerationFlags.Collider;
            Dispose();
        }
        return GenerationData;
    }

    #region Deallocations
    public void Dispose()
    {
        jobHandle.Complete();
        meshData.Dispose();
    }
    public static void DisposeAll()
    {
        if (Vertices != null && Vertices.IsCreated) Vertices.Dispose();
        if (FaceCheck != null && FaceCheck.IsCreated) FaceCheck.Dispose();
        if (FaceVerticeIndex != null && FaceVerticeIndex.IsCreated) FaceVerticeIndex.Dispose();
        if (VerticeUVs != null && VerticeUVs.IsCreated) VerticeUVs.Dispose();
        if (FaceIndices != null && FaceIndices.IsCreated) FaceIndices.Dispose();
    }
    #endregion
}

[BurstCompile]
public struct ChunkParallelMeshJob : IJobParallelFor
{
    #region Input
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<float3> Vertices;
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<float3> FaceCheck;
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> FaceVerticeIndex;
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<float2> VerticeUVs;
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> FaceIndices;
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public int chunkWidth;
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public int chunkHeight;
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<Voxel> voxels;
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<HeightMap> heightMaps;
    #endregion

    #region Output
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public MeshDataStruct meshData;
    #endregion

    #region Methods
    private readonly int GetVoxelIndex(int x, int y, int z)
    {
        return z + (y * (chunkWidth + 2)) + (x * (chunkWidth + 2) * chunkHeight);
    }

    private readonly int GetMapIndex(int x, int z)
    {
        return z + (x * (chunkWidth + 2));
    }

    public readonly float3 PackVertexData(float3 position, int normalIndex, int uvIndex, int heigth, ushort id)
    {
        uint x = (uint)uvIndex;
        x <<= 3;
        x += (byte)(normalIndex & 0x7);
        x <<= 8;
        x += (byte)position.z;
        x <<= 8;
        x += (byte)position.y;
        x <<= 8;
        x += (byte)position.x;

        return new float3(BitConverter.Int32BitsToSingle((int)x), (float)heigth/chunkHeight, id);
    }

    #endregion

    #region Execution
    public unsafe void Execute(int index)
    {
        int x = index / (chunkWidth + 2);
        int z = index % (chunkWidth + 2);
        if (x >= 1 && z >= 1 && x <= chunkWidth && z <= chunkWidth)
        {
            int maxHeight = (int)(RWStructs.GetSolid(heightMaps[GetMapIndex(x, z)])) - 1;
            for (int y = maxHeight; y >= 0; y--)
            {
                Voxel voxel = voxels[GetVoxelIndex(x, y, z)];

                if (voxel.IsEmpty)
                    continue;

                float3 voxelPos = new(x - 1, y, z - 1);

                bool surrounded = true;

                for (int i = 0; i < 6; i++)
                {
                    float3 face = FaceCheck[i];

                    if (!(y == chunkHeight - 1 && i == 4)) // highest and top face
                    {
                        if (y == 0 && i == 5) // lowest and bottom face
                            continue;
                        int faceCheckIndex = GetVoxelIndex(x + (int)face.x, y + (int)face.y, z + (int)face.z);
                        if (RWStructs.GetVoxelType(voxels[faceCheckIndex]) != 0)
                            continue;
                    }

                    surrounded = false;

                    int verts = Interlocked.Add(ref ((int*)meshData.count.GetUnsafePtr())[0], 4);
                    int tris = Interlocked.Add(ref ((int*)meshData.count.GetUnsafePtr())[1], 6);

                    for (int j = 0; j < 4; j++)
                    {
                        meshData.vertices[verts - 4 + j] = PackVertexData(Vertices[FaceVerticeIndex[i * 4 + j]] + voxelPos, i, j, y, voxel.ID);
                    }
                    for (int k = 0; k < 6; k++)
                    {
                        meshData.indices[tris - 6 + k] = verts - 4 + FaceIndices[k];
                    }
                }
                if (surrounded)
                    y = -1;
            }
        }
    }
    #endregion
}
