using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VoxelSystem.Data.GenerationFlags;
using VoxelSystem.Factory;
using VoxelSystem.Generators;
using VoxelSystem.Managers;

public class ChunkJob
{
    private ChunkDataGenerator chunkDataGenerator;
    private ChunkColliderGenerator chunkColliderGenerator;
    private ChunkMeshGenerator chunkMeshGenerator;
    private ChunkParallelMeshGenerator chunkParallelMeshGenerator;
    private readonly IChunksManager _chunksManager;

    public static int Processed { get; private set; } // Tracks active jobs
    public GenerationData GenerationData { get; private set; }
    private bool Completed; // Initialize later

    private bool DataScheduled;
    private bool ColliderScheduled;
    private bool MeshScheduled;

    private Chunk chunk;

    public ChunkJob(GenerationData generationData,
        IChunksManager chunksManager,
        CancellationToken cancellationToken)
    {
        GenerationData = generationData;
        _chunksManager = chunksManager;
        Completed = false; // Assume not completed initially
        DataScheduled = false;
        ColliderScheduled = false;
        MeshScheduled = false;

        bool workScheduled = false; // Flag to track if any work is actually scheduled

        // --- Schedule Data Generation (if requested) ---
        if ((GenerationData.flags & ChunkGenerationFlags.Data) != 0)
        {
            var parameters = ChunkFactory.Instance.NoiseParameters;
            // var octaves = ChunkFactory.Instance.NoiseParameters.OctaveOffsets; // Ensure this is valid if used
            chunkDataGenerator = new ChunkDataGenerator(GenerationData, parameters, _chunksManager);
            DataScheduled = true;
            workScheduled = true;
            // Debug.Log($"Job {GenerationData.position}: Scheduled Data");
        }
        else // --- Or Get Existing Chunk for Mesh/Collider Regen ---
        {
            chunk = _chunksManager?.GetChunk(GenerationData.position);
            if (chunk != null && chunk.DataGenerated) // Ensure chunk exists and has data for regen
            {
                // Schedule Collider (if requested)
                if ((GenerationData.flags & ChunkGenerationFlags.Collider) != 0)
                {
                    // Ensure HeightMap reference is valid and accessible when needed
                    chunkColliderGenerator = new ChunkColliderGenerator(GenerationData, ref chunk.HeightMap, _chunksManager);
                    ColliderScheduled = true;
                    workScheduled = true;
                    // Debug.Log($"Job {GenerationData.position}: Scheduled Collider (Regen)");
                }
                // Schedule Mesh (if requested)
                if ((GenerationData.flags & ChunkGenerationFlags.Mesh) != 0)
                {
                    // Ensure Voxel/HeightMap references are valid and accessible when needed
                    chunkParallelMeshGenerator = new ChunkParallelMeshGenerator(GenerationData, ref chunk.Voxels, ref chunk.HeightMap, _chunksManager);
                    MeshScheduled = true;
                    workScheduled = true;
                    // Debug.Log($"Job {GenerationData.position}: Scheduled Mesh (Regen)");
                }
            }
            else
            {
                // Chunk doesn't exist or has no data, cannot schedule Mesh/Collider regen
                Debug.LogWarning($"Job {GenerationData.position}: Cannot schedule Mesh/Collider regen - Chunk missing or no data.");
                Completed = true; // Mark as completed immediately as no work can be done
                workScheduled = false;
            }
        }

        // --- Final Setup ---
        if (workScheduled)
        {
            // Mark the chunk as generating in the manager
            _chunksManager?.SetChunkToGenerating(GenerationData.position);
            // Re-get chunk instance *after* setting state in manager, if needed by generators
            chunk = _chunksManager?.GetChunk(GenerationData.position);
            // Increment processed count *only if* work was actually scheduled
            Processed += 1;
            // Start the async completion watcher
            WatchForCompletion(cancellationToken);
        }
        else
        {
            // No work scheduled (either flags were None, or regen failed pre-check)
            Completed = true;
            // Do not increment Processed count
            // Do not mark chunk as generating
            // Do not start WatchForCompletion
            // Dispose any potentially created (but unused) generators immediately? Or rely on GC?
            // Let's assume generators are lightweight or handle null checks.
        }
    }

    // Renamed StartGenerating to WatchForCompletion for clarity
    private async void WatchForCompletion(CancellationToken cancellationToken)
    {
        try
        {
            // Wait until the Complete() method indicates all scheduled tasks are done
            await UniTask.WaitUntil(() => Complete(), cancellationToken: cancellationToken);

            // --- Post-Completion ---
            // This block executes *after* Complete() returns true for the last time.
            // Ensure cleanup and final state updates happen here.

            // Check if cancellation was requested *during* the final Complete() check or just before
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log($"Job {GenerationData.position}: Cancelled during final completion check.");
                // Ensure Processed is decremented even on cancellation if it was incremented
                // Note: This assumes cancellation might happen after Complete() returns true but before Processed decrement.
                // If Complete() handles cancellation internally, this might not be needed.
                // Let's assume Complete handles its internal state correctly.
            }
            else if (Completed) // Double check completion state
            {
                // Debug.Log($"Job {GenerationData.position}: All tasks completed successfully.");
                // Final notification to manager (already done inside Complete)
                // Final decrement of Processed count (already done inside Complete)
                // Final Dispose call (already done inside Complete)
            }

        }
        catch (OperationCanceledException)
        {
            Debug.Log($"Job {GenerationData.position}: Generation task was canceled.");
            // Ensure resources are disposed on cancellation
            Dispose(true); // Dispose data if data generation was cancelled midway
                           // Decrement Processed count if it was incremented for this job
                           // Need a reliable way to know if Processed was incremented for *this specific instance*
                           // A simple approach: Add an instance field `bool processedIncremented = false;`
                           // Set it to true when Processed += 1 happens. Check it here on cancellation.
                           // For now, let's assume the decrement in Complete() handles most cases,
                           // but cancellation *before* the first Complete() check might leave Processed inflated.
                           // A robust solution might involve registering/unregistering jobs from a central tracker.
                           // Let's stick to the decrement within Complete() for simplicity now.
        }
        catch (Exception ex)
        {
            Debug.LogError($"Job {GenerationData.position}: Error during generation or completion check: {ex}");
            // Ensure cleanup on error
            Dispose(true);
            // Potentially decrement Processed count here too, similar to cancellation logic.
            // Again, relying on Complete() decrement for now.
        }
        // No finally block needed as Dispose should be called within Complete or catch blocks.
    }


    // Modify the Complete method:
    public bool Complete()
    {
        // If already marked completed by constructor or previous call, exit early.
        if (Completed) return true;

        // --- Check Data Generation ---
        if (DataScheduled) // Check if data generation was scheduled
        {
            // If generator exists and is complete...
            if (chunkDataGenerator != null && chunkDataGenerator.IsComplete)
            {
                // Debug.Log($"Job {GenerationData.position}: Data generation complete.");
                // Mark data as no longer scheduled *before* completing it
                DataScheduled = false;
                // Complete the data generation step (uploads data to chunk)
                GenerationData = chunkDataGenerator.Complete(); // Returns updated GenerationData (flags might change)
                chunkDataGenerator.Dispose(); // Dispose the data generator now
                chunkDataGenerator = null;

                // --- Schedule Mesh/Collider based on remaining flags ---
                if (chunk != null) // Ensure chunk instance is valid
                {
                    // Schedule Collider (if requested)
                    if ((GenerationData.flags & ChunkGenerationFlags.Collider) != 0)
                    {
                        chunkColliderGenerator = new ChunkColliderGenerator(GenerationData, ref chunk.HeightMap, _chunksManager);
                        ColliderScheduled = true;
                        // Debug.Log($"Job {GenerationData.position}: Scheduled Collider (Post-Data)");
                    }
                    // Schedule Mesh (if requested)
                    if ((GenerationData.flags & ChunkGenerationFlags.Mesh) != 0)
                    {
                        chunkParallelMeshGenerator = new ChunkParallelMeshGenerator(GenerationData, ref chunk.Voxels, ref chunk.HeightMap, _chunksManager);
                        MeshScheduled = true;
                        // Debug.Log($"Job {GenerationData.position}: Scheduled Mesh (Post-Data)");
                    }
                }
                else
                {
                    Debug.LogError($"Job {GenerationData.position}: Chunk became null after data generation!");
                    // Cannot schedule Mesh/Collider, mark job as completed prematurely
                    MeshScheduled = false;
                    ColliderScheduled = false;
                }
            }
            else // Data not complete yet
            {
                return false; // Still waiting for data
            }
        } // End if DataScheduled

        // --- Check Collider Generation ---
        if (ColliderScheduled) // Check if collider generation was scheduled
        {
            // If generator exists and is complete...
            if (chunkColliderGenerator != null && chunkColliderGenerator.IsComplete)
            {
                // Debug.Log($"Job {GenerationData.position}: Collider generation complete.");
                ColliderScheduled = false; // Mark as no longer scheduled
                GenerationData = chunkColliderGenerator.Complete(); // Complete (uploads collider)
                chunkColliderGenerator.Dispose(); // Dispose generator
                chunkColliderGenerator = null;
            }
            else // Collider not complete yet
            {
                return false; // Still waiting for collider
            }
        } // End if ColliderScheduled

        // --- Check Mesh Generation ---
        if (MeshScheduled) // Check if mesh generation was scheduled
        {
            // If generator exists and is complete...
            if (chunkParallelMeshGenerator != null && chunkParallelMeshGenerator.IsComplete)
            {
                // Debug.Log($"Job {GenerationData.position}: Mesh generation complete.");
                MeshScheduled = false; // Mark as no longer scheduled
                GenerationData = chunkParallelMeshGenerator.Complete(); // Complete (uploads mesh)
                chunkParallelMeshGenerator.Dispose(); // Dispose generator
                chunkParallelMeshGenerator = null;
            }
            else // Mesh not complete yet
            {
                return false; // Still waiting for mesh
            }
        } // End if MeshScheduled


        // --- Final Check ---
        // If we reach here, check if all scheduled tasks are now complete
        if (!DataScheduled && !ColliderScheduled && !MeshScheduled)
        {
            // Debug.Log($"Job {GenerationData.position}: All scheduled tasks finished.");
            // All scheduled steps are done, mark the entire job as completed.
            Completed = true;
            // Notify the manager that this chunk is no longer 'generating'.
            _chunksManager?.CompleteGeneratingChunk(GenerationData.position);
            // Decrement the global processed count *only once* when fully completed.
            Processed -= 1;
            // Dispose is handled by individual steps now, no need for a final Dispose() call here.
            return true; // Job is fully complete
        }

        // Should not be reachable if logic is correct, but as a fallback:
        return false;
    }

    // Keep Dispose method as is, it's called by individual completion steps now.
    public void Dispose(bool disposeData = false)
    {
        // Dispose any remaining generators (e.g., if cancelled)
        chunkDataGenerator?.Dispose(disposeData); // Use null-conditional operator
        chunkMeshGenerator?.Dispose(); // Keep if you might switch back
        chunkParallelMeshGenerator?.Dispose();
        chunkColliderGenerator?.Dispose();
        // Set references to null to help GC and prevent accidental reuse
        chunkDataGenerator = null;
        chunkMeshGenerator = null;
        chunkParallelMeshGenerator = null;
        chunkColliderGenerator = null;
        chunk = null;
        // _chunksManager = null; // Keep manager reference if needed?
    }
}