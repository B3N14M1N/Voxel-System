using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName ="Noise Parameters", menuName ="ScriptableObjects/NoiseParameters")]
public class NoiseParametersScriptableObject : ScriptableObject
{
    [SerializeField]
    public float globalScale;
    [SerializeField]
    public List<NoiseParameters> noise;
};

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