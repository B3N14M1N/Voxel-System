using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class JobChunkGenerator
{
    /*
    public static int Processed { get; private set; }
    public NativeArray<Voxel> voxels;
    public NativeArray<HeightMap> heightMap;
    public MeshDataStruct meshData;
    public Vector3 chunkPos;
    private NativeArray<NoiseParameters> _noiseParameters;
    private NativeArray<Vector2Int> _octaveOffsets;
    private float globalScale;

    public bool GenerationStarted { get; private set; }
    public bool DataScheduled { get; private set; }
    public bool MeshScheduled { get; private set; }
    public bool DataGenerated { get; private set; }
    public bool MeshGenerated { get; private set; }

    public JobHandle dataHandle;

    public JobHandle meshHandle;

    public JobChunkGenerator(Vector3 chunkPos, NoiseParameters[] _noiseParameters, Vector2Int[] _octaveOffsets, float globalScale)
    {
        this.chunkPos = chunkPos;
        this.globalScale = globalScale;
        GenerationStarted = false;
        DataGenerated = false;
        MeshGenerated = false;
        this._noiseParameters = new NativeArray<NoiseParameters>(_noiseParameters, Allocator.Persistent);
        this._octaveOffsets = new NativeArray<Vector2Int>(_octaveOffsets, Allocator.Persistent);
        ScheduleDataGeneration();
        Processed += 1;
    }
    public JobChunkGenerator(Vector3 chunkPos, NativeArray<Voxel> voxels, NativeArray<HeightMap> heightMap)
    {
        this.chunkPos = chunkPos;
        GenerationStarted = true;

        DataScheduled = false;
        DataGenerated = true;

        MeshScheduled = false;
        MeshGenerated = false;
        Processed += 1;
        ScheduleMeshGeneration();
        Processed += 1;
    }

    public void ScheduleDataGeneration()
    {
        if (!GenerationStarted && !DataScheduled && !DataGenerated)
        {
            voxels = new NativeArray<Voxel>((WorldSettings.ChunkWidth + 2) * WorldSettings.ChunkHeight * (WorldSettings.ChunkWidth + 2), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            heightMap = new NativeArray<HeightMap>((WorldSettings.ChunkWidth + 2) * (WorldSettings.ChunkWidth + 2), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var dataJob = new ChunkDataJob()
            {
                chunkWidth = WorldSettings.ChunkWidth,
                chunkHeight = WorldSettings.ChunkHeight,
                voxels = this.voxels,
                heightMap = this.heightMap,
                chunkPos = this.chunkPos,
                _noiseParameters = this._noiseParameters,
                _octaveOffsets = this._octaveOffsets.Reinterpret<int2>(),
                globalScale = this.globalScale,
            };
            GenerationStarted = true;
            DataScheduled = true;
            dataHandle = dataJob.Schedule(heightMap.Length, 1);
        }
    }
    public bool CompleteDataGeneration()
    {
        if (GenerationStarted && DataScheduled && !DataGenerated && dataHandle.IsCompleted)
        {
            dataHandle.Complete();
            DataScheduled = false;
            DataGenerated = true;
            return true;
        }
        return false;
    }
    public void ScheduleMeshGeneration()
    {
        if (GenerationStarted && DataGenerated && !MeshScheduled && voxels.IsCreated && heightMap.IsCreated)
        {
            meshData.Initialize();

            var meshJob = new ChunkMeshJob()
            {
                chunkWidth = WorldSettings.ChunkWidth,
                chunkHeight = WorldSettings.ChunkHeight,
                voxels = this.voxels,
                heightMap = this.heightMap,
                meshData = this.meshData
            };
            MeshScheduled = true;
            meshHandle = meshJob.Schedule();
        }
    }
    public bool CompleteMeshGeneration()
    {
        if (GenerationStarted && DataGenerated && MeshScheduled && !MeshGenerated && meshHandle.IsCompleted)
        {
            Processed -= 1;
            meshHandle.Complete();
            MeshScheduled = false;
            MeshGenerated = true;
            return true;
        }
        return false;
    }
    public void SetMesh(ref Mesh mesh)
    {
        if (MeshGenerated && meshData.Initialized)
        {
            if (mesh == null)
                mesh = new Mesh() { indexFormat = IndexFormat.UInt32 };
            meshData.UploadToMesh(ref mesh);
        }
        //return ref mesh;
    }
    public void Dispose(bool disposeData = false)
    {
        dataHandle.Complete();
        meshHandle.Complete();
        meshData.Dispose();
        if (_noiseParameters.IsCreated) _noiseParameters.Dispose();
        if (_octaveOffsets.IsCreated) _octaveOffsets.Dispose();
        if (disposeData)
        {
            if (voxels.IsCreated) voxels.Dispose();
            if (heightMap.IsCreated) heightMap.Dispose();
        }

    }
    */
}

/*
[BurstCompile]
public struct ChunkDataJob : IJobParallelFor
{
    [ReadOnly]
    public float globalScale;
    [ReadOnly]
    public int chunkWidth;
    [ReadOnly]
    public int chunkHeight;
    [ReadOnly]
    public float3 chunkPos;
    [ReadOnly]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<NoiseParameters> _noiseParameters;
    [ReadOnly]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int2> _octaveOffsets;

    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<Voxel> Voxels;
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<HeightMap> HeightMap;

    public void Execute(int index)
    {
        int x = index / (chunkWidth + 2);
        int z = index % (chunkWidth + 2);

        Voxel solid = new Voxel() { ID = 1 };
        Voxel emptyVoxel = Voxel.EmptyVoxel;
        HeightMap heightMap = new HeightMap() { data = 0 };
        int height;

        float fHeight = 0;
        int sampleX = (int)chunkPos.x * chunkWidth + x - 1, sampleZ = (int)chunkPos.z * chunkWidth + z - 1;
        for (int i = 0; i < _noiseParameters.Length; i++)
        {
            fHeight += GetHeight(sampleX, sampleZ, _noiseParameters[i]);
        }
        height = Mathf.FloorToInt(fHeight / _noiseParameters.Length * (chunkHeight - 1));

        if (height <= 0)
            height = 1;

        if (height > chunkHeight)
        {
            height = chunkHeight;
        }

        RWStructs.SetSolid(ref heightMap, (uint)height);
        HeightMap[index] = heightMap;
        for (int y = 0; y < chunkHeight; y++)
        {
            int voxelIndex = GetVoxelIndex(x, y, z);
            Voxels[voxelIndex] = emptyVoxel;
            if (y < height)
            {
                Voxels[voxelIndex] = solid;
            }
        }
    }


    float GetHeight(int x, int z, NoiseParameters param)
    {
        float height = 0;
        float amplitude = 1;
        float frequency = 1;

        float max = 0;
        for (int i = 0; i < param.octaves; i++)
        {
            float sampleX = (x + _octaveOffsets[i].x) / param.noiseScale / globalScale * frequency;
            float sampleZ = (z + _octaveOffsets[i].y) / param.noiseScale / globalScale * frequency;

            float value = Mathf.PerlinNoise(sampleX, sampleZ);
            height += value * amplitude / param.damping;
            max += amplitude;
            amplitude *= -param.persistence;
            frequency *= param.lacunarity;
        }
        height = (Mathf.Pow(Mathf.Abs(height), param.ePow) / Mathf.Pow(Mathf.Abs(max), param.ePow));
        if (height > param.maxHeight)
            height = param.maxHeight;
        return height * param.blending;
    }

    private readonly int GetVoxelIndex(int x, int y, int z)
    {
        return z + (y * (chunkWidth + 2)) + (x * (chunkWidth + 2) * chunkHeight);
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
    public NativeArray<Voxel> Voxels;
    [ReadOnly]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<HeightMap> HeightMap;
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

        float y = id/(chunkHeight + 1.0f);
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
                int maxHeight = (int)(RWStructs.GetSolid(HeightMap[GetMapIndex(x, z)])) - 1;


                for (int y = maxHeight; y >= 0; y--)
                {
                    Voxel voxel = Voxels[GetVoxelIndex(x, y, z)];
                    if (RWStructs.GetVoxelType(voxel) == 0)
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
                            if (RWStructs.GetVoxelType(Voxels[faceCheckIndex]) != 0)
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
*/