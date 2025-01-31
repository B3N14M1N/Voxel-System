using System.Collections.Generic;
using UnityEngine;

namespace VoxelSystem.Data.Blocks
{
    /// <summary>
    /// 
    /// </summary>
    [CreateAssetMenu(fileName = "Blocks", menuName = "Voxel System/Blocks Catalogue", order = 0)]
    public class BlocksCatalogue : ScriptableObject
    {
        [field: SerializeField] public List<Block> Blocks { get; set; }
    }
}