using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VoxelSystem.Factory;
using VoxelSystem.Managers;

public class ChunkJob
{
    private ChunkDataGenerator chunkDataGenerator;
    private ChunkColliderGenerator chunkColliderGenerator;
    private ChunkMeshGenerator chunkMeshGenerator;
    private ChunkParallelMeshGenerator chunkParallelMeshGenerator;
    private readonly IChunksManager _chunksManager;

    public static int Processed { get; private set; }
    public GenerationData GenerationData { get; private set; }
    private bool Completed = false;

    private bool DataScheduled;

    private bool ColliderScheduled;

    private bool MeshScheduled;

    private Chunk chunk;

    public ChunkJob(GenerationData generationData,
        IChunksManager chunksManager,
        CancellationToken cancellationToken)
    {

        GenerationData = generationData;
        Completed = true;
        DataScheduled = false;
        ColliderScheduled = false;
        MeshScheduled = false;
        _chunksManager = chunksManager;

        if ((GenerationData.flags & ChunkGenerationFlags.Data) != 0)
        {
            var parameters = ChunkFactory.Instance.NoiseParameters;
            var octaves = ChunkFactory.Instance.NoiseOctaves;
            chunkDataGenerator = new ChunkDataGenerator(GenerationData, parameters.noise.ToArray(), octaves, parameters.globalScale, _chunksManager);
            DataScheduled = true;
            Completed = false;
            Processed += 1;
        }
        else
        {
            chunk = _chunksManager?.GetChunk(GenerationData.position);
            if (chunk != null)
            {
                if ((GenerationData.flags & ChunkGenerationFlags.Collider) != 0)
                {
                    chunkColliderGenerator = new ChunkColliderGenerator(GenerationData, ref chunk.HeightMap, _chunksManager);
                    ColliderScheduled = true;
                    Completed = false;
                }
                if ((GenerationData.flags & ChunkGenerationFlags.Mesh) != 0)
                {
                    //chunkMeshGenerator = new ChunkMeshGenerator(GenerationData, ref chunk.Voxels, ref chunk.HeightMap);
                    chunkParallelMeshGenerator = new ChunkParallelMeshGenerator(GenerationData, ref chunk.Voxels, ref chunk.HeightMap, _chunksManager);
                    MeshScheduled = true;
                    Completed = false;
                }
            }
        }
        if (!Completed)
        {
            _chunksManager?.SetChunkToGenerating(GenerationData.position);
            chunk = _chunksManager?.GetChunk(GenerationData.position);
        }
        StartGenerating(cancellationToken);
    }

    public bool Complete()
    {
        if (DataScheduled && chunkDataGenerator.IsComplete)
        {
            DataScheduled = true;
            DataScheduled = false;
            GenerationData = chunkDataGenerator.Complete();

            if (GenerationData.flags == ChunkGenerationFlags.None)
            {
                _chunksManager?.CompleteGeneratingChunk(GenerationData.position);
                Completed = true;
                Dispose();
                Processed -= 1;
            }
            else
            {
                //Chunk chunk = ChunksManager.Instance.GetChunk(GenerationData.position);

                // if the flags wants a collider, continue _generating
                if ((GenerationData.flags & ChunkGenerationFlags.Collider) != 0)
                {
                    chunkColliderGenerator = new ChunkColliderGenerator(GenerationData, ref chunk.HeightMap, _chunksManager);
                    ColliderScheduled = true;
                }

                // if the flags wants a mesh, continue _generating
                if ((GenerationData.flags & ChunkGenerationFlags.Mesh) != 0)
                {
                    //chunkMeshGenerator = new ChunkMeshGenerator(GenerationData, ref chunk.Voxels, ref chunk.HeightMap);
                    chunkParallelMeshGenerator = new ChunkParallelMeshGenerator(GenerationData, ref chunk.Voxels, ref chunk.HeightMap, _chunksManager);
                    MeshScheduled = true;
                }
            }
            chunkDataGenerator = null;
            return Completed;
        }

        if (ColliderScheduled && chunkColliderGenerator.IsComplete)
        {
            ColliderScheduled = false;
            GenerationData = chunkColliderGenerator.Complete();

            if (GenerationData.flags == ChunkGenerationFlags.None)
            {
                _chunksManager?.CompleteGeneratingChunk(GenerationData.position);
                Completed = true;
                Dispose();
                Processed -= 1;
            }
            chunkColliderGenerator = null;
            return Completed;
        }

        if (MeshScheduled && chunkParallelMeshGenerator.IsComplete)//chunkMeshGenerator.IsComplete)
        {
            MeshScheduled = false;
            //GenerationData = chunkMeshGenerator.Complete();
            GenerationData = chunkParallelMeshGenerator.Complete();

            if (GenerationData.flags == ChunkGenerationFlags.None)
            {
                _chunksManager?.CompleteGeneratingChunk(GenerationData.position);
                Completed = true;
                Dispose();
                Processed -= 1;
            }
            //chunkMeshGenerator = null;
            chunkParallelMeshGenerator = null;
            return Completed;
        }

        return Completed;
    }


    public async void StartGenerating(CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.WaitUntil(() => Complete(), cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("Generation was canceled.");
        }
    }

    public void Dispose(bool disposeData = false)
    {
        if (chunkDataGenerator != null)
            chunkDataGenerator.Dispose(disposeData);
        if (chunkMeshGenerator != null)
            chunkMeshGenerator.Dispose();
        if (chunkParallelMeshGenerator != null)
            chunkParallelMeshGenerator.Dispose();
        if (chunkColliderGenerator != null)
            chunkColliderGenerator.Dispose();
    }
}
