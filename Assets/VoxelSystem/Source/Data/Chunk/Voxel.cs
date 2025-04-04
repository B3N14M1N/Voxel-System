using System;

namespace VoxelSystem.Data
{
    public enum VoxelType
    {
        air = 0,
        grass = 1,
        dirt = 2,
        stone = 3,
        sand = 4,
    }

    [Serializable]
    public struct Voxel
    {
        public readonly bool IsEmpty => ID == 0;
        public static Voxel Empty => new();

        public byte ID;

        public void SetVoxelType(VoxelType type)
        {
            ID = (byte)type;
        }

        public readonly VoxelType GetVoxelType() => (VoxelType)(ID);
    }; 
}