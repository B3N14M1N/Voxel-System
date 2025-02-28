using System;

namespace VoxelSystem.Data
{
    public enum VoxelPhysicsType
    {
        none = 0,
        solid = 1,
        liquid = 2,
    };

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
        private static readonly ushort VOXEL_TYPE_MASK = 0x00FF;
        private static readonly ushort EXCLUDE_VOXEL_TYPE_MASK = 0xFF00;

        private static readonly ushort PHYSICS_TYPE_MASK = 0xFF00;
        private static readonly ushort EXCLUDE_PHYSICS_TYPE_MASK = 0x00FF;
        public readonly bool IsEmpty => ID == 0;
        public static Voxel EmptyVoxel => new();

        public ushort ID;

        public void SetPhysicsType(VoxelPhysicsType type)
        {
            ID = (ushort)((ID & EXCLUDE_PHYSICS_TYPE_MASK) + (ushort)(((ushort)type << 8) & PHYSICS_TYPE_MASK));
        }

        public void SetVoxelType(VoxelType type)
        {
            ID = (ushort)((ID & EXCLUDE_VOXEL_TYPE_MASK) + (ushort)(((ushort)type) & VOXEL_TYPE_MASK));
        }

        public readonly VoxelPhysicsType GetPhysicsType() => (VoxelPhysicsType)((ID & PHYSICS_TYPE_MASK) >> 8);

        public readonly VoxelType GetVoxelType() => (VoxelType)(ID & VOXEL_TYPE_MASK);
    }; 
}