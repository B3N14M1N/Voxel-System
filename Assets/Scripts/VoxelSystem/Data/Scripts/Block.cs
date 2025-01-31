using System;
using UnityEngine;

namespace VoxelSystem.Data
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
	public struct Block
	{
		[field: SerializeField] public VoxelType VoxelType {  get; private set; }
        [field: SerializeField] public VoxelPhysicsType VoxelPhysicsType { get; private set; }
        [field: SerializeField] public int AtlasTopTextureIndex { get; private set; }
        [field: SerializeField] public int AtlasSideTextureIndex { get; private set; }
    }
}