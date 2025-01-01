using UnityEngine;

public interface IChunksManager
{
    void UpdateChunks(Vector3 center);
    Chunk GetChunk(Vector3 pos);
    void SetChunkToGenerating(Vector3 pos);
    void CompleteGeneratingChunk(Vector3 pos);
}
