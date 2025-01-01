using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChunksFactory : MonoBehaviour
{
    /*
    #region Fields
    public const string ChunkMeshCreationString = "ChunkMeshCreationString";
    public const string ChunkFactoryLoopString = "ChunkFactoryLoop";
    private float time = 0f;

    [Header("Noise Parameters")]
    [SerializeField] private NoiseParametersScriptableObject noiseParameters;
    private Vector2Int[] octaveOffsets;

    [Header("Buffers")]
    private List<Vector3> chunksToProccess = new List<Vector3>();

    private List<JobChunkGenerator> chunkJobs = new List<JobChunkGenerator>();
    private List<ChunkColliderGenerator> colliderJobs = new List<ChunkColliderGenerator>();
    [Header("Materials")]
    [SerializeField]private List<Material> materials = new List<Material>();
    private int materialIndex = 0;
    public Material Material { get { return materials[materialIndex]; } }
    public bool CanChangeMaterial { get; set; }

    [SerializeField] private KeyCode MaterialChangeKey = KeyCode.M;

    private static ChunksFactory instance;
    public static ChunksFactory Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<ChunksFactory>();
            if (instance == null)
                instance = new ChunksFactory();
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

    void Update()
    {
        var UpdateTime = Time.realtimeSinceStartup;

        if (Input.GetKeyUp(MaterialChangeKey) && CanChangeMaterial)
        {
            materialIndex = materialIndex == materials.Count - 1 ? 0 : ++materialIndex;
        }

        int loaded = 0;
        for (int i = 0; i < chunkJobs.Count; i++)
        {
            if (chunkJobs[i] != null)
            {
                var timeNow = Time.realtimeSinceStartup;
                if (chunkJobs[i].CompleteDataGeneration())
                {
                    colliderJobs.Add(new ChunkColliderGenerator(chunkJobs[i].heightMaps.AsReadOnly(), chunkJobs[i].chunkPos));
                    chunkJobs[i].ScheduleMeshGeneration();
                }
                else
                if (chunkJobs[i].CompleteMeshGeneration())
                {
                    Chunk chunk = ChunksManager.Instance.GetChunk(chunkJobs[i].chunkPos);
                    if (chunk != null)
                    {
                        chunk.UploadData(ref chunkJobs[i].voxels, ref chunkJobs[i].heightMaps);
                        Mesh mesh = new Mesh() { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                        chunkJobs[i].SetMesh(ref mesh);
                        chunk.UploadMesh(ref mesh);
                        ChunksManager.Instance.CompleteGeneratingChunk(chunk.Position);
                    }
                    chunkJobs[i].Dispose();
                    chunkJobs.RemoveAt(i);
                    i--;
                    loaded++;
                    AvgCounter.UpdateCounter(ChunkMeshCreationString, (Time.realtimeSinceStartup - timeNow) * 1000f);

                    if (loaded >= PlayerSettings.ChunksToLoad)
                        break;
                }
            }
        }
        for (int i = 0; i < colliderJobs.Count; i++)
        {
            if(colliderJobs[i] != null && colliderJobs[i].CompletedGeneration())
            {
                Chunk chunk = ChunksManager.Instance.GetChunk(colliderJobs[i].chunkPos);
                if (chunk != null)
                {
                    Mesh mesh = new Mesh() { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    colliderJobs[i].SetCollider(ref mesh);
                    chunk.UpdateCollider(ref mesh);
                }
                colliderJobs[i].Dispose();
                colliderJobs.RemoveAt(i);
                i--;
            }
        }
        time += Time.deltaTime;
        if (time >= PlayerSettings.TimeToLoadNextChunks || JobChunkGenerator.Processed == 0)
        {
            if (chunksToProccess != null)
            {
                for (int i = 0; i < chunksToProccess.Count && JobChunkGenerator.Processed < PlayerSettings.ChunksProcessed; i++)
                {
                    chunkJobs.Add(new JobChunkGenerator(chunksToProccess[i], noiseParameters.noise.ToArray(), octaveOffsets.ToArray(), noiseParameters.globalScale));
                    ChunksManager.Instance.SetChunkToGenerating(chunksToProccess[i]);
                    chunksToProccess.RemoveAt(i);
                    i--;
                }
            }
            time = 0f;
        }
        AvgCounter.UpdateCounter(ChunkFactoryLoopString, (Time.realtimeSinceStartup - UpdateTime) * 1000f);
    }

    public void GenerateChunksData(List<Vector3> positions)
    {
        chunksToProccess = positions;
    }

    public void Dispose()
    {
        for (int i = 0; i < colliderJobs.Count; i++)
        {
            colliderJobs[i].Dispose();
            colliderJobs.RemoveAt(i);
            i--;
        }
        for (int i = 0; i < chunkJobs.Count; i++)
        {
            chunkJobs[i].Dispose(disposeData: true);
            chunkJobs.RemoveAt(i);
            i--;
        }
    }

    public void OnApplicationQuit()
    {
        Dispose();
    }

    public void OnDestroy()
    {
        Dispose();
    }
    */
}
