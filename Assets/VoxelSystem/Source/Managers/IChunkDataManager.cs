using VoxelSystem.Data.Chunk;

namespace VoxelSystem.Managers
{
    /// <summary>
    /// 
    /// </summary>
    public interface IChunkDataManager
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chunkData"></param>
        void AddChunkData(ChunkData chunkData);
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chunkData"></param>
        void RemoveChunkData(ChunkData chunkData);
    }
}