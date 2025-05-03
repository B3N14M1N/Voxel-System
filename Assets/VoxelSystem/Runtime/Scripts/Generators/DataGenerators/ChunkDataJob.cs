/* ChunkDataJob.cs */
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelSystem.Data;
using VoxelSystem.Settings.Generation;
using System.Runtime.CompilerServices;

namespace VoxelSystem.Generators
{
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
        [ReadOnly] public float seaLevel;
        [ReadOnly] public float steepnessExponent;
        [ReadOnly] public float mountainThreshold;

        [ReadOnly] public int dirtDepth;
        [ReadOnly] public int sandMargin;

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
            int paddedX = index / (chunkWidth + 2);
            int paddedZ = index % (chunkWidth + 2);
            int x = paddedX - 1;
            int z = paddedZ - 1;

            // Calculate world coordinates for noise sampling
            float worldX = (chunkPos.x * chunkWidth) + x;
            float worldZ = (chunkPos.z * chunkWidth) + z;

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

            int terrainHeight = (int)math.floor(shapedHeight * chunkHeight);
            terrainHeight = math.clamp(terrainHeight, 1, chunkHeight);

            // --- Set HeightMap ---
            HeightMap heightMap = new((byte)terrainHeight);
            heightMaps[index] = heightMap;

            // --- Voxel Placement ---

            if (!(paddedX >= 1 && paddedZ >= 1 && paddedX <= chunkWidth && paddedZ <= chunkWidth)) return;
            Voxel grass = new((byte)VoxelType.grass);
            Voxel dirt = new((byte)VoxelType.dirt);
            Voxel stone = new((byte)VoxelType.stone);
            Voxel sand = new((byte)VoxelType.sand);
            Voxel air = Voxel.Empty;



            for (int y = 0; y < chunkHeight; y++)
            {
                int voxelIndex = GetVoxelIndex(x, y, z);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        private readonly int GetVoxelIndex(int x, int y, int z)
        {
            return z + (y * chunkWidth) + (x * chunkWidth * chunkHeight);
        }
    }
}