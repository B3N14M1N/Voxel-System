using System;
using UnityEngine;

namespace VoxelSystem.Data.Blocks
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
	public struct Block
	{
		[field: SerializeField] public VoxelType VoxelType {  get; private set; }
        [field: SerializeField] public Texture2D TopTexture { get; private set; }
        [field: SerializeField] public Texture2D SideTexture { get; private set; }
    }
}