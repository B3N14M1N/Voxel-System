using UnityEngine;
using VoxelSystem.Data;

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
        void GenerateChunksPositionsCheck();

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

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        (int, int, int, int, int) ChunksMeshAndColliderSize();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mV"></param>
        /// <param name="mI"></param>
        void UpdateChunkMeshSize(int mV, int mI);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cV"></param>
        /// <param name="cI"></param>
        void UpdateChunkColliderSize(int cV, int cI);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="voxel"></param>
        /// <returns></returns>
        bool ModifyVoxel(int x, int y, int z, Voxel voxel);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="worldPos"></param>
        /// <param name="voxel"></param>
        /// <returns></returns>
        bool ModifyVoxel(Vector3 worldPos, Voxel voxel);
    }

}