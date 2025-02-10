using UnityEngine;

public static class WorldSettings
{
    public static int Seed = 0;

    public static int ChunkWidth = 32;

    public static int ChunkHeight = 255;

    public static Bounds RecalculatedBounds
    {
        get
        {
            return new Bounds(new Vector3(ChunkWidth, ChunkHeight, ChunkWidth) * 0.5f, new Vector3(ChunkWidth, ChunkHeight, ChunkWidth));
        }
    }

    public static Bounds ChunkBounds = RecalculatedBounds;

    public static int TotalVoxelsInChunk => (ChunkWidth + 2) * (ChunkWidth + 2) * ChunkHeight;
    public static int RenderedVoxelsInChunk => ChunkWidth * ChunkWidth * ChunkHeight;

    public static bool ChunksInRange(Vector3 center, Vector3 position, int range)
    {
        return position.x <= center.x + range &&
            position.x >= center.x - range &&
            position.z <= center.z + range &&
            position.z >= center.z - range;
    }

    public static float ChunkRangeMagnitude(Vector3 center, Vector3 position)
    {
        return (position.x - center.x) * (position.x - center.x) + (position.z - center.z) * (position.z - center.z);
    }

    public static Vector3 ChunkPositionFromPosition(Vector3 pos)
    {
        pos /= ChunkWidth;
        pos.x = Mathf.FloorToInt(pos.x);
        pos.y = 0;
        pos.z = Mathf.FloorToInt(pos.z);
        return pos;
    }
}
