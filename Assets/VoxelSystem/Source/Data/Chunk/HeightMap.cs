using System;

namespace VoxelSystem.Data
{
    [Serializable]
    public struct HeightMap
    {
        private static readonly uint SOLID_MASK = 0x000000FF;
        private static readonly uint EXCLUDE_SOLID_MASK = 0xFFFFFF00;

        private static readonly uint LIQUID_BOTTOM_MASK = 0x0000FF00;
        private static readonly uint EXCLUDE_LIQUID_BOTTOM_MASK = 0xFFFF00FF;

        private static readonly uint LIQUID_TOP_MASK = 0x00FF0000;
        private static readonly uint EXCLUDE_LIQUID_TOP_MASK = 0xFF00FFFF;

        public uint data;


        public void SetSolid(uint height)
        {
            data = (data & EXCLUDE_SOLID_MASK) + height;
        }

        public void SetLiquidTop(uint height)
        {
            data = (data & EXCLUDE_LIQUID_TOP_MASK) + (height << 16);
        }

        public void SetLiquidBottom(uint height)
        {
            data = (data & EXCLUDE_LIQUID_BOTTOM_MASK) + (height << 8);
        }

        public readonly uint GetSolid() => data & SOLID_MASK;

        public readonly uint GetLiquidTop() => (data & LIQUID_TOP_MASK) >> 16;

        public readonly uint GetLiquidBottom() => (data & LIQUID_BOTTOM_MASK) >> 8;
    };

}