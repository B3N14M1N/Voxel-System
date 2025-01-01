using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GenerationData
{
    public Vector3 position;
    public ChunkGenerationFlags flags;
}
public enum ChunkGenerationFlags
{
    None = 0,
    Data = 1,
    Collider = 2,
    Mesh = 4,
};

public class ChunkFactory : MonoBehaviour
{
    private float time = 0f;
    private List<GenerationData> chunksToProccess = new List<GenerationData>();
    private List<ChunkJob> chunkJobs = new List<ChunkJob>();
    #region Fields

    [Header("Noise Parameters")]
    [SerializeField] private NoiseParametersScriptableObject noiseParameters;
    private Vector2Int[] octaveOffsets;
    public NoiseParametersScriptableObject NoiseParameters { get { return noiseParameters; } }
    public Vector2Int[] NoiseOctaves { get { return octaveOffsets; } }

    [Header("Materials")]
    [SerializeField] private List<Material> materials = new List<Material>();
    private int materialIndex = 0;
    public Material Material { get { return materials[materialIndex]; } }
    public bool CanChangeMaterial { get; set; }

    [SerializeField] private KeyCode MaterialChangeKey = KeyCode.M;

    private static ChunkFactory instance;
    public static ChunkFactory Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<ChunkFactory>();
            if (instance == null)
                instance = new ChunkFactory();
            return instance;
        }
        private set
        {
            instance = value;
        }
    }
    #endregion

    #region Initializations

    public void Awake()
    {
        InitSeed();
        CanChangeMaterial = true;
    }

    public void InitSeed()
    {
        uint octavesMax = 0;
        for (int i = 0; i < noiseParameters.noise.Count; i++)
        {
            if (noiseParameters.noise[i].octaves > octavesMax)
                octavesMax = noiseParameters.noise[i].octaves;
        }
        octaveOffsets = new Vector2Int[octavesMax];
        System.Random rnd = new System.Random((int)WorldSettings.Seed);
        for (int i = 0; i < octavesMax; i++)
        {
            octaveOffsets[i] = new Vector2Int(rnd.Next(-10000, 10000), rnd.Next(-10000, 10000));
        }
    }

    #endregion


    private void Update()
    {
        if (Input.GetKeyUp(MaterialChangeKey) && CanChangeMaterial)
        {
            materialIndex = materialIndex == materials.Count - 1 ? 0 : ++materialIndex;
        }

        for(int i = 0, load = 0; i < chunkJobs.Count && load < PlayerSettings.ChunksToLoad; i++, load++)
        {
            if (chunkJobs[i].Complete())
            {
                chunkJobs.RemoveAt(i);
                i--;
            }
        }

        //Debug.Log(chunkJobs.Count);
        #region generation
        time += Time.deltaTime;
        if (time >= PlayerSettings.TimeToLoadNextChunks || ChunkJob.Processed == 0)
        {
            for (int i = 0; i < chunksToProccess.Count && ChunkJob.Processed < PlayerSettings.ChunksProcessed; i++)
            {
                chunkJobs.Add(new ChunkJob(chunksToProccess[i]));
                chunksToProccess.RemoveAt(i);
                i--;
            }
            time = 0f;
        }
        #endregion

    }

    public void GenerateChunksData(List<GenerationData> toGenerate)
    {
        chunksToProccess = toGenerate;
    }

    public void Dispose()
    {
        chunksToProccess.Clear();
        for (int i = 0; i < chunkJobs.Count; i++)
        {
            chunkJobs[i].Dispose(true);
            chunkJobs[i] = null;
        }
        chunkJobs.Clear();
    }

    public void OnApplicationQuit()
    {
        Dispose();
        ChunkParallelMeshGenerator.DisposeAll();
    }

    public void OnDestroy()
    {
        Dispose();
    }
}
