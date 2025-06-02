using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelSystem.Managers;
using VoxelSystem.Data.GenerationFlags;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using VoxelSystem.Settings.Generation;
using VoxelSystem.Settings;
using VoxelSystem.Data.Structs;
using VoxelSystem.Data.Chunk;

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
            int width = WorldSettings.ChunkWidth;
            int height = WorldSettings.ChunkHeight;
            int paddedWidth = WorldSettings.ChunkWidth + 2;
            Voxels = new NativeArray<Voxel>(width * height * width, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            HeightMap = new NativeArray<HeightMap>(paddedWidth * paddedWidth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // --- Schedule the Job ---
            var dataJob = new ChunkDataJob()
            {
                // Chunk settings
                chunkWidth = width,
                chunkHeight = height,
                chunkPos = GenerationData.position,

                // General generation settings
                globalScale = generationParams.GlobalScale,
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
            jobHandle = dataJob.Schedule(HeightMap.Length, WorldSettings.ChunkWidth);
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
                if (GenerationData == null)
                    Debug.LogWarning($"ChunkDataGenerator: GenerationData is null.");

                if (chunk != null)
                {
                    chunk.UploadData(ref Voxels, ref HeightMap);
                }
                else
                {
                    Dispose(true);
                    GenerationData.flags = ChunkGenerationFlags.Disposed;
                    return GenerationData;
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
    }
}