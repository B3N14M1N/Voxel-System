using UnityEngine;

namespace VoxelSystem.Managers
{
    /// <summary>
    /// 
    /// </summary>
    public interface IChunksManager
    {
        Vector3 Center { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="center"></param>
        void UpdateChunks(Vector3 center);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        Chunk GetChunk(Vector3 pos);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        void SetChunkToGenerating(Vector3 pos);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        void CompleteGeneratingChunk(Vector3 pos);
    }

}