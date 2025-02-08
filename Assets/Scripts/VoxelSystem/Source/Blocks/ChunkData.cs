using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
namespace VoxelSystem.Data
{
    public struct ChunkData
    {
        public NativeArray<Voxel> Voxels;
        public NativeArray<HeightMap> Map;
        public float2 HeigthBounds;
    }
}
