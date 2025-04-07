/* ChunkDataJob.cs */
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelSystem.Managers; // Assuming this namespace exists
using VoxelSystem.Data;
using VoxelSystem.Settings.Generation;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using VoxelSystem.Data.GenerationFlags; // Use the new GenerationParameters


namespace VoxelSystem.Generators
{
    /// <summary>
    /// Generates the voxel data and heightmap for a single chunk using multiple noise layers.
    /// </summary>
    public class ChunkDataGenerator : IDisposable // Implement IDisposable
    {
        public GenerationData GenerationData { get; private set; }
        public NativeArray<Voxel> Voxels;
        public NativeArray<HeightMap> HeightMap;

        // Keep references to NativeArrays used by the job
        private NativeArray<int2> octaveOffsets;
        private NativeArray<NoiseLayerParametersJob> noiseLayers; // Use a job-safe struct

        private JobHandle jobHandle;
        public bool IsComplete => jobHandle.IsCompleted;
        private readonly IChunksManager _chunksManager; // Assuming this interface exists

        // Job-safe version of NoiseLayerParameters
        [BurstCompile]
        public struct NoiseLayerParametersJob
        {
            public bool enabled;
            public NoiseParameters noise; // NoiseParameters should be Burst-compatible
            public float weight;
            public bool useAsMask;
            public float maskThreshold;
        }

        public ChunkDataGenerator(GenerationData generationData,
            GenerationParameters generationParams, // Pass the whole parameters object
            IChunksManager chunksManager)
        {
            GenerationData = generationData;
            _chunksManager = chunksManager;

            // --- Prepare Data for the Job ---

            // 1. Octave Offsets (ensure they are generated in GenerationParameters)
            if (generationParams.OctaveOffsets == null || generationParams.OctaveOffsets.Length == 0)
            {
                Debug.LogError("Octave Offsets not generated in GenerationParameters!");
                // Handle error appropriately, maybe generate them here if needed
                generationParams.GenerateOctaveOffsets(10); // Example: generate for max 10 octaves
            }
            // --- CONVERSION START ---
            // Create a managed array of the target type (int2)
            int numOffsets = generationParams.OctaveOffsets.Length;
            var octaveOffsetsInt2 = new Unity.Mathematics.int2[numOffsets];

            // Copy data from Vector2Int[] to int2[]
            for (int i = 0; i < numOffsets; i++)
            {
                octaveOffsetsInt2[i] = new Unity.Mathematics.int2(
                    generationParams.OctaveOffsets[i].x,
                    generationParams.OctaveOffsets[i].y
                );
            }
            // --- CONVERSION END ---

            // Now create the NativeArray from the converted int2[] array
            this.octaveOffsets = new NativeArray<int2>(octaveOffsetsInt2, Allocator.Persistent);


            // 2. Noise Layer Parameters (convert to job-safe struct)
            var layers = new List<NoiseLayerParametersJob>();
            if (generationParams.ContinentalNoise?.enabled ?? false) layers.Add(CreateJobLayer(generationParams.ContinentalNoise));
            if (generationParams.MountainNoise?.enabled ?? false) layers.Add(CreateJobLayer(generationParams.MountainNoise));
            if (generationParams.HillNoise?.enabled ?? false) layers.Add(CreateJobLayer(generationParams.HillNoise));
            if (generationParams.DetailNoise?.enabled ?? false) layers.Add(CreateJobLayer(generationParams.DetailNoise));
            this.noiseLayers = new NativeArray<NoiseLayerParametersJob>(layers.ToArray(), Allocator.Persistent);


            // 3. Allocate Voxel and HeightMap Arrays
            // Size includes padding for neighbor lookups during meshing (+2)
            int paddedWidth = WorldSettings.ChunkWidth + 2;
            int paddedHeight = WorldSettings.ChunkHeight; // Height doesn't usually need padding unless accessing neighbors vertically
            int paddedDepth = WorldSettings.ChunkWidth + 2;
            Voxels = new NativeArray<Voxel>(paddedWidth * paddedHeight * paddedDepth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            HeightMap = new NativeArray<HeightMap>(paddedWidth * paddedDepth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // --- Schedule the Job ---
            var dataJob = new ChunkDataJob()
            {
                // Chunk settings
                chunkWidth = WorldSettings.ChunkWidth,
                chunkHeight = WorldSettings.ChunkHeight,
                chunkPos = this.GenerationData.position, // Assuming GenerationData has position

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
                noiseLayers = this.noiseLayers,
                octaveOffsets = this.octaveOffsets,

                // Output arrays
                voxels = this.Voxels,
                heightMaps = this.HeightMap,
            };

            // Schedule the job to run in parallel
            // The job processes one column (x, z) at a time.
            jobHandle = dataJob.Schedule(HeightMap.Length, 32); // Process 32 columns per batch
        }

        // Helper to convert managed class to job struct
        private NoiseLayerParametersJob CreateJobLayer(NoiseLayerParameters layer)
        {
            return new NoiseLayerParametersJob
            {
                enabled = layer.enabled,
                noise = layer.noise, // Assumes NoiseParameters is a struct
                weight = layer.weight,
                useAsMask = layer.useAsMask,
                maskThreshold = layer.maskThreshold
            };
        }


        // Call this when the job is done to apply data and clean up
        public GenerationData Complete()
        {
            if (IsComplete)
            {
                jobHandle.Complete(); // Ensure job is finished

                Chunk chunk = _chunksManager?.GetChunk(GenerationData.position);
                if (chunk != null)
                {
                    // Transfer ownership/copy data to the chunk
                    // The specific method depends on your Chunk class implementation
                    // This might involve chunk.SetVoxels(Voxels) or similar.
                    // We pass by ref here, assuming UploadData might take ownership or copy.
                    chunk.UploadData(ref Voxels, ref HeightMap);
                }
                else
                {
                    // Chunk doesn't exist anymore (maybe unloaded), discard data
                    Dispose(true); // Dispose native arrays immediately
                    return null; // Indicate failure or cancellation
                }

                // Update flags for the next steps (e.g., meshing)
                // Assuming ChunkGenerationFlags exists
                GenerationData.flags &= ChunkGenerationFlags.Mesh | ChunkGenerationFlags.Collider;
                // GenerationData.flags = ChunkGenerationFlags.Generated; // Or whatever flags you use


                Dispose(); // Dispose job-specific native arrays (offsets, noise layers)
                return GenerationData; // Return updated data
            }
            return GenerationData; // Job not yet complete
        }

        // Dispose pattern for NativeArrays
        public void Dispose()
        {
            Dispose(false); // Dispose only job arrays, not voxel/heightmap data yet
            GC.SuppressFinalize(this); // Prevent finalizer call
        }

        public void Dispose(bool disposingAll)
        {
            // Always complete the handle before disposing arrays used by the job
            if (jobHandle.IsCompleted) // Check if already completed
                jobHandle.Complete();


            // Dispose job-specific arrays
            if (octaveOffsets.IsCreated) octaveOffsets.Dispose();
            if (noiseLayers.IsCreated) noiseLayers.Dispose();

            if (disposingAll)
            {
                // Dispose voxel/heightmap data only if requested (e.g., chunk unload)
                if (Voxels.IsCreated) Voxels.Dispose();
                if (HeightMap.IsCreated) HeightMap.Dispose();
            }
        }

        ~ChunkDataGenerator()
        {
            // Finalizer calls Dispose with disposingAll=true
            // This is a safety net in case Dispose() isn't called explicitly.
            // However, relying on the finalizer for NativeArray disposal is not recommended.
            Dispose(true);
        }


        // --- The Burst Compiled Job ---
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)] // Adjust precision as needed
        internal struct ChunkDataJob : IJobParallelFor
        {
            // --- Input Settings (Read Only) ---
            [ReadOnly] public int chunkWidth;
            [ReadOnly] public int chunkHeight; // Max height of the voxel array
            [ReadOnly] public float3 chunkPos; // World position of the chunk corner (in chunk units)

            [ReadOnly] public float globalScale;
            [ReadOnly] public int maxHeight;    // Max terrain height allowed (in voxels)
            [ReadOnly] public float seaLevel;
            [ReadOnly] public float steepnessExponent;
            [ReadOnly] public float mountainThreshold;

            [ReadOnly] public int dirtDepth;
            [ReadOnly] public int sandMargin;

            // Noise Data (Read Only)
            [ReadOnly] public NativeArray<NoiseLayerParametersJob> noiseLayers;
            [ReadOnly] public NativeArray<int2> octaveOffsets; // Shared octave offsets

            // --- Output Data (Write Only) ---
            // Use NativeDisableParallelForRestriction as we write to different columns per thread
            [WriteOnly]
            [NativeDisableParallelForRestriction]
            public NativeArray<Voxel> voxels;

            [WriteOnly]
            [NativeDisableParallelForRestriction]
            public NativeArray<HeightMap> heightMaps;


            // --- Job Execution Logic ---
            // This method is called for each index, representing a column (x, z) in the padded chunk.
            public void Execute(int index)
            {
                // Calculate local (padded) coordinates within the chunk (0 to chunkWidth+1)
                int paddedChunkWidth = chunkWidth + 2;
                int x = index / paddedChunkWidth; // Local X within the padded chunk
                int z = index % paddedChunkWidth; // Local Z within the padded chunk

                // Calculate world coordinates for noise sampling
                // Offset by -1 because the padded border represents neighbor data
                float worldX = (chunkPos.x * chunkWidth) + x - 1;
                float worldZ = (chunkPos.z * chunkWidth) + z - 1;

                // --- Calculate Final Terrain Height ---
                float totalHeight = 0;
                float weightSum = 0; // To normalize if weights don't sum to 1
                float maskModifier = 1.0f; // Modifier applied by mask layers

                for (int i = 0; i < noiseLayers.Length; i++)
                {
                    NoiseLayerParametersJob layer = noiseLayers[i];
                    if (!layer.enabled) continue;

                    // Calculate noise value for this layer (0 to 1 range)
                    float noiseValue = GetNormalizedNoise(worldX, worldZ, layer.noise);

                    // Apply mask if this layer is configured as a mask
                    if (layer.useAsMask)
                    {
                        // Simple threshold mask: values below threshold reduce influence of subsequent layers
                        //maskModifier = math.select(0.0f, 1.0f, noiseValue >= layer.maskThreshold);
                        // Could also use smoothstep for a gradual transition:
                        maskModifier = math.smoothstep(layer.maskThreshold - 0.1f, layer.maskThreshold + 0.1f, noiseValue);
                    }

                    // Add weighted noise value, potentially modified by mask
                    totalHeight += noiseValue * layer.weight * maskModifier;
                    weightSum += math.abs(layer.weight * maskModifier); // Use absolute weight for normalization
                }

                // Normalize height if weights don't sum nicely (optional but safer)
                // if (weightSum > 0.01f) // Avoid division by zero
                // {
                //     totalHeight /= weightSum; // This makes weights relative proportions
                // }
                // Instead of normalizing, let weights directly control contribution scale
                // Clamp the combined height to a 0-1 range before scaling to maxHeight
                totalHeight = math.saturate(totalHeight);


                // --- Terrain Shaping ---
                // Apply steepness based on height (e.g., make mountains steeper)
                // This raises the height based on how high it already is, creating peaks
                float mountainInfluence = math.smoothstep(mountainThreshold - 0.1f, mountainThreshold + 0.1f, totalHeight); // 0=low, 1=high
                float shapedHeight = math.pow(totalHeight, 1.0f + mountainInfluence * (steepnessExponent - 1.0f)); // Apply exponent more strongly at higher elevations


                // Scale normalized height (0-1) to voxel height
                int terrainHeight = (int)math.floor(shapedHeight * maxHeight);

                // Clamp height to valid range
                terrainHeight = math.clamp(terrainHeight, 1, chunkHeight - 1); // Ensure terrain is at least 1 block high and leaves space at the top


                // --- Set HeightMap ---
                // Store the final calculated terrain height (solid ground level)
                HeightMap heightMap = new() { data = 0 }; // Initialize
                heightMap.SetSolid((uint)terrainHeight);
                // TODO: Add liquid height calculation if needed (e.g., based on seaLevel)
                // if (terrainHeight < seaLevel) {
                //    heightMap.SetLiquidTop((uint)seaLevel);
                //    heightMap.SetLiquidBottom((uint)terrainHeight); // Liquid fills space above terrain up to sea level
                // }
                heightMaps[index] = heightMap;


                // --- Voxel Placement ---
                // Define base voxel types (cache them)
                Voxel grass = new() { ID = (byte)VoxelType.grass };
                Voxel dirt = new() { ID = (byte)VoxelType.dirt };
                Voxel stone = new() { ID = (byte)VoxelType.stone };
                Voxel sand = new() { ID = (byte)VoxelType.sand };

                //Voxel water = CreateVoxel(VoxelType.water, VoxelPhysicsType.liquid); // Assuming water exists
                Voxel air = Voxel.Empty; // Air


                for (int y = 0; y < chunkHeight; y++)
                {
                    int voxelIndex = GetVoxelIndex(x, y, z, paddedChunkWidth, chunkHeight);
                    Voxel voxelToPlace = air; // Default to air

                    if (y >= terrainHeight)
                    {
                        voxels[voxelIndex] = voxelToPlace;
                        continue;
                    }

                    voxelToPlace = stone;

                    if (terrainHeight >= chunkHeight * 0.8f) // if not at the peak (stone)
                    {
                        voxels[voxelIndex] = voxelToPlace;
                        continue;
                    }

                    if (terrainHeight > chunkHeight * seaLevel) // if not at the bottom place grass/dirt
                    {
                        if (y >= terrainHeight - dirtDepth)
                            voxelToPlace = dirt;
                        if (y == terrainHeight - 1)
                            voxelToPlace = grass;
                    }
                    else // place sand on top layer then place stone
                    {
                        if (y >= terrainHeight - sandMargin)
                            voxelToPlace = sand;
                    }

                    voxels[voxelIndex] = voxelToPlace;
                }
            }


            // --- Helper Functions ---

            // Calculates normalized Perlin noise (0 to 1) using multiple octaves
            private readonly float GetNormalizedNoise(float x, float z, NoiseParameters param)
            {
                // Ensure noiseScale is not zero to avoid division by zero
                if (param.noiseScale <= 0) param.noiseScale = 0.0001f;


                float total = 0;
                float frequency = 1;
                float amplitude = 1;
                float maxValue = 0; // Used for normalizing to 0-1 range

                for (int i = 0; i < param.octaves; i++)
                {
                    // Calculate sample coordinates with octave offsets
                    // Use world coordinates directly + octave offsets
                    // Divide by noiseScale *after* adding offsets
                    float sampleX = (x + octaveOffsets[i].x) * frequency / param.noiseScale / globalScale;
                    float sampleZ = (z + octaveOffsets[i].y) * frequency / param.noiseScale / globalScale;

                    // Get Perlin noise value (-1 to 1 or 0 to 1 depending on implementation)
                    // Unity's Mathf.PerlinNoise returns 0 to 1
                    // Using Unity.Mathematics.noise.snoise (Simplex) returns -1 to 1, which is often better
                    // Let's assume we have access to simplex noise 'snoise' from Unity.Mathematics
                    // float noiseValue = noise.snoise(new float2(sampleX, sampleZ)); // Returns -1 to 1
                    // For now, stick to Perlin from Mathf for compatibility if Burst doesn't have snoise easily accessible
                    // Need to ensure Mathf.PerlinNoise is Burst-compatible or use Unity.Mathematics.noise
                    // Let's assume Unity.Mathematics.noise.cnoise (Classic Perlin) is available and returns ~ -1 to 1
                    float noiseValue = noise.cnoise(new float2(sampleX, sampleZ)); // Classic Perlin noise (-1 to 1)

                    total += noiseValue * amplitude;

                    maxValue += amplitude; // Track max possible amplitude

                    amplitude *= param.persistence; // Reduce amplitude for next octave
                    frequency *= param.lacunarity; // Increase frequency for next octave
                }

                // Normalize the result to a 0-1 range
                // If noise is -1 to 1, map it to 0 to 1: (value + 1) / 2
                // Also divide by maxValue to ensure it stays within bounds even if persistence > 1
                if (maxValue <= 0) return 0.5f; // Avoid division by zero, return neutral value
                return (total / maxValue + 1.0f) * 0.5f;

                // If using Mathf.PerlinNoise (0 to 1):
                // return total / maxValue; // Already in ~0-1 range, just normalize by max amplitude
            }


            // Calculates the 1D index into the voxel array from 3D coordinates
            [MethodImpl(MethodImplOptions.AggressiveInlining)] // Suggest inlining for performance
            private readonly int GetVoxelIndex(int x, int y, int z, int width, int height)
            {
                // Order: X changes fastest, then Z, then Y (or vice-versa depending on preference)
                // Standard Unity layout often Y-up: Z + X*Depth + Y*Depth*Width
                // Your original layout: Z + Y*Width + X*Width*Height (Check this matches meshing!)
                // Let's stick to your original: Z changes fastest, then Y, then X
                return z + (y * width) + (x * width * height);
            }
        }
    }

}