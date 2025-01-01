public static class PlayerSettings
{
    public static int RenderDistance = 20;
    public static int CacheDistance = 5;
    public static int SimulateDistance = 5;

    // limits how many chunks to generate - it dilutes the workload and uses less memory per frame
    public static int ChunksProcessed = 1;
    public static int ChunksToLoad = 1;
    public static float TimeToLoadNextChunks = 0.01667f;//seconds
}
