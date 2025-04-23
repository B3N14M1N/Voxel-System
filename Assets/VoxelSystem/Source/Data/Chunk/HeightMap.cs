using System;

namespace VoxelSystem.Data
{
    /// <summary>
    /// Represents height data for a specific column in a chunk.
    /// </summary>
    [Serializable]
    public struct HeightMap
    {
        private byte _solid;

        /// <summary>
        /// Creates a new height map entry with the specified solid height.
        /// </summary>
        /// <param name="solid">The height value</param>
        public HeightMap(byte solid)
        {
            _solid = solid;
        }

        /// <summary>
        /// Sets the solid height value.
        /// </summary>
        /// <param name="height">The new height value</param>
        public void SetSolid(byte height)
        {
            _solid = height;
        }

        /// <summary>
        /// Gets the solid height value.
        /// </summary>
        /// <returns>The current height value</returns>
        public readonly byte GetSolid() => _solid;
    };
}