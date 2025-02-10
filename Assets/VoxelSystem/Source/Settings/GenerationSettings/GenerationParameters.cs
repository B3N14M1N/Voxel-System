using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelSystem.Settings.Generation
{
    /// <summary>
    /// 
    /// </summary>
    [CreateAssetMenu(fileName = "Generation Parameters", menuName = "ScriptableObjects/Generation Parameters")]
    public class GenerationParameters : ScriptableObject
    {
        [field: SerializeField] public float GlobalScale {  get; private set; }
        [field: SerializeField] public List<NoiseParameters> Noise {  get; private set; }
    };

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public struct NoiseParameters
    {
        public float noiseScale;
        public uint octaves;
        public float lacunarity;
        public float persistence;
        public float blending;
        public float damping;
        public float maxHeight;
        public uint ePow;
    }; 
}