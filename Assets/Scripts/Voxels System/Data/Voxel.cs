public struct Voxel
{
    public ushort ID;
    public readonly bool IsEmpty => ID == 0;
    public static Voxel EmptyVoxel => new Voxel();
};

public struct HeightMap
{
    public uint data;
};

public enum VoxelPhysicsType
{
    none = 0,
    solid = 1,
    liquid = 2,
};

public enum VoxelType
{
    air = 0,
    dirt = 1,
    mud = 2,
    water = 3,
    rock = 4,
    sand = 5,
}

public static class RWStructs
{
    #region Voxel

    private static readonly ushort VOXEL_TYPE_MASK = 0x00FF;
    private static readonly ushort EXCLUDE_VOXEL_TYPE_MASK = 0xFF00;

    private static readonly ushort PHYSICS_TYPE_MASK = 0xFF00;
    private static readonly ushort EXCLUDE_PHYSICS_TYPE_MASK = 0x00FF;

    public static void SetPhysicsType(ref Voxel voxel, VoxelPhysicsType type)
    {
        voxel.ID = (ushort)((voxel.ID & EXCLUDE_PHYSICS_TYPE_MASK) + (ushort)(((ushort)type << 8) & PHYSICS_TYPE_MASK));
    }

    public static void SetVoxelType(ref Voxel voxel, VoxelType type)
    {
        voxel.ID = (ushort)((voxel.ID & EXCLUDE_VOXEL_TYPE_MASK) + (ushort)(((ushort)type) & VOXEL_TYPE_MASK));
    }

    public static VoxelPhysicsType GetPhysicsType(Voxel voxel) => (VoxelPhysicsType)((int)((voxel.ID & PHYSICS_TYPE_MASK) >> 8));

    public static VoxelType GetVoxelType(Voxel voxel) => (VoxelType)((int)(voxel.ID & VOXEL_TYPE_MASK));
    #endregion

    #region HeightMap

    private static readonly uint SOLID_MASK = 0x000000FF;
    private static readonly uint EXCLUDE_SOLID_MASK = 0xFFFFFF00;

    private static readonly uint LIQUID_BOTTOM_MASK = 0x0000FF00;
    private static readonly uint EXCLUDE_LIQUID_BOTTOM_MASK = 0xFFFF00FF;

    private static readonly uint LIQUID_TOP_MASK = 0x00FF0000;
    private static readonly uint EXCLUDE_LIQUID_TOP_MASK = 0xFF00FFFF;

    public static void SetSolid(ref HeightMap map, uint height)
    {
        map.data = (map.data & EXCLUDE_SOLID_MASK) + height;
    }

    public static void SetLiquidTop(ref HeightMap map, uint height)
    {
        map.data = (map.data & EXCLUDE_LIQUID_TOP_MASK) + (height << 16);
    }

    public static void SetLiquidBottom(ref HeightMap map, uint height)
    {
        map.data = (map.data & EXCLUDE_LIQUID_BOTTOM_MASK) + (height << 8);
    }
    public static uint GetSolid(HeightMap map) => map.data & SOLID_MASK;

    public static uint GetLiquidTop(HeightMap map) => (map.data & LIQUID_TOP_MASK) >> 16;

    public static uint GetLiquidBottom(HeightMap map) => (map.data & LIQUID_BOTTOM_MASK) >> 8;
    #endregion
}
