using System;
using UnityEngine;

namespace VoxelSystem.Data.Blocks
{
    /// <summary>
    /// Represents the data for a specific type of voxel block, including its type and textures.
    /// This struct is serializable and can be configured in the Unity Inspector.
    /// </summary>
    [Serializable]
    public struct Block
    {
        /// <summary>
        /// Gets the type of the voxel represented by this block.
        /// </summary>
        [field: SerializeField] public VoxelType VoxelType { get; private set; }

        /// <summary>
        /// Gets the texture used for the top face of the block.
        /// </summary>
        [field: SerializeField] public Texture2D TopTexture { get; private set; }

        /// <summary>
        /// Gets the texture used for the side faces of the block.
        /// </summary>
        [field: SerializeField] public Texture2D SideTexture { get; private set; }
    }
}
