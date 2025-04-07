using System;

namespace VoxelSystem.Data
{
    [Serializable]
    public struct HeightMap
    {
        private byte _solid;

        public HeightMap(byte solid)
        {
            _solid = solid;
        }

        public void SetSolid(byte height)
        {
            _solid = height;
        }

        public readonly byte GetSolid() => _solid;
    };

}