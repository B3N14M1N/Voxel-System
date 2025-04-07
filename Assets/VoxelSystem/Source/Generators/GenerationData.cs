using UnityEngine;

namespace VoxelSystem.Data.GenerationFlags
{
    public class GenerationData
    {
        public Vector3 position;
        public ChunkGenerationFlags flags;
    }

    public enum ChunkGenerationFlags
    {
        None = 0,
        Data = 1,
        Collider = 2,
        Mesh = 4,
    };
}