using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


public class ChunkDataGenerator
{
    public GenerationData GenerationData { get; private set; }
    public NativeArray<Voxel> Voxels;
    public NativeArray<HeightMap> HeightMap;
    private NativeArray<NoiseParameters> noiseParameters;
    private NativeArray<Vector2Int> octaveOffsets;
    private float globalScale;
    private JobHandle jobHandle;
    public bool IsComplete => jobHandle.IsCompleted;

    public ChunkDataGenerator(GenerationData generationData,
        NoiseParameters[] noiseParameters,
        Vector2Int[] octaveOffsets,
        float globalScale)
    {
        GenerationData = generationData;
        this.globalScale = globalScale;
        this.octaveOffsets = new NativeArray<Vector2Int>(octaveOffsets, Allocator.Persistent);
        this.noiseParameters = new NativeArray<NoiseParameters>(noiseParameters, Allocator.Persistent);
        Voxels = new NativeArray<Voxel>((WorldSettings.ChunkWidth + 2) * WorldSettings.ChunkHeight * (WorldSettings.ChunkWidth + 2), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        HeightMap = new NativeArray<HeightMap>((WorldSettings.ChunkWidth + 2) * (WorldSettings.ChunkWidth + 2), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var dataJob = new ChunkDataJob()
        {
            chunkWidth = WorldSettings.ChunkWidth,
            chunkHeight = WorldSettings.ChunkHeight,
            voxels = this.Voxels,
            heightMaps = this.HeightMap,
            chunkPos = this.GenerationData.position,
            noiseParameters = this.noiseParameters,
            octaveOffsets = this.octaveOffsets.Reinterpret<int2>(),
            globalScale = this.globalScale,
        };
        jobHandle = dataJob.Schedule(HeightMap.Length, 1);
        //Debug.Log("Scheduled: " + HeightMap.Length);
    }


    public GenerationData Complete()
    {
        if (IsComplete)
        {
            jobHandle.Complete();
            Chunk chunk = ChunksManager.Instance.GetChunk(GenerationData.position);
            if (chunk != null)
            {
                chunk.UploadData(ref Voxels, ref HeightMap);
            }
            else
            {
                Dispose(true);
                return null;
            }
            GenerationData.flags &= ChunkGenerationFlags.Mesh | ChunkGenerationFlags.Collider;
            Dispose();
        }
        return GenerationData;
    }

    public void Dispose(bool disposeData = false)
    {
        jobHandle.Complete();
        if (noiseParameters.IsCreated) noiseParameters.Dispose();
        if (octaveOffsets.IsCreated) octaveOffsets.Dispose();
        if (disposeData)
        {
            if (Voxels.IsCreated) Voxels.Dispose();
            if (HeightMap.IsCreated) HeightMap.Dispose();
        }
    }
}

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
    public NativeArray<NoiseParameters> noiseParameters;
    [ReadOnly]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int2> octaveOffsets;

    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<Voxel> voxels;
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<HeightMap> heightMaps;

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
        for (int i = 0; i < noiseParameters.Length; i++)
        {
            fHeight += GetHeight(sampleX, sampleZ, noiseParameters[i]);
        }
        height = Mathf.FloorToInt(fHeight / noiseParameters.Length * (chunkHeight - 1));

        if (height <= 0)
            height = 1;

        if (height > chunkHeight)
        {
            height = chunkHeight;
        }

        RWStructs.SetSolid(ref heightMap, (uint)height);
        heightMaps[index] = heightMap;
        for (int y = 0; y < chunkHeight; y++)
        {
            int voxelIndex = GetVoxelIndex(x, y, z);
            voxels[voxelIndex] = emptyVoxel;
            if (y < height)
            {
                voxels[voxelIndex] = solid;
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
            float sampleX = (x + octaveOffsets[i].x) / param.noiseScale / globalScale * frequency;
            float sampleZ = (z + octaveOffsets[i].y) / param.noiseScale / globalScale * frequency;

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
