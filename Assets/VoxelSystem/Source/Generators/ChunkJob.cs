using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using VoxelSystem.Data.GenerationFlags;
using VoxelSystem.Factory;
using VoxelSystem.Generators;
using VoxelSystem.Managers;

/// <summary>
/// Represents and manages the process of generating different components (data, mesh, collider) for a single chunk based on specified flags.
/// </summary>
public class ChunkJob
{

    private ChunkDataGenerator _chunkDataGenerator;
    private ChunkColliderGenerator _chunkColliderGenerator;
    private ChunkParallelMeshGenerator _chunkParallelMeshGenerator;
    private readonly IChunksManager _chunksManager;
    private readonly Chunk _chunk;
    private readonly bool _regenerating = false;
    private bool _completed = false;
    private bool _dataScheduled;
    private bool _colliderScheduled;
    private bool _meshScheduled;

    /// <summary>
    /// Gets the generation data associated with this job, including position and flags.
    /// </summary>
    public GenerationData GenerationData { get; private set; }

    /// <summary>
    /// Static counter tracking the number of active or processed jobs.
    /// </summary>
    public static int Processed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkJob"/> class, setting up the required generators based on flags and starting the process.
    /// </summary>
    public ChunkJob(GenerationData generationData,
        IChunksManager chunksManager,
        CancellationToken cancellationToken)
    {
        GenerationData = generationData;
        _completed = true;
        _dataScheduled = false;
        _colliderScheduled = false;
        _meshScheduled = false;
        _chunksManager = chunksManager;

        if ((GenerationData.flags & ChunkGenerationFlags.Data) != 0)
        {
            var parameters = ChunkFactory.Instance.NoiseParameters;
            _chunkDataGenerator = new ChunkDataGenerator(GenerationData, parameters, _chunksManager);
            _dataScheduled = true;
            _completed = false;
            Processed += 1;
        }
        else
        {
            _chunk = _chunksManager?.GetChunk(GenerationData.position);
            if (_chunk != null)
            {
                if ((GenerationData.flags & ChunkGenerationFlags.Collider) != 0)
                {
                    _chunkColliderGenerator = new ChunkColliderGenerator(GenerationData, ref _chunk.HeightMap, _chunksManager);
                    _colliderScheduled = true;
                    _regenerating = true;
                    _completed = false;
                }
                if ((GenerationData.flags & ChunkGenerationFlags.Mesh) != 0)
                {
                    _chunkParallelMeshGenerator = new ChunkParallelMeshGenerator(GenerationData, ref _chunk.Voxels, ref _chunk.HeightMap, _chunksManager);
                    _meshScheduled = true;
                    _regenerating = true;
                    _completed = false;
                }
            }
        }

        if (!_completed)
        {
            _chunksManager?.SetChunkToGenerating(GenerationData.position);
            _chunk = _chunksManager?.GetChunk(GenerationData.position);
        }

        StartGenerating(cancellationToken);
    }

    /// <summary>
    /// Checks the status of scheduled generation tasks (data, collider, mesh) and processes their completion, potentially scheduling subsequent tasks.
    /// </summary>
    /// <returns>True if the entire job (all requested flags) is completed, false otherwise.</returns>
    public bool Complete()
    {
        if (_dataScheduled && _chunkDataGenerator.IsComplete)
        {
            _dataScheduled = true; // Original code had this redundant assignment
            _dataScheduled = false;
            GenerationData = _chunkDataGenerator.Complete();

            if (GenerationData.flags == ChunkGenerationFlags.None)
            {
                _chunksManager?.CompleteGeneratingChunk(GenerationData.position);
                _completed = true;
                Dispose();
                Processed -= 1;
            }
            else
            {
                if ((GenerationData.flags & ChunkGenerationFlags.Collider) != 0)
                {
                    _chunkColliderGenerator = new ChunkColliderGenerator(GenerationData, ref _chunk.HeightMap, _chunksManager);
                    _colliderScheduled = true;
                }
                if ((GenerationData.flags & ChunkGenerationFlags.Mesh) != 0)
                {
                    _chunkParallelMeshGenerator = new ChunkParallelMeshGenerator(GenerationData, ref _chunk.Voxels, ref _chunk.HeightMap, _chunksManager);
                    _meshScheduled = true;
                }
            }
            _chunkDataGenerator = null;
            return _completed;
        }

        if (_colliderScheduled && _chunkColliderGenerator.IsComplete)
        {
            _colliderScheduled = false;
            GenerationData = _chunkColliderGenerator.Complete();

            if (GenerationData.flags == ChunkGenerationFlags.None)
            {
                _chunksManager?.CompleteGeneratingChunk(GenerationData.position);
                _completed = true;
                Dispose();
                if (_regenerating == false)
                    Processed -= 1;
            }
            _chunkColliderGenerator = null;
            return _completed;
        }

        if (_meshScheduled && _chunkParallelMeshGenerator.IsComplete)
        {
            _meshScheduled = false;
            GenerationData = _chunkParallelMeshGenerator.Complete();

            if (GenerationData.flags == ChunkGenerationFlags.None)
            {
                _chunksManager?.CompleteGeneratingChunk(GenerationData.position);
                _completed = true;
                Dispose();
                if (_regenerating == false)
                    Processed -= 1;
            }
            _chunkParallelMeshGenerator = null;
            return _completed;
        }

        return _completed;
    }

    /// <summary>
    /// Asynchronously waits until the generation job is fully completed or cancellation is requested.
    /// </summary>
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

    /// <summary>
    /// Disposes the internal generator instances used by this job.
    /// </summary>
    public void Dispose(bool disposeData = false)
    {
        _chunkDataGenerator?.Dispose(disposeData);
        _chunkParallelMeshGenerator?.Dispose();
        _chunkColliderGenerator?.Dispose();
    }
}
