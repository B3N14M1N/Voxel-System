using VoxelSystem.Generators;

namespace VoxelSystem.Settings
{
    /// <summary>
    /// Contains settings for the player in the voxel system.
    /// </summary>
    public static class PlayerSettings
    {
        public static int RenderDistance { get; set; } = 20;
        public static int CacheDistance { get; set; } = 5;
        public static int SimulateDistance { get; set; } = 5;
        public static MeshGeneratorType MeshGeneratorType { get; set; } = MeshGeneratorType.Parallel;
        public static int ChunksProcessed { get; set; } = 1;
        public static int ChunksToLoad { get; set; } = 1;
        public static float TimeToLoadNextChunks { get; set; } = 1f / 60f; // 0.01667f seconds
    }
}