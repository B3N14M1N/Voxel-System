using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using VoxelSystem.Data.Chunk;
using VoxelSystem.Data.GenerationFlags;
using VoxelSystem.Factory;
using VoxelSystem.Managers;
using VoxelSystem.Settings;

namespace VoxelSystem.Generators
{
    /// <summary>
    /// Manages the asynchronous generation of chunk components including data, mesh and collider.
    /// </summary>
    public class ChunkJob
    {
        private IChunkGenerator _chunkDataGenerator;
        private IChunkGenerator _chunkColliderGenerator;
        private IChunkGenerator _chunkMeshGenerator;
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

        public static void ResetProcessed()
        {
            Processed = 0;
        }
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

            if (!CheckAndScheduleDataGeneration())
            {
                _chunk = _chunksManager?.GetChunk(GenerationData.position);

                if (_chunk != null)
                {
                    _regenerating = CheckAndScheduleColliderGeneration() | CheckAndScheduleMeshGeneration();
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
            if (false && WorldSettings.HasDebugging)
                Debug.Log($"ChunkJob: GenerationData" +
                $"\nHasData = {GenerationData.flags.HasFlag(ChunkGenerationFlags.Data)}" +
                $"\nHasCollider = {GenerationData.flags.HasFlag(ChunkGenerationFlags.Collider)}" +
                $"\nHasMesh = {GenerationData.flags.HasFlag(ChunkGenerationFlags.Mesh)}");

            return CompleteDataGeneration() | CompleteColliderGeneration() | CompleteMeshGeneration();
        }

        /// <summary>
        /// Checks if data generation is flagged for generation and initializes the data generator.
        /// </summary>
        /// <returns>True if the generation started, False if no generation scheduled</returns>
        private bool CheckAndScheduleDataGeneration()
        {
            if (!((GenerationData.flags & ChunkGenerationFlags.Data) != 0)) return false;

            var parameters = ChunkFactory.Instance.NoiseParameters;
            _chunkDataGenerator = new ChunkDataGenerator(GenerationData, parameters, _chunksManager);
            _dataScheduled = true;
            _completed = false;
            Processed += 1;
            return true;
        }

        /// <summary>
        /// Checks if collider generation is flagged for generation and initializes the collider generator.
        /// </summary>
        /// <returns>True if the generation started, False if no generation scheduled</returns>
        private bool CheckAndScheduleColliderGeneration()
        {
            if (!((GenerationData.flags & ChunkGenerationFlags.Collider) != 0)) return false;

            if (WorldSettings.HasDebugging && _chunk.HeightMap.Length <= 0)
                Debug.LogError($"ChunkJob: CheckAndScheduleColliderGeneration -> HeightMap is empty on Chunk: {_chunk.GetHashCode()}");

            _chunkColliderGenerator = new ChunkColliderGenerator(GenerationData, ref _chunk.HeightMap, _chunksManager);
            _colliderScheduled = true;
            _completed = false;
            return true;
        }

        /// <summary>
        /// Checks if mesh generation is flagged for generation and initializes the mesh generator.
        /// </summary>
        /// <returns>True if the generation started, False if no generation scheduled</returns>
        private bool CheckAndScheduleMeshGeneration()
        {
            if (!((GenerationData.flags & ChunkGenerationFlags.Mesh) != 0)) return false;

            if (WorldSettings.HasDebugging && _chunk.HeightMap.Length <= 0)
                Debug.LogError($"ChunkJob: CheckAndScheduleMeshGeneration -> HeightMap is empty on Chunk: {_chunk.GetHashCode()}");

            _chunkMeshGenerator = new ChunkMeshGenerator(GenerationData, ref _chunk.Voxels, ref _chunk.HeightMap, _chunksManager);
            _meshScheduled = true;
            _completed = false;
            return true;
        }

        /// <summary>
        /// Completes the data generation job and applies the data to the chunk.
        /// Then checks if the generation is done or disposed, and schedules further tasks if needed.
        /// </summary>
        /// <returns>True if the data generation is complete</returns>
        private bool CompleteDataGeneration()
        {
            if (!(_dataScheduled && _chunkDataGenerator.IsComplete))
                return _completed;

            _dataScheduled = true;
            _dataScheduled = false;
            GenerationData = _chunkDataGenerator.Complete();
            
            if (IsGenerationDone() || IsGenerationDisposed())
            {
                if (!IsGenerationDisposed())
                    _chunksManager?.CompleteGeneratingChunk(GenerationData.position);

                _completed = true;
                Processed -= 1;
                Dispose();
            }
            else
            {
                CheckAndScheduleColliderGeneration();
                CheckAndScheduleMeshGeneration();
            }

            _chunkDataGenerator = null;
            return _completed;
        }

        /// <summary>
        /// Completes the collider generation job and applies the collider to the chunk.
        /// </summary>
        /// <returns>True if the collider generation is complete</returns>
        private bool CompleteColliderGeneration()
        {
            if (!(_colliderScheduled && _chunkColliderGenerator.IsComplete))
                return _completed;

            _colliderScheduled = false;
            GenerationData = _chunkColliderGenerator.Complete();

            if (IsGenerationDone() || IsGenerationDisposed())
            {
                if (!IsGenerationDisposed())
                    _chunksManager?.CompleteGeneratingChunk(GenerationData.position);

                _completed = true;
                Dispose();

                if (!_regenerating)
                    Processed -= 1;
            }

            _chunkColliderGenerator = null;
            return _completed;
        }

        /// <summary>
        /// Completes the mesh generation job and applies the mesh to the chunk.
        /// </summary>
        /// <returns>True if the mesh generation is complete</returns>
        private bool CompleteMeshGeneration()
        {
            if (!(_meshScheduled && _chunkMeshGenerator.IsComplete))
                return _completed;

            _meshScheduled = false;
            GenerationData = _chunkMeshGenerator.Complete();

            if (IsGenerationDone() || IsGenerationDisposed())
            {
                if (!IsGenerationDisposed())
                    _chunksManager?.CompleteGeneratingChunk(GenerationData.position);

                _completed = true;
                Dispose();

                if (!_regenerating)
                    Processed -= 1;
            }

            _chunkMeshGenerator = null;
            return _completed;
        }


        /// <summary>
        /// Checks if the generation is done by examining the flags in GenerationData.
        /// </summary>
        private bool IsGenerationDone()
        {
            return GenerationData.flags == ChunkGenerationFlags.None;
        }

        /// <summary>
        /// Checks if the generation has been disposed, which means it will not be processed further.
        /// </summary>
        private bool IsGenerationDisposed()
        {
            return GenerationData.flags == ChunkGenerationFlags.Disposed;
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
}
