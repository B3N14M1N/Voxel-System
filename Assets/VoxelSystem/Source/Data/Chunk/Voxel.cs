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
        public readonly bool IsEmpty => _type == 0;
        public static Voxel Empty => new();

        private byte _type;
        public Voxel(byte type)
        {
            _type = type;
        }

        public void SetVoxelType(VoxelType type)
        {
            _type = (byte)type;
        }

        public readonly VoxelType GetVoxelType() => (VoxelType)(_type);
    }; 
}