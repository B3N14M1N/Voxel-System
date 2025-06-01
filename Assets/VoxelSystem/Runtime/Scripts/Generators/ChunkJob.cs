using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using VoxelSystem.Data.GenerationFlags;
using VoxelSystem.Factory;
using VoxelSystem.Generators;
using VoxelSystem.Managers;

/// <summary>
/// Manages the asynchronous generation of chunk components including data, mesh and collider.
/// </summary>
public class ChunkJob
{
    private ChunkDataGenerator _chunkDataGenerator;
    private ChunkColliderGenerator _chunkColliderGenerator;
    private IChunkMeshGenerator _chunkMeshGenerator;
    private readonly IChunksManager _chunksManager;
    private readonly Chunk _chunk;
    private readonly bool _regenerating = false;
    private bool _completed = false;
    private bool _dataScheduled;
    private bool _colliderScheduled;
    private bool _meshScheduled;

    /// <summary>
    /// Gets the generation data for this job, containing position and generation flags.
    /// </summary>
    public GenerationData GenerationData { get; private set; }

    /// <summary>
    /// Gets the count of actively processing jobs.
    /// </summary>
    public static int Processed { get; private set; }

    /// <summary>
    /// Creates a new chunk generation job and initiates processing.
    /// </summary>
    /// <param name="generationData">The data specifying what to generate</param>
    /// <param name="chunksManager">The manager responsible for chunk lifecycle</param>
    /// <param name="cancellationToken">Token to cancel the job</param>
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
                    _chunkMeshGenerator = new ChunkMeshGenerator(GenerationData, ref _chunk.Voxels, ref _chunk.HeightMap, _chunksManager);
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
    /// Checks and processes completed generation tasks, potentially scheduling subsequent tasks.
    /// </summary>
    /// <returns>True if all requested generation is complete</returns>
    public bool Complete()
    {

        if (_dataScheduled && _chunkDataGenerator.IsComplete)
        {
            _dataScheduled = true;
            _dataScheduled = false;
            GenerationData = _chunkDataGenerator.Complete();

            if (GenerationData.flags == ChunkGenerationFlags.None || GenerationData.flags == ChunkGenerationFlags.Disposed)
            {
                if (GenerationData.flags != ChunkGenerationFlags.Disposed)
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
                    _chunkMeshGenerator = new ChunkMeshGenerator(GenerationData, ref _chunk.Voxels, ref _chunk.HeightMap, _chunksManager);
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

            if (GenerationData.flags == ChunkGenerationFlags.None || GenerationData.flags == ChunkGenerationFlags.Disposed)
            {
                if (GenerationData.flags != ChunkGenerationFlags.Disposed)
                    _chunksManager?.CompleteGeneratingChunk(GenerationData.position);
                _completed = true;
                Dispose();

                if (_regenerating == false)
                    Processed -= 1;
            }
            _chunkColliderGenerator = null;
            return _completed;
        }

        if (_meshScheduled && _chunkMeshGenerator.IsComplete)
        {
            _meshScheduled = false;
            GenerationData = _chunkMeshGenerator.Complete();

            if (GenerationData.flags == ChunkGenerationFlags.None || GenerationData.flags == ChunkGenerationFlags.Disposed)
            {
                if (GenerationData.flags != ChunkGenerationFlags.Disposed)
                    _chunksManager?.CompleteGeneratingChunk(GenerationData.position);
                _completed = true;
                Dispose();

                if (_regenerating == false)
                    Processed -= 1;
            }
            _chunkMeshGenerator = null;
            return _completed;
        }

        return _completed;
    }

    /// <summary>
    /// Begins the asynchronous generation process.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
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
    /// Releases resources used by this job.
    /// </summary>
    /// <param name="disposeData">Whether to also dispose data resources</param>
    public void Dispose(bool disposeData = false)
    {
        _chunkDataGenerator?.Dispose(disposeData);
        _chunkMeshGenerator?.Dispose();
        _chunkColliderGenerator?.Dispose();
    }
}
