using System;
using UnityEngine;
using VoxelSystem.Data.Structs;

namespace VoxelSystem.Data.Blocks
{
    /// <summary>
    /// Represents a voxel block with texture information.
    /// </summary>
    [Serializable]
    public struct Block
    {
        /// <summary>
        /// Gets the type of voxel this block represents.
        /// </summary>
        [field: SerializeField] public VoxelType VoxelType { get; private set; }

        /// <summary>
        /// Gets the texture applied to the top face of the block.
        /// </summary>
        [field: SerializeField] public Texture2D TopTexture { get; private set; }

        /// <summary>
        /// Gets the texture applied to the side faces of the block.
        /// </summary>
        [field: SerializeField] public Texture2D SideTexture { get; private set; }
    }
}
