using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class ChunksManager : MonoBehaviour, IChunksManager
{
    public const string ChunksManagerLoopString = "ChunksManagerLoop";
    public Vector3 Center { get; private set; }

    private Queue<Chunk> pool = new Queue<Chunk>();
    private Dictionary<Vector3, Chunk> active = new Dictionary<Vector3, Chunk>();
    private Dictionary<Vector3, Chunk> cached = new Dictionary<Vector3, Chunk>();
    private Dictionary<Vector3, Chunk> generating = new Dictionary<Vector3, Chunk>();
    private Queue<Chunk> ChunksToClear = new Queue<Chunk>();
    private Vector3[] ChunksPositionCheck;
    private Chunk NewChunk
    {
        get
        {
            Chunk chunk = new Chunk(Vector3.zero);
            chunk.Parent = transform;
            chunk.Active = false;
            return chunk;
        }
    }

    private static ChunksManager instance;
    public static ChunksManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ChunksManager>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        for (int i = 0; i < (PlayerSettings.RenderDistance + PlayerSettings.CacheDistance) * (PlayerSettings.RenderDistance + PlayerSettings.CacheDistance); i++)
        {
            pool.Enqueue(NewChunk);
        }
        GenerateChunksPositionsCheck();
    }

    public void GenerateChunksPositionsCheck()
    {
        var job = new JobChunksFilter(Vector3.zero, PlayerSettings.RenderDistance);
        ChunksPositionCheck = job.Complete();
        job.Dispose();
    }

    public void UpdateChunks(Vector3 center)
    {
        var UpdateTime = Time.realtimeSinceStartup;
        Center = center;
        cached.AddRange(active);
        active.Clear();

        var chunksToGenerate = new List<GenerationData>();

        for (int i = 0; i < ChunksPositionCheck.Length; i++)
        {
            Vector3 key = new Vector3(ChunksPositionCheck[i].x + Center.x, 0, ChunksPositionCheck[i].z + Center.z);
            var data = new GenerationData()
            {
                position = key,
                flags = ChunkGenerationFlags.Data | ChunkGenerationFlags.Collider | ChunkGenerationFlags.Mesh,
            };

            //bool simulate = ChunksPositionCheck[i].y <= PlayerSettings.SimulateDistance * PlayerSettings.SimulateDistance;

            if (cached.TryGetValue(key, out Chunk chunk))
            {
                chunk.Active = true;
                chunk.Render = true;
                active.Add(key, chunk);
                cached.Remove(key);
                continue;
            }
            if(generating.TryGetValue(key, out chunk))
            {
                continue;
            }

            chunksToGenerate.Add(data);
        }
 
        if(chunksToGenerate.Count > 0)
            ChunkFactory.Instance.GenerateChunksData(chunksToGenerate);

        List<Vector3> removals = (from key in cached.Keys
                                  where !WorldSettings.ChunksInRange(center, key, PlayerSettings.RenderDistance + PlayerSettings.CacheDistance)
                                  select key).ToList();
        
        foreach (var key in removals)
        {
            ChunksToClear.Enqueue(cached[key]);
            cached.Remove(key);
            //ClearChunkAndEnqueue(key, ref cached);
        }

        foreach (var key in cached.Keys)
        {
            cached[key].Active = false;
        }

        AvgCounter.UpdateCounter(ChunksManagerLoopString, (Time.realtimeSinceStartup - UpdateTime) * 1000f);
    }
    public void LateUpdate()
    {
        if(ChunksToClear.Count > 0)
        {
            ClearChunkAndEnqueue(ChunksToClear.Dequeue());
        }
    }

    public void ClearChunkAndEnqueue(Chunk chunk)
    {
        chunk.ClearChunk();
        var size = PlayerSettings.CacheDistance + PlayerSettings.RenderDistance;
        size *= size;
        size -= PlayerSettings.RenderDistance * PlayerSettings.RenderDistance;
        if (pool.Count < size)
            pool.Enqueue(chunk);
        else
            chunk.Dispose();
    }

    private Chunk GetPooledChunk()
    {
        if(pool.Count == 0 && ChunksToClear.Count>0)
        {
            ClearChunkAndEnqueue(ChunksToClear.Dequeue());
        }
        if (pool.Count > 0)
        {
            return pool.Dequeue();
        }
        else
        {
            return NewChunk;
        }
    }

    private Chunk GetChunkFromSource(Vector3 pos, ref Dictionary<Vector3, Chunk> source)
    {
        if (source == null)
            return null;
        source.TryGetValue(pos, out Chunk chunk);
        return chunk;
    }

    public Chunk GetChunk(Vector3 pos)
    {
        Chunk chunk =  GetChunkFromSource(pos, ref active);
        if (chunk == null)
            chunk = GetChunkFromSource(pos, ref cached);
        if (chunk == null)
            chunk = GetChunkFromSource(pos, ref generating);
        return chunk;
    }

    public void SetChunkToGenerating(Vector3 pos)
    {
        Chunk chunk = GetChunk(pos);
        if (chunk == null)
        {
            chunk = GetPooledChunk();
            chunk.UpdateChunk(pos);
            chunk.Active = true;
            chunk.Render = true;
        }
        generating.Add(pos, chunk);
    }

    public void CompleteGeneratingChunk(Vector3 pos)
    {
        if(generating.TryGetValue(pos, out Chunk chunk))
        {
            active.Add(pos, chunk);
            generating.Remove(pos);
        }
    }

    private int meshVertices = 0;
    private int meshIndices = 0;
    private int colliderVertices = 0;
    private int colliderIndices = 0;
    public void UpdateChunkMeshSize(int mV, int mI)
    {
        meshVertices += mV;
        meshIndices += mI;
    }
    public void UpdateChunkColliderSize(int cV, int cI)
    {
        colliderVertices += cV;
        colliderIndices += cI;
    }
    public (int, int, int, int, int) ChunksMeshAndColliderSize()
    {
        return (active.Count ,meshVertices, meshIndices, colliderVertices, colliderIndices);
    }


    public void Dispose()
    {
        while (pool.Count > 0)
        {
            pool.Dequeue().Dispose();
        }
        pool.Clear();

        foreach (var key in active.Keys)
        {
            active[key].Dispose();
        }
        active.Clear();

        foreach (var key in cached.Keys)
        {
            cached[key].Dispose();
        }
        cached.Clear();

        foreach (var key in generating.Keys)
        {
            generating[key].Dispose();
        }
        generating.Clear();

#if UNITY_EDITOR
        EditorUtility.UnloadUnusedAssetsImmediate();
        GC.Collect();
#endif
    }

    private void OnApplicationQuit()
    {
        Dispose();
    }

    public void OnDestroy()
    {
        Dispose();
    }
}
