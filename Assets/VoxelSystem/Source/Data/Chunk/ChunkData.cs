using Unity.Collections;
using UnityEngine;

namespace VoxelSystem.Data.Chunk
{
    /// <summary>
    /// 
    /// </summary>
    public struct ChunkData
    {
        private NativeArray<Voxel> _voxels;
        private NativeArray<HeightMap> _heightMap;
        public bool Dirty { get; private set; }
        public readonly NativeArray<Voxel>.ReadOnly Voxels => _voxels.AsReadOnly();
        public readonly NativeArray<HeightMap>.ReadOnly HeightMap => _heightMap.AsReadOnly();

        public ChunkData(NativeArray<Voxel> voxels, NativeArray<HeightMap> heightMap)
        {
            Dirty = false;
            _voxels = voxels;
            _heightMap = heightMap;
        }

        public Voxel this[Vector3 pos]
        {
            get
            {
                return _voxels[GetVoxelIndex(pos)];
            }

            private set
            {
                int index = GetVoxelIndex(pos);
                if (_voxels[index].IsEmpty)
                    _voxels[index] = value;
            }
        }

        public Voxel this[int x, int y, int z]
        {
            get
            {
                return _voxels[GetVoxelIndex(x, y, z)];
            }

            private set
            {
                int index = GetVoxelIndex(x, y, z);
                if (_voxels[index].IsEmpty)
                    _voxels[index] = value;
            }
        }

        public HeightMap this[int x, int z]
        {
            get
            {
                return _heightMap[GetMapIndex(x, z)];
            }
            private set
            {
                int index = GetMapIndex(x, z);
                _heightMap[index] = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        private readonly int GetVoxelIndex(int x, int y, int z)
        {
            return z + (y * (WorldSettings.ChunkWidth + 2)) + (x * (WorldSettings.ChunkWidth + 2) * WorldSettings.ChunkHeight);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        private readonly int GetVoxelIndex(Vector3 pos)
        {
            return GetVoxelIndex((int)pos.x, (int)pos.y, (int)pos.z);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        private readonly int GetMapIndex(int x, int z)
        {
            return z + x * (WorldSettings.ChunkWidth + 2);
        }
    }
}