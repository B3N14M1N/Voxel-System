using System;
using UnityEngine;

namespace VoxelSystem.Settings.Generation
{
    /// <summary>
    /// Parameters controlling the overall terrain generation process.
    /// </summary>
    [CreateAssetMenu(fileName = "Generation Parameters", menuName = "ScriptableObjects/Generation Parameters")]
    public class GenerationParameters : ScriptableObject
    {
        [Header("General Settings")]
        [Tooltip("Global scaling factor for all noise sampling coordinates.")]
        [field: SerializeField] public float GlobalScale { get; private set; } = 500f;


        [Tooltip("The sea level percent height in voxels.")]
        [Range(0f, 1f)]
        [field: SerializeField] public float SeaLevel { get; private set; } = 0.6f;

        [Header("Terrain Noise Layers")]
        [Tooltip("Defines large-scale continents and oceans.")]
        [field: SerializeField] public NoiseLayerParameters ContinentalNoise { get; private set; }

        [Tooltip("Creates major mountain ranges and elevation changes.")]
        [field: SerializeField] public NoiseLayerParameters MountainNoise { get; private set; }

        [Tooltip("Adds rolling hills and plains.")]
        [field: SerializeField] public NoiseLayerParameters HillNoise { get; private set; }

        [Tooltip("Adds fine details and roughness to the terrain.")]
        [field: SerializeField] public NoiseLayerParameters DetailNoise { get; private set; }

        [Header("Terrain Shaping")]
        [Tooltip("Exponent applied to the combined mountain/hill height to create steeper slopes. >1 = steeper, <1 = flatter.")]
        [field: SerializeField] public float SteepnessExponent { get; private set; } = 1.5f;

        [Tooltip("Height threshold above which terrain is considered mountainous (influences steepness). Normalized (0-1).")]
        [field: SerializeField] public float MountainThreshold { get; private set; } = 0.6f;

        [Header("Biome Settings")]

        [Tooltip("How many layers of dirt to place below grass.")]
        [field: SerializeField] public int DirtDepth { get; private set; } = 3;

        [Tooltip("How many layers near sea level should be sand.")]
        [field: SerializeField] public int SandMargin { get; private set; } = 2;

        // Pre-calculate octave offsets based on seed during initialization or OnValidate
        [HideInInspector] public Vector2Int[] OctaveOffsets;

        public void OnValidate()
        {
            // Ensure MaxHeight and SeaLevel are reasonable
            if (SeaLevel >= 1.0f) SeaLevel = 0.6f;
            if (SeaLevel < 0.0f) SeaLevel = 0.6f;

            // Find the maximum number of octaves needed across all layers
            uint maxOctaves = 0;
            maxOctaves = Math.Max(maxOctaves, ContinentalNoise?.noise.octaves ?? 0);
            maxOctaves = Math.Max(maxOctaves, MountainNoise?.noise.octaves ?? 0);
            maxOctaves = Math.Max(maxOctaves, HillNoise?.noise.octaves ?? 0);
            maxOctaves = Math.Max(maxOctaves, DetailNoise?.noise.octaves ?? 0);

            // Generate octave offsets if needed or if seed/octave count changed
            // This ensures consistent offsets for a given seed.
            GenerateOctaveOffsets(maxOctaves);
        }

        public void GenerateOctaveOffsets(uint maxOctaves)
        {
            System.Random prng = new(WorldSettings.Seed);
            Debug.Log($"Generating {maxOctaves} octave offsets with seed {WorldSettings.Seed}");
            OctaveOffsets = new Vector2Int[maxOctaves];
            for (int i = 0; i < maxOctaves; i++)
            {
                // Generate large offsets to ensure different sampling points per octave
                int offsetX = prng.Next(-100000, 100000);
                int offsetY = prng.Next(-100000, 100000);
                OctaveOffsets[i] = new Vector2Int(offsetX, offsetY);
            }
        }
    }

    /// <summary>
    /// Parameters for a single layer of noise used in terrain generation.
    /// </summary>
    [Serializable]
    public class NoiseLayerParameters
    {
        [Tooltip("Enable this noise layer.")]
        public bool enabled = true;

        [Tooltip("Parameters for the Perlin/Simplex noise function.")]
        public NoiseParameters noise;

        [Tooltip("How much this layer contributes to the final height. Can be negative to carve.")]
        public float weight = 1.0f;

        [Tooltip("Use first layer as mask. If enabled, noise values below the threshold will significantly reduce or negate the contribution of subsequent layers.")]
        public bool useAsMask = false;
        [Tooltip("Threshold for the mask (normalized 0-1). Only relevant if useAsMask is true.")]
        [Range(0f, 1f)]
        public float maskThreshold = 0.5f;
    }


    /// <summary>
    /// Defines parameters for a multi-octave Perlin/Simplex noise calculation.
    /// </summary>
    [Serializable]
    public struct NoiseParameters
    {
        [Tooltip("Scale of the noise features. Smaller values = larger features.")]
        public float noiseScale;
        [Tooltip("Number of noise layers summed for detail. More octaves = more detail, higher cost.")]
        public uint octaves;
        [Tooltip("How quickly noise frequency increases per octave (>1).")]
        public float lacunarity;
        [Tooltip("How quickly noise amplitude decreases per octave (0-1).")]
        [Range(0f, 1f)]
        public float persistence;
    };

}
