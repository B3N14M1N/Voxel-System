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
using VoxelSystem.SaveSystem;
using Cysharp.Threading.Tasks;


namespace VoxelSystem.Generators
{
    /// <summary>
    /// Handles the generation of voxel terrain data for a chunk using multiple noise layers.
    /// Uses Unity's Job system with Burst compilation for efficient parallel processing.
    /// </summary>
    public class ChunkDataGenerator : IChunkGenerator, IDisposable
    {
        /// <summary>
        /// Data describing what is being generated.
        /// </summary>
        public GenerationData GenerationData { get; private set; }        // Main data arrays that will be passed to the chunk
        public NativeArray<Voxel> Voxels;
        public NativeArray<HeightMap> HeightMap;

        // Job system related fields
        private GenerationParameters _generationParams;
        private NativeArray<int2> octaveOffsets;
        private NativeArray<NoiseLayerParametersJob> noiseLayers;
        private JobHandle jobHandle;

        /// <summary>
        /// Whether the generation job has completed.
        /// </summary>
        public bool IsComplete => (_hasLoaded && _isLoading)  || (_isGenerating && jobHandle.IsCompleted);
        private readonly IChunksManager _chunksManager;

        // Fields for async loading state
        private bool _isLoading = false;
        private bool _isGenerating = false;
        private bool _hasLoaded = false;
        private ChunkLoadResult? _loadResult;

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
            _generationParams = generationParams;
            _hasLoaded = false;

            // First check if there's saved data for this chunk and start loading asynchronously
            if (ChunkSaveSystem.ChunkSaveExists(generationData.position))
            {
                if (WorldSettings.HasDebugging)
                    Debug.Log($"Found saved data for chunk at {generationData.position}. Starting async loading.");

                _isLoading = true;
                _isGenerating = false;
                // Start loading in background and don't await - we'll check in Complete()
                StartLoadingAsync(generationData.position).Forget();
                return;
            }

            // If we couldn't load the data or it doesn't exist, generate new data

            // --- Prepare Data for the Job ---
            ScheduleDataGeneration();
        }

        private void ScheduleDataGeneration()
        {
            _isLoading = false;
            _isGenerating = true;
            // 1. Octave Offsets (ensure they are generated in GenerationParameters)
            if (_generationParams.OctaveOffsets == null || _generationParams.OctaveOffsets.Length == 0)
            {
                Debug.LogError("Octave Offsets not generated in GenerationParameters!");
                _generationParams.GenerateOctaveOffsets(10);
            }

            // Convert Vector2Int[] to int2[]
            int numOffsets = _generationParams.OctaveOffsets.Length;
            var octaveOffsetsInt2 = new int2[numOffsets];

            for (int i = 0; i < numOffsets; i++)
            {
                octaveOffsetsInt2[i] = new int2(
                    _generationParams.OctaveOffsets[i].x,
                    _generationParams.OctaveOffsets[i].y
                );
            }

            octaveOffsets = new NativeArray<int2>(octaveOffsetsInt2, Allocator.Persistent);

            // 2. Noise Layer Parameters (convert to job-safe struct)
            var layers = new List<NoiseLayerParametersJob>();

            if (_generationParams.ContinentalNoise?.enabled ?? false) layers.Add(CreateJobLayer(_generationParams.ContinentalNoise));
            if (_generationParams.MountainNoise?.enabled ?? false) layers.Add(CreateJobLayer(_generationParams.MountainNoise));
            if (_generationParams.HillNoise?.enabled ?? false) layers.Add(CreateJobLayer(_generationParams.HillNoise));
            if (_generationParams.DetailNoise?.enabled ?? false) layers.Add(CreateJobLayer(_generationParams.DetailNoise));

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
                globalScale = _generationParams.GlobalScale,
                seaLevel = _generationParams.SeaLevel,
                steepnessExponent = _generationParams.SteepnessExponent,
                mountainThreshold = _generationParams.MountainThreshold,

                // Biome/Voxel settings
                dirtDepth = _generationParams.DirtDepth,
                sandMargin = _generationParams.SandMargin,

                // Noise data
                noiseLayers = noiseLayers,
                octaveOffsets = octaveOffsets,

                // Output arrays
                voxels = Voxels,
                heightMaps = HeightMap,
            };

            if (WorldSettings.HasDebugging)
                Debug.Log("ChunkDataGenerator: Started Generating");

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
            Debug.Log($"_isGenerating: {_isGenerating} jobHandle.IsCompleted: {jobHandle.IsCompleted}");

            if (_isLoading)
            {
                return GenerationData;
            }

            // Safe job completion
            try
            {
                if (_isGenerating && jobHandle.IsCompleted)
                {
                    if (WorldSettings.HasDebugging)
                        Debug.Log("Data Generated");

                    // Complete the job
                    jobHandle.Complete();
                }
                else
                {
                    return GenerationData;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error completing job: {ex.Message} {ex.StackTrace}");
                return GenerationData;
            }

            return UploadData();
        }

        private GenerationData UploadData()
        {
            Chunk chunk = _chunksManager?.GetChunk(GenerationData.position);

            if (GenerationData == null)
                Debug.LogWarning($"ChunkDataGenerator: GenerationData is null.");

            if (chunk != null)
            {
                chunk.UploadData(ref Voxels, ref HeightMap);
                _hasLoaded = true;
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
            if (jobHandle.IsCompleted) //jobHandle != null && 
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
        /// Starts loading chunk data asynchronously
        /// </summary>
        /// <param name="position">Position of the chunk to load</param>
        /// <returns>Task that completes when loading finishes</returns>
        private async UniTask<ChunkLoadResult> StartLoadingAsync(Vector3 position)
        {
            try
            {
                var result = await ChunkSaveSystem.LoadChunkDataAsync(position);

                // Clean up previous arrays if they exist
                if (Voxels.IsCreated) Voxels.Dispose();
                if (HeightMap.IsCreated) HeightMap.Dispose();

                _loadResult = result;
                Voxels = _loadResult.Value.Voxels;
                HeightMap = _loadResult.Value.HeightMap;

                UploadData();

                if (WorldSettings.HasDebugging && result.Success)
                    Debug.Log($"Successfully loaded chunk data asynchronously for {position}\n" +
                    $"ChunkDataGenerator: Loaded Voxels Count: {_loadResult.Value.Voxels.Length}\n" +
                    $"Loaded HeightMap Count: {_loadResult.Value.HeightMap.Length}");

                return result;
            }
            catch (Exception ex)
            {
                _isLoading = false;
                _hasLoaded = false;
                Debug.LogWarning($"Failed to load chunk data asynchronously from save: {ex.Message}. Will fall back to generation.");
                ScheduleDataGeneration();
                return new ChunkLoadResult(false);
            }
        }
    }
}