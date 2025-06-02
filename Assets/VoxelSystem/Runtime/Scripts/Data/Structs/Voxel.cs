using System;

namespace VoxelSystem.Data.Structs
{
    /// <summary>
    /// Represents a single voxel element with type information.
    /// </summary>
    [Serializable]
    public struct Voxel : IComparable<Voxel>, IEquatable<Voxel>
    {
        /// <summary>
        /// Gets whether this voxel is empty (air).
        /// </summary>
        public readonly bool IsEmpty => _type == 0;

        /// <summary>
        /// Gets an empty voxel instance.
        /// </summary>
        public static Voxel Empty => new();

        private byte _type;

        /// <summary>
        /// Creates a new voxel with the specified type.
        /// </summary>
        /// <param name="type">The type of the voxel as a byte value</param>
        public Voxel(byte type)
        {
            _type = type;
        }

        /// <summary>
        /// Sets the type of this voxel.
        /// </summary>
        /// <param name="type">The type to set</param>
        public void Set(VoxelType type)
        {
            _type = (byte)type;
        }

        /// <summary>
        /// Gets the type of this voxel.
        /// </summary>
        /// <returns>The voxel type</returns>
        public readonly VoxelType Get() => (VoxelType)_type;

        /// <summary>
        /// Compares this voxel with another voxel for equality.
        /// </summary>
        /// <param name="other">The other voxel to compare with</param>
        /// <returns>True if the voxels are equal, false otherwise</returns>
        public readonly bool Equals(Voxel other) => _type == other._type;

        /// <summary>
        /// Compares this voxel with another voxel for ordering.
        /// </summary>
        /// <param name="other">The other voxel to compare with</param>
        /// <returns>An integer indicating the relative order of the voxels</returns>
        public readonly int CompareTo(Voxel other) => _type.CompareTo(other._type);

    };
}