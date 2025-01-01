using System.Linq;
using UnityEngine;

public class ChunkJob
{
    private ChunkDataGenerator chunkDataGenerator;
    private ChunkColliderGenerator chunkColliderGenerator;
    private ChunkMeshGenerator chunkMeshGenerator;
    private ChunkParallelMeshGenerator chunkParallelMeshGenerator;

    public static int Processed { get; private set; }
    public GenerationData GenerationData {  get; private set; }
    private bool Completed = false;

    private bool DataScheduled;
    private bool DataGenerated;

    private bool ColliderScheduled;
    private bool ColliderGenerated;

    private bool MeshScheduled;
    private bool MeshGenerated;

    private Chunk chunk;

    public ChunkJob(GenerationData generationData)
    {
        GenerationData = generationData;
        Completed = true;
        DataScheduled = false;
        DataGenerated = false; 
        ColliderScheduled = false;
        ColliderGenerated = false;
        MeshScheduled = false;
        MeshGenerated = false;

        if ((GenerationData.flags & ChunkGenerationFlags.Data) != 0)
        {
            var parameters = ChunkFactory.Instance.NoiseParameters;
            var octaves = ChunkFactory.Instance.NoiseOctaves;
            chunkDataGenerator = new ChunkDataGenerator(GenerationData, parameters.noise.ToArray(), octaves, parameters.globalScale);
            DataScheduled = true;
            DataGenerated = false;
            Completed = false;
            Processed += 1;
        }
        else
        {
            DataGenerated = true;
            chunk = ChunksManager.Instance.GetChunk(GenerationData.position);
            if (chunk != null)
            {
                if ((GenerationData.flags & ChunkGenerationFlags.Collider) != 0)
                {
                    chunkColliderGenerator = new ChunkColliderGenerator(GenerationData, ref chunk.HeightMap);
                    ColliderScheduled = true;
                    ColliderGenerated = false;
                    Completed = false;
                }
                if ((GenerationData.flags & ChunkGenerationFlags.Mesh) != 0)
                {
                    //chunkMeshGenerator = new ChunkMeshGenerator(GenerationData, ref chunk.Voxels, ref chunk.HeightMap);
                    chunkParallelMeshGenerator = new ChunkParallelMeshGenerator(GenerationData, ref chunk.Voxels, ref chunk.HeightMap);
                    MeshScheduled = true;
                    MeshGenerated = false;
                    Completed = false;
                }
            }
        }
        if (!Completed)
        {
            ChunksManager.Instance.SetChunkToGenerating(GenerationData.position);
            chunk = ChunksManager.Instance.GetChunk(GenerationData.position);
        }
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
                ChunksManager.Instance.CompleteGeneratingChunk(GenerationData.position);
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
                    chunkColliderGenerator = new ChunkColliderGenerator(GenerationData, ref chunk.HeightMap);
                    ColliderScheduled = true;
                    ColliderGenerated = false;
                }

                // if the flags wants a mesh, continue _generating
                if ((GenerationData.flags & ChunkGenerationFlags.Mesh) != 0)
                {
                    //chunkMeshGenerator = new ChunkMeshGenerator(GenerationData, ref chunk.Voxels, ref chunk.HeightMap);
                    chunkParallelMeshGenerator = new ChunkParallelMeshGenerator(GenerationData, ref chunk.Voxels, ref chunk.HeightMap);
                    MeshScheduled = true;
                    MeshGenerated = false;
                }
            }
            chunkDataGenerator = null;
            return Completed;
        }

        if (ColliderScheduled && chunkColliderGenerator.IsComplete)
        {
            ColliderGenerated = true;
            ColliderScheduled = false;
            GenerationData = chunkColliderGenerator.Complete();

            if (GenerationData.flags == ChunkGenerationFlags.None)
            {
                ChunksManager.Instance.CompleteGeneratingChunk(GenerationData.position);
                Completed = true;
                Dispose();
                Processed -= 1;
            }
            chunkColliderGenerator = null;
            return Completed;
        }

        if (MeshScheduled  && chunkParallelMeshGenerator.IsComplete)//chunkMeshGenerator.IsComplete)
        {
            MeshGenerated = true; 
            MeshScheduled = false;
            //GenerationData = chunkMeshGenerator.Complete();
            GenerationData = chunkParallelMeshGenerator.Complete();

            if (GenerationData.flags == ChunkGenerationFlags.None)
            {
                ChunksManager.Instance.CompleteGeneratingChunk(GenerationData.position);
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

    public void Dispose(bool disposeData = false)
    {
        if(chunkDataGenerator != null)
            chunkDataGenerator.Dispose(disposeData);
        if(chunkMeshGenerator != null)
            chunkMeshGenerator.Dispose();
        if (chunkParallelMeshGenerator != null)
            chunkParallelMeshGenerator.Dispose();
        if (chunkColliderGenerator != null)
            chunkColliderGenerator.Dispose();
    }
}
