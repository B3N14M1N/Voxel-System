using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelSystem.Managers;
using VoxelSystem.Data;
using VoxelSystem.Data.GenerationFlags;

namespace VoxelSystem.Generators
{
    /// <summary>
    /// 
    /// </summary>
    public class ChunkDataGenerator
    {
        public GenerationData GenerationData { get; private set; }
        public NativeArray<Voxel> Voxels;
        public NativeArray<HeightMap> HeightMap;
        private NativeArray<NoiseParameters> noiseParameters;
        private NativeArray<Vector2Int> octaveOffsets;
        private JobHandle jobHandle;
        public bool IsComplete => jobHandle.IsCompleted;
        private readonly IChunksManager _chunksManager;

        public ChunkDataGenerator(GenerationData generationData,
            NoiseParameters[] noiseParameters,
            Vector2Int[] octaveOffsets,
            float globalScale,
            IChunksManager chunksManager)
        {
            GenerationData = generationData;
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
                globalScale = globalScale,
            };
            jobHandle = dataJob.Schedule(HeightMap.Length, 1);
            _chunksManager = chunksManager;
        }


        public GenerationData Complete()
        {
            if (IsComplete)
            {
                jobHandle.Complete();
                Chunk chunk = _chunksManager?.GetChunk(GenerationData.position);
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

        [BurstCompile]
        internal struct ChunkDataJob : IJobParallelFor
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

                HeightMap heightMap = new() { data = 0 };
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

                heightMap.SetSolid((uint)height);
                heightMaps[index] = heightMap;
                Voxel grass = new() { ID = (ushort)VoxelType.grass };
                Voxel dirt = new() { ID = (ushort)VoxelType.dirt };
                Voxel stone = new() { ID = (ushort)VoxelType.stone };
                Voxel sand = new() { ID = (ushort)VoxelType.sand };

                for (int y = 0; y < chunkHeight; y++)
                {
                    Voxel voxel = Voxel.EmptyVoxel;
                    int voxelIndex = GetVoxelIndex(x, y, z);

                    if (y >= height)
                    {
                        voxels[voxelIndex] = voxel;
                        continue;
                    }

                    voxel = stone;

                    if (height >= chunkHeight * 0.8f) // if not at the peak (stone)
                    {
                        voxels[voxelIndex] = voxel;
                        continue;
                    }

                    if (height > chunkHeight * 0.2f) // if not at the bottom place grass/dirt
                    {
                        if (y >= height - 3)
                            voxel = dirt;
                        if (y == height - 1)
                            voxel = grass;
                    }
                    else // place sand on top layer then place stone
                    {
                        if (y == height - 1)
                            voxel = sand;
                    }

                    voxels[voxelIndex] = voxel;
                }
            }


            private readonly float GetHeight(int x, int z, NoiseParameters param)
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
    }
}