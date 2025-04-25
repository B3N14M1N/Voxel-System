/* ChunkDataJob.cs */
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelSystem.Managers;
using VoxelSystem.Data;
using VoxelSystem.Settings.Generation;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using VoxelSystem.Data.GenerationFlags;

namespace VoxelSystem.Generators
{
    /// <summary>
    /// Handles the generation of voxel terrain data for a chunk using multiple noise layers.
    /// Uses Unity's Job system with Burst compilation for efficient parallel processing.
    /// </summary>
    public class ChunkDataGenerator : IDisposable
    {
        /// <summary>
        /// Data describing what is being generated.
        /// </summary>
        public GenerationData GenerationData { get; private set; }
        
        /// <summary>
        /// The voxel data array for the chunk.
        /// </summary>
        public NativeArray<Voxel> Voxels;
        
        /// <summary>
        /// The height map data array for the chunk.
        /// </summary>
        public NativeArray<HeightMap> HeightMap;

        private NativeArray<int2> octaveOffsets;
        private NativeArray<NoiseLayerParametersJob> noiseLayers;

        private JobHandle jobHandle;
        
        /// <summary>
        /// Whether the generation job has completed.
        /// </summary>
        public bool IsComplete => jobHandle.IsCompleted;
        
        private readonly IChunksManager _chunksManager;

        /// <summary>
        /// Job-safe version of noise layer parameters.
        /// </summary>
        [BurstCompile]
        public struct NoiseLayerParametersJob
        {
            /// <summary>
            /// Whether this noise layer is enabled.
            /// </summary>
            public bool enabled;
            
            /// <summary>
            /// Parameters controlling the noise generation.
            /// </summary>
            public NoiseParameters noise;
            
            /// <summary>
            /// The weight/influence of this layer.
            /// </summary>
            public float weight;
            
            /// <summary>
            /// Whether this layer should act as a mask for subsequent layers.
            /// </summary>
            public bool useAsMask;
            
            /// <summary>
            /// The threshold value for masking.
            /// </summary>
            public float maskThreshold;
        }

        /// <summary>
        /// Creates a new chunk data generator for the specified chunk position.
        /// </summary>
        /// <param name="generationData">Data about what to generate</param>
        /// <param name="generationParams">Parameters controlling terrain generation</param>
        /// <param name="chunksManager">Manager that handles the chunks</param>
        public ChunkDataGenerator(GenerationData generationData,
            GenerationParameters generationParams,
            IChunksManager chunksManager)
        {
            GenerationData = generationData;
            _chunksManager = chunksManager;

            // --- Prepare Data for the Job ---

            // 1. Octave Offsets (ensure they are generated in GenerationParameters)
            if (generationParams.OctaveOffsets == null || generationParams.OctaveOffsets.Length == 0)
            {
                Debug.LogError("Octave Offsets not generated in GenerationParameters!");
                generationParams.GenerateOctaveOffsets(10);
            }

            // Convert Vector2Int[] to int2[]
            int numOffsets = generationParams.OctaveOffsets.Length;
            var octaveOffsetsInt2 = new Unity.Mathematics.int2[numOffsets];

            for (int i = 0; i < numOffsets; i++)
            {
                octaveOffsetsInt2[i] = new Unity.Mathematics.int2(
                    generationParams.OctaveOffsets[i].x,
                    generationParams.OctaveOffsets[i].y
                );
            }

            octaveOffsets = new NativeArray<int2>(octaveOffsetsInt2, Allocator.Persistent);

            // 2. Noise Layer Parameters (convert to job-safe struct)
            var layers = new List<NoiseLayerParametersJob>();
            if (generationParams.ContinentalNoise?.enabled ?? false) layers.Add(CreateJobLayer(generationParams.ContinentalNoise));
            if (generationParams.MountainNoise?.enabled ?? false) layers.Add(CreateJobLayer(generationParams.MountainNoise));
            if (generationParams.HillNoise?.enabled ?? false) layers.Add(CreateJobLayer(generationParams.HillNoise));
            if (generationParams.DetailNoise?.enabled ?? false) layers.Add(CreateJobLayer(generationParams.DetailNoise));
            noiseLayers = new NativeArray<NoiseLayerParametersJob>(layers.ToArray(), Allocator.Persistent);

            // 3. Allocate Voxel and HeightMap Arrays
            int paddedWidth = WorldSettings.ChunkWidth + 2;
            int paddedHeight = WorldSettings.ChunkHeight;
            int paddedDepth = WorldSettings.ChunkWidth + 2;
            Voxels = new NativeArray<Voxel>(paddedWidth * paddedHeight * paddedDepth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            HeightMap = new NativeArray<HeightMap>(paddedWidth * paddedDepth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // --- Schedule the Job ---
            var dataJob = new ChunkDataJob()
            {
                // Chunk settings
                chunkWidth = WorldSettings.ChunkWidth,
                chunkHeight = WorldSettings.ChunkHeight,
                chunkPos = GenerationData.position,

                // General generation settings
                globalScale = generationParams.GlobalScale,
                maxHeight = WorldSettings.ChunkHeight,
                seaLevel = generationParams.SeaLevel,
                steepnessExponent = generationParams.SteepnessExponent,
                mountainThreshold = generationParams.MountainThreshold,

                // Biome/Voxel settings
                dirtDepth = generationParams.DirtDepth,
                sandMargin = generationParams.SandMargin,

                // Noise data
                noiseLayers = noiseLayers,
                octaveOffsets = octaveOffsets,

                // Output arrays
                voxels = Voxels,
                heightMaps = HeightMap,
            };

            // Schedule the job to run in parallel
            jobHandle = dataJob.Schedule(HeightMap.Length, 32);
        }

        /// <summary>
        /// Creates a job-compatible version of noise layer parameters.
        /// </summary>
        /// <param name="layer">The source noise layer parameters</param>
        /// <returns>A job-compatible struct with the same values</returns>
        private NoiseLayerParametersJob CreateJobLayer(NoiseLayerParameters layer)
        {
            return new NoiseLayerParametersJob
            {
                enabled = layer.enabled,
                noise = layer.noise,
                weight = layer.weight,
                useAsMask = layer.useAsMask,
                maskThreshold = layer.maskThreshold
            };
        }

        /// <summary>
        /// Completes the generation job and applies the data to the chunk.
        /// </summary>
        /// <returns>Updated generation data with new flags</returns>
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
                return GenerationData;
            }
            return GenerationData;
        }

        /// <summary>
        /// Releases resources used by this generator.
        /// </summary>
        public void Dispose()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources used by this generator.
        /// </summary>
        /// <param name="disposingAll">Whether to also dispose data arrays</param>
        public void Dispose(bool disposingAll)
        {
            if (jobHandle.IsCompleted)
                jobHandle.Complete();

            if (octaveOffsets.IsCreated) octaveOffsets.Dispose();
            if (noiseLayers.IsCreated) noiseLayers.Dispose();

            if (disposingAll)
            {
                if (Voxels.IsCreated) Voxels.Dispose();
                if (HeightMap.IsCreated) HeightMap.Dispose();
            }
        }

        /// <summary>
        /// Finalizer to ensure resources are released.
        /// </summary>
        ~ChunkDataGenerator()
        {
            Dispose(true);
        }

        /// <summary>
        /// The job that generates chunk terrain data in parallel.
        /// </summary>
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
        internal struct ChunkDataJob : IJobParallelFor
        {
            // --- Input Settings (Read Only) ---
            [ReadOnly] public int chunkWidth;
            [ReadOnly] public int chunkHeight;
            [ReadOnly] public float3 chunkPos;

            [ReadOnly] public float globalScale;
            [ReadOnly] public int maxHeight;
            [ReadOnly] public float seaLevel;
            [ReadOnly] public float steepnessExponent;
            [ReadOnly] public float mountainThreshold;

            [ReadOnly] public int dirtDepth;
            [ReadOnly] public int sandMargin;

            // Noise Data (Read Only)
            [ReadOnly] public NativeArray<NoiseLayerParametersJob> noiseLayers;
            [ReadOnly] public NativeArray<int2> octaveOffsets;

            // --- Output Data (Write Only) ---
            [WriteOnly]
            [NativeDisableParallelForRestriction]
            public NativeArray<Voxel> voxels;

            [WriteOnly]
            [NativeDisableParallelForRestriction]
            public NativeArray<HeightMap> heightMaps;

            /// <summary>
            /// Generates voxel data for one column of the chunk.
            /// </summary>
            /// <param name="index">Index of the column to process</param>
            public void Execute(int index)
            {
                // Calculate local (padded) coordinates within the chunk
                int paddedChunkWidth = chunkWidth + 2;
                int x = index / paddedChunkWidth;
                int z = index % paddedChunkWidth;

                // Calculate world coordinates for noise sampling
                float worldX = (chunkPos.x * chunkWidth) + x - 1;
                float worldZ = (chunkPos.z * chunkWidth) + z - 1;

                // --- Calculate Final Terrain Height ---
                float totalHeight = 0;
                float weightSum = 0;
                float maskModifier = 1.0f;

                for (int i = 0; i < noiseLayers.Length; i++)
                {
                    NoiseLayerParametersJob layer = noiseLayers[i];
                    if (!layer.enabled) continue;

                    float noiseValue = GetNormalizedNoise(worldX, worldZ, layer.noise);

                    if (layer.useAsMask)
                    {
                        maskModifier = math.smoothstep(layer.maskThreshold - 0.1f, layer.maskThreshold + 0.1f, noiseValue);
                    }

                    totalHeight += noiseValue * layer.weight * maskModifier;
                    weightSum += math.abs(layer.weight * maskModifier);
                }

                totalHeight = math.saturate(totalHeight);

                // --- Terrain Shaping ---
                float mountainInfluence = math.smoothstep(mountainThreshold - 0.1f, mountainThreshold + 0.1f, totalHeight);
                float shapedHeight = math.pow(totalHeight, 1.0f + mountainInfluence * (steepnessExponent - 1.0f));

                int terrainHeight = (int)math.floor(shapedHeight * maxHeight);
                terrainHeight = math.clamp(terrainHeight, 1, chunkHeight - 1);

                // --- Set HeightMap ---
                HeightMap heightMap = new((byte)terrainHeight);
                heightMaps[index] = heightMap;

                // --- Voxel Placement ---
                Voxel grass = new((byte)VoxelType.grass);
                Voxel dirt = new((byte)VoxelType.dirt);
                Voxel stone = new((byte)VoxelType.stone);
                Voxel sand = new((byte)VoxelType.sand);
                Voxel air = Voxel.Empty;

                for (int y = 0; y < chunkHeight; y++)
                {
                    int voxelIndex = GetVoxelIndex(x, y, z, paddedChunkWidth, chunkHeight);
                    Voxel voxelToPlace = air;

                    if (y >= terrainHeight)
                    {
                        voxels[voxelIndex] = voxelToPlace;
                        continue;
                    }

                    voxelToPlace = stone;

                    if (terrainHeight >= chunkHeight * 0.8f)
                    {
                        voxels[voxelIndex] = voxelToPlace;
                        continue;
                    }

                    if (terrainHeight > chunkHeight * seaLevel)
                    {
                        if (y >= terrainHeight - dirtDepth)
                            voxelToPlace = dirt;
                        if (y == terrainHeight - 1)
                            voxelToPlace = grass;
                    }
                    else
                    {
                        if (y >= terrainHeight - sandMargin)
                            voxelToPlace = sand;
                    }

                    voxels[voxelIndex] = voxelToPlace;
                }
            }

            /// <summary>
            /// Calculates normalized noise value using multiple octaves.
            /// </summary>
            private readonly float GetNormalizedNoise(float x, float z, NoiseParameters param)
            {
                if (param.noiseScale <= 0) param.noiseScale = 0.0001f;

                float total = 0;
                float frequency = 1;
                float amplitude = 1;
                float maxValue = 0;

                for (int i = 0; i < param.octaves; i++)
                {
                    float sampleX = (x + octaveOffsets[i].x) * frequency / param.noiseScale / globalScale;
                    float sampleZ = (z + octaveOffsets[i].y) * frequency / param.noiseScale / globalScale;

                    float noiseValue = noise.cnoise(new float2(sampleX, sampleZ));

                    total += noiseValue * amplitude;
                    maxValue += amplitude;

                    amplitude *= param.persistence;
                    frequency *= param.lacunarity;
                }

                if (maxValue <= 0) return 0.5f;
                return (total / maxValue + 1.0f) * 0.5f;
            }

            /// <summary>
            /// Calculates the index in the voxel array from 3D coordinates.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private readonly int GetVoxelIndex(int x, int y, int z, int width, int height)
            {
                return z + (y * width) + (x * width * height);
            }
        }
    }
}