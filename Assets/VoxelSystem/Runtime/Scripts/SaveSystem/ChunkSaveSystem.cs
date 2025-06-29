using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using VoxelSystem.Data.Chunk;
using VoxelSystem.Data.Structs;
using VoxelSystem.Settings;
using Unity.Collections;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Threading;
using Cysharp.Threading.Tasks;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace VoxelSystem.SaveSystem
{

    /// <summary>
    /// Static class responsible for saving and loading chunk data efficiently using binary serialization.
    /// Handles disk I/O operations in a performant manner with optional compression.
    /// </summary>
    public static class ChunkSaveSystem
    {
        // How many save operations can happen concurrently
        private const int MAX_CONCURRENT_OPERATIONS = 8;

        // Current active save operations - recreated on initialization to prevent deadlocks between sessions
        private static SemaphoreSlim _saveSemaphore = new SemaphoreSlim(MAX_CONCURRENT_OPERATIONS);

        // Cache to avoid hitting the disk repeatedly for the same chunk
        private static readonly ConcurrentDictionary<string, DateTime> _chunkModificationCache = new ConcurrentDictionary<string, DateTime>();

        // File extension for chunk save files
        private const string CHUNK_EXTENSION = ".vchunk";

        // Compression level for chunk saves (None, Fastest, Optimal)
        private static readonly CompressionLevel _compressionLevel = CompressionLevel.Fastest;

        // Queue of files to be deleted when it's safe to do so
        private static readonly ConcurrentQueue<string> _filesToDelete = new ConcurrentQueue<string>();

        /// <summary>
        /// Gets the path for a chunk file based on its position.
        /// </summary>
        /// <param name="position">The position of the chunk</param>
        /// <returns>The file path for the chunk</returns>
        public static string GetChunkFilePath(Vector3 position)
        {
            if (WorldSettings.Instance == null)
            {
                Debug.LogError("WorldSettings.Instance is null when trying to get chunk file path!");
                return null;
            }

            string chunkFileName = $"chunk_{(int)position.x}_{(int)position.y}_{(int)position.z}{CHUNK_EXTENSION}";
            return Path.Combine(WorldSettings.Instance.PersistentChunksPath, chunkFileName);
        }

        /// <summary>
        /// Checks if a saved chunk exists at the specified position.
        /// </summary>
        /// <param name="position">The position of the chunk to check</param>
        /// <returns>True if saved data exists, false otherwise</returns>        
        public static bool ChunkSaveExists(Vector3 position)
        {
            string chunkFilePath = GetChunkFilePath(position);
            return File.Exists(chunkFilePath);
        }

        /// <summary>
        /// Asynchronously saves a chunk's data to disk if it's dirty.
        /// </summary>
        /// <param name="chunk">The chunk to save</param>
        /// <returns>A UniTask representing the save operation</returns>
        public static async UniTask SaveChunkAsync(ChunkDataHandler chunk)
        {
            if (!chunk.IsDirty)
            {
                return;
            }

            // Validate the semaphore to ensure it's not in a bad state
            if (_saveSemaphore == null)
            {
                Debug.LogWarning("Semaphore was null during SaveChunkAsync, reinitializing...");
                _saveSemaphore = new SemaphoreSlim(MAX_CONCURRENT_OPERATIONS);
            }

            try
            {
                // Use a timeout to prevent indefinite waiting if the semaphore is stuck
                bool acquired = await _saveSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                if (!acquired)
                {
                    Debug.LogWarning($"Timed out waiting for semaphore when saving chunk at {chunk.Position}, resetting semaphore");
                    _saveSemaphore.Dispose();
                    _saveSemaphore = new SemaphoreSlim(MAX_CONCURRENT_OPERATIONS);
                    return;
                }

                string chunkFilePath = GetChunkFilePath(chunk.Position);

                await SaveChunkInternalAsync(chunk, chunkFilePath);

                // Update the cache
                _chunkModificationCache[chunkFilePath] = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving chunk at {chunk.Position}: {ex.Message}");
            }
            finally
            {
                try
                {
                    // Only release if we have current waiters to avoid incrementing past the max count
                    if (_saveSemaphore != null && _saveSemaphore.CurrentCount < MAX_CONCURRENT_OPERATIONS)
                    {
                        _saveSemaphore.Release();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Semaphore was disposed, create a new one
                    _saveSemaphore = new SemaphoreSlim(MAX_CONCURRENT_OPERATIONS);
                }
            }
        }

        /// <summary>
        /// Immediately saves a chunk's data to disk if it's dirty.
        /// </summary>
        /// <param name="chunk">The chunk to save</param>
        public static void SaveChunk(Chunk chunk)
        {
            if (!chunk.Dirty)
            {
                return;
            }

            try
            {
                string chunkFilePath = GetChunkFilePath(chunk.Position);

                SaveChunkInternal(chunk, chunkFilePath);

                // Update the cache
                _chunkModificationCache[chunkFilePath] = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving chunk at {chunk.Position}: {ex.Message}");
            }
        }

        /// <summary>
        /// Internal method to handle the actual chunk data saving with compression.
        /// </summary>
        private static async UniTask SaveChunkInternalAsync(ChunkDataHandler chunk, string filePath)
        {
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None,
                   bufferSize: 4096, useAsync: true);
            using var compressStream = new GZipStream(fileStream, _compressionLevel);
            using var writer = new BinaryWriter(compressStream);
            // Write a version identifier for future compatibility
            writer.Write(1); // Version 1

            // Write position
            writer.Write(chunk.Position.x);
            writer.Write(chunk.Position.y);
            writer.Write(chunk.Position.z);

            // Write voxel data
            var voxels = chunk.VoxelsRef;
            writer.Write(voxels.Length);

            // Efficiently write the voxel data without copying
            await SaveVoxelDataAsync(writer, voxels);
            //chunk.DisposeVoxels();

            // Write height map
            var heightMap = chunk.HeightMapRef;
            writer.Write(heightMap.Length);

            // Efficiently write the height map data without copying
            await SaveHeightMapDataAsync(writer, heightMap);
            //chunk.DisposeHeightMap();
        }

        /// <summary>
        /// Synchronous version of the internal save method.
        /// </summary>
        private static void SaveChunkInternal(Chunk chunk, string filePath)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var compressStream = new GZipStream(fileStream, _compressionLevel))
            using (var writer = new BinaryWriter(compressStream))
            {
                // Write a version identifier for future compatibility
                writer.Write(1); // Version 1

                // Write position
                writer.Write(chunk.Position.x);
                writer.Write(chunk.Position.y);
                writer.Write(chunk.Position.z);

                // Write voxel data
                var voxels = chunk.Voxels;
                writer.Write(voxels.Length);

                // Efficiently write the voxel data
                SaveVoxelData(writer, ref voxels);

                // Write height map
                var heightMap = chunk.HeightMap;
                writer.Write(heightMap.Length);

                // Efficiently write the height map data
                SaveHeightMapData(writer, ref heightMap);
            }
        }

        /// <summary>
        /// Efficiently writes voxel data to the stream asynchronously.
        /// </summary>
        private static async UniTask SaveVoxelDataAsync(BinaryWriter writer, NativeArray<Voxel> voxels)
        {
            int length = voxels.Length;
            for (int i = 0; i < length; i++)
            {
                writer.Write(voxels[i].Get().GetRawValue());

                // Every 4096 voxels, allow other operations to proceed
                if (i % 4096 == 0 && i > 0)
                {
                    await UniTask.Yield();
                }
            }

            if (voxels.IsCreated) voxels.Dispose();
        }

        /// <summary>
        /// Efficiently writes voxel data to the stream synchronously.
        /// </summary>
        private static void SaveVoxelData(BinaryWriter writer, ref NativeArray<Voxel> voxels)
        {
            int length = voxels.Length;
            for (int i = 0; i < length; i++)
            {
                writer.Write(voxels[i].Get().GetRawValue());
            }
        }

        /// <summary>
        /// Efficiently writes height map data to the stream asynchronously.
        /// </summary>
        private static async UniTask SaveHeightMapDataAsync(BinaryWriter writer, NativeArray<HeightMap> heightMap)
        {
            int length = heightMap.Length;
            for (int i = 0; i < length; i++)
            {
                writer.Write(heightMap[i].GetSolid());

                // Every 2048 height values, allow other operations to proceed
                if (i % 2048 == 0 && i > 0)
                {
                    await UniTask.Yield();
                }
            }

            if (heightMap.IsCreated) heightMap.Dispose();
        }

        /// <summary>
        /// Efficiently writes height map data to the stream synchronously.
        /// </summary>
        private static void SaveHeightMapData(BinaryWriter writer, ref NativeArray<HeightMap> heightMap)
        {
            int length = heightMap.Length;
            for (int i = 0; i < length; i++)
            {
                writer.Write(heightMap[i].GetSolid());
            }
        }

        /// <summary>
        /// Asynchronously loads chunk data from disk if it exists.
        /// </summary>
        /// <param name="position">The position of the chunk to load</param>
        /// <returns>A UniTask containing the ChunkLoadResult with loaded data</returns>
        public static async UniTask<ChunkLoadResult> LoadChunkDataAsync(Vector3 position)
        {
            string chunkFilePath = GetChunkFilePath(position);

            if (!File.Exists(chunkFilePath))
            {
                return new ChunkLoadResult(false);
            }

            // Validate the semaphore to ensure it's not in a bad state
            if (_saveSemaphore == null)
            {
                Debug.LogWarning("Semaphore was null during LoadChunkDataAsync, reinitializing...");
                _saveSemaphore = new SemaphoreSlim(MAX_CONCURRENT_OPERATIONS);
            }

            try
            {
                // Use a timeout to prevent indefinite waiting if the semaphore is stuck
                bool acquired = await _saveSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                if (!acquired)
                {
                    Debug.LogWarning($"Timed out waiting for semaphore when loading chunk at {position}, resetting semaphore");
                    _saveSemaphore.Dispose();
                    _saveSemaphore = new SemaphoreSlim(MAX_CONCURRENT_OPERATIONS);
                    return new ChunkLoadResult(false);
                }

                return await LoadChunkInternalAsync(chunkFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading chunk at {position}: {ex.Message}");
                return new ChunkLoadResult(false);
            }
            finally
            {
                try
                {
                    // Only release if we have current waiters to avoid incrementing past the max count
                    if (_saveSemaphore != null && _saveSemaphore.CurrentCount < MAX_CONCURRENT_OPERATIONS)
                    {
                        _saveSemaphore.Release();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Semaphore was disposed, create a new one
                    _saveSemaphore = new SemaphoreSlim(MAX_CONCURRENT_OPERATIONS);
                }
            }
        }

        /// <summary>
        /// Synchronously loads chunk data from disk if it exists.
        /// </summary>
        /// <param name="position">The position of the chunk to load</param>
        /// <param name="voxels">Output parameter for the loaded voxel data</param>
        /// <param name="heightMap">Output parameter for the loaded height map data</param>
        /// <returns>True if data was loaded successfully, false otherwise</returns>
        public static ChunkLoadResult LoadChunkData(Vector3 position)
        {
            string chunkFilePath = GetChunkFilePath(position);

            if (!File.Exists(chunkFilePath))
            {
                return new ChunkLoadResult(false);
            }

            try
            {
                ChunkLoadResult result = LoadChunkInternal(chunkFilePath);
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading chunk at {position}: {ex.Message}");
                return new ChunkLoadResult(false);
            }
        }

        /// <summary>
        /// Internal method to handle the actual chunk data loading with decompression.
        /// </summary>
        private static async UniTask<ChunkLoadResult> LoadChunkInternalAsync(string filePath)
        {
            NativeArray<Voxel> voxels = default;
            NativeArray<HeightMap> heightMap = default;

            // Check if file is empty or too small to be valid
            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.Length < 20) // Minimum size for a valid chunk file
            {
                Debug.LogWarning($"Chunk file is too small or empty: {filePath}, size: {fileInfo.Length} bytes");
                // Schedule file for deletion rather than deleting immediately
                ScheduleFileForDeletion(filePath);
                return new ChunkLoadResult(false);
            }

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                   bufferSize: 4096, useAsync: true))
            using (var decompressStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var reader = new BinaryReader(decompressStream))
            {
                try
                {
                    // Read version
                    int version = reader.ReadInt32();

                    // Validate version
                    if (version != 1)
                    {
                        Debug.LogWarning($"Invalid chunk file version: {version} at {filePath}");
                        DeleteCorruptedFile(filePath);

                        if (voxels.IsCreated) voxels.Dispose();
                        if (heightMap.IsCreated) heightMap.Dispose();
                        return new ChunkLoadResult(false);
                    }

                    // Skip position as we already know it
                    reader.ReadSingle(); // x
                    reader.ReadSingle(); // y
                    reader.ReadSingle(); // z

                    // Read voxel data
                    int voxelCount = reader.ReadInt32();

                    // Check for unreasonable voxel count (corrupted data)
                    if (voxelCount <= 0 || voxelCount > WorldSettings.RenderedVoxelsInChunk)
                    {
                        Debug.LogWarning($"Invalid voxel count: {voxelCount} at {filePath}. Expected count: {WorldSettings.RenderedVoxelsInChunk}");
                        ScheduleFileForDeletion(filePath);

                        if (voxels.IsCreated) voxels.Dispose();
                        if (heightMap.IsCreated) heightMap.Dispose();
                        return new ChunkLoadResult(false);
                    }

                    voxels = new NativeArray<Voxel>(voxelCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                    await LoadVoxelDataAsync(reader, voxels, voxelCount);

                    // Read height map data
                    int heightMapCount = reader.ReadInt32();

                    int expectedMapCount = (WorldSettings.ChunkWidth + 2) * (WorldSettings.ChunkWidth + 2);
                    // Check for unreasonable heightmap count (corrupted data)
                    if (heightMapCount <= 0 || heightMapCount > expectedMapCount)
                    {
                        Debug.LogWarning($"Invalid heightmap count: {heightMapCount} at {filePath}. Expected count: {expectedMapCount}");
                        ScheduleFileForDeletion(filePath);

                        if (voxels.IsCreated) voxels.Dispose();
                        if (heightMap.IsCreated) heightMap.Dispose();
                        return new ChunkLoadResult(false);
                    }

                    heightMap = new NativeArray<HeightMap>(heightMapCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                    await LoadHeightMapDataAsync(reader, heightMap, heightMapCount);

                    return new ChunkLoadResult(voxels, heightMap);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"ChunkSaveSystem: {ex.Message}\n {ex.StackTrace}");
                    // Clean up allocated memory if an error occurs
                    if (voxels.IsCreated) voxels.Dispose();
                    if (heightMap.IsCreated) heightMap.Dispose();

                    // Schedule file for deletion after handle is closed
                    ScheduleFileForDeletion(filePath);

                    return new(false);
                }
            }
        }

        /// <summary>
        /// Synchronous version of the internal load method.
        /// </summary>
        private static ChunkLoadResult LoadChunkInternal(string filePath)
        {
            NativeArray<Voxel> voxels = default;
            NativeArray<HeightMap> heightMap = default;

            // Check if file is empty or too small to be valid
            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.Length < 20) // Minimum size for a valid chunk file
            {
                Debug.LogWarning($"Chunk file is too small or empty: {filePath}, size: {fileInfo.Length} bytes");
                ScheduleFileForDeletion(filePath);
                return new ChunkLoadResult(false);
            }

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var decompressStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var reader = new BinaryReader(decompressStream))
            {
                try
                {
                    // Read version
                    int version = reader.ReadInt32();

                    // Validate version
                    if (version != 1)
                    {
                        Debug.LogWarning($"Invalid chunk file version: {version} at {filePath}");
                        ScheduleFileForDeletion(filePath);

                        if (voxels.IsCreated) voxels.Dispose();
                        if (heightMap.IsCreated) heightMap.Dispose();
                        return new ChunkLoadResult(false);
                    }

                    // Skip position as we already know it
                    reader.ReadSingle(); // x
                    reader.ReadSingle(); // y
                    reader.ReadSingle(); // z

                    // Read voxel data
                    int voxelCount = reader.ReadInt32();

                    // Check for unreasonable voxel count (corrupted data)
                    if (voxelCount <= 0 || voxelCount > WorldSettings.RenderedVoxelsInChunk)
                    {
                        Debug.LogWarning($"Invalid voxel count: {voxelCount} at {filePath}. Expected count: {WorldSettings.RenderedVoxelsInChunk}");
                        ScheduleFileForDeletion(filePath);

                        if (voxels.IsCreated) voxels.Dispose();
                        if (heightMap.IsCreated) heightMap.Dispose();
                        return new ChunkLoadResult(false);
                    }

                    voxels = new NativeArray<Voxel>(voxelCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                    LoadVoxelData(reader, ref voxels, voxelCount);

                    // Read height map data
                    int heightMapCount = reader.ReadInt32();

                    int expectedMapCount = (WorldSettings.ChunkWidth + 2) * (WorldSettings.ChunkWidth + 2);
                    // Check for unreasonable heightmap count (corrupted data)
                    if (heightMapCount <= 0 || heightMapCount > expectedMapCount)
                    {
                        Debug.LogWarning($"Invalid heightmap count: {heightMapCount} at {filePath}. Expected count: {expectedMapCount}");
                        ScheduleFileForDeletion(filePath);

                        if (voxels.IsCreated) voxels.Dispose();
                        if (heightMap.IsCreated) heightMap.Dispose();
                        return new ChunkLoadResult(false);
                    }

                    heightMap = new NativeArray<HeightMap>(heightMapCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                    LoadHeightMapData(reader, ref heightMap, heightMapCount);
                    ChunkLoadResult result = new(voxels, heightMap);
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"ChunkSaveSystem: {ex.Message}\n {ex.StackTrace}");
                    // Clean up allocated memory if an error occurs
                    if (voxels.IsCreated) voxels.Dispose();
                    if (heightMap.IsCreated) heightMap.Dispose();

                    // Delete corrupted file
                    ScheduleFileForDeletion(filePath);
                    return new(false);
                }
            }
        }

        /// <summary>
        /// Efficiently reads voxel data from the stream asynchronously.
        /// </summary>
        private static async UniTask LoadVoxelDataAsync(BinaryReader reader, NativeArray<Voxel> voxels, int count)
        {
            for (int i = 0; i < count; i++)
            {
                byte value = reader.ReadByte();
                voxels[i] = new Voxel(value);

                // Every 4096 voxels, allow other operations to proceed
                if (i % PlayerSettings.VoxelsToLoadFromFile == 0 && i > 0)
                {
                    await UniTask.Yield();
                }
            }
        }

        /// <summary>
        /// Efficiently reads voxel data from the stream synchronously.
        /// </summary>
        private static void LoadVoxelData(BinaryReader reader, ref NativeArray<Voxel> voxels, int count)
        {
            for (int i = 0; i < count; i++)
            {
                byte value = reader.ReadByte();
                voxels[i] = new Voxel(value);
            }
        }

        /// <summary>
        /// Efficiently reads height map data from the stream asynchronously.
        /// </summary>        
        private static async UniTask LoadHeightMapDataAsync(BinaryReader reader, NativeArray<HeightMap> heightMap, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int value = reader.ReadInt32();
                heightMap[i] = new HeightMap(value);

                // Every 2048 height values, allow other operations to proceed
                if (i % 2048 == 0 && i > 0)
                {
                    await UniTask.Yield();
                }
            }
        }

        /// <summary>
        /// Efficiently reads height map data from the stream synchronously.
        /// </summary>
        private static void LoadHeightMapData(BinaryReader reader, ref NativeArray<HeightMap> heightMap, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int value = reader.ReadInt32();
                heightMap[i] = new HeightMap(value);
            }
        }

        /// <summary>
        /// Adds an extension method to VoxelType to get the raw byte value.
        /// </summary>
        private static byte GetRawValue(this VoxelType type) => (byte)type;

        /// <summary>
        /// Deletes saved chunk data if it exists.
        /// </summary>
        /// <param name="position">The position of the chunk to delete</param>
        /// <returns>True if the file was deleted, false otherwise</returns>
        public static bool DeleteChunkSave(Vector3 position)
        {
            string chunkFilePath = GetChunkFilePath(position);

            if (File.Exists(chunkFilePath))
            {
                try
                {
                    File.Delete(chunkFilePath);

                    // Remove from cache
                    _chunkModificationCache.TryRemove(chunkFilePath, out _);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error deleting chunk file at {position}: {ex.Message}");
                }
            }

            return false;
        }

        /// <summary>
        /// Deletes all saved chunks for the current world.
        /// </summary>
        /// <returns>The number of chunk files deleted</returns>
        public static int DeleteAllChunkSaves()
        {
            int count = 0;

            if (WorldSettings.Instance == null) return count;

            string chunksPath = WorldSettings.Instance.PersistentChunksPath;

            if (Directory.Exists(chunksPath))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(chunksPath, $"*{CHUNK_EXTENSION}"))
                    {
                        File.Delete(file);
                        count++;
                    }

                    // Clear the cache
                    _chunkModificationCache.Clear();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error deleting chunk files: {ex.Message}");
                }
            }

            return count;
        }

        /// <summary>
        /// Initializes the chunk save system directory structure.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // Reset the semaphore to prevent deadlocks between sessions
                if (_saveSemaphore != null)
                {
                    _saveSemaphore.Dispose();
                }
                _saveSemaphore = new SemaphoreSlim(MAX_CONCURRENT_OPERATIONS);

                // Make sure world settings are available
                if (WorldSettings.Instance == null)
                {
                    Debug.LogWarning("WorldSettings.Instance is null, cannot initialize ChunkSaveSystem.");
                    return;
                }

                // Ensure the chunks directory exists
                if (!Directory.Exists(WorldSettings.Instance.PersistentChunksPath))
                {
                    Directory.CreateDirectory(WorldSettings.Instance.PersistentChunksPath);
                    Debug.Log($"Created chunk save directory at: {WorldSettings.Instance.PersistentChunksPath}");
                }

                // Clear the cache on initialization
                _chunkModificationCache.Clear();

                if (WorldSettings.HasDebugging)
                    Debug.Log("ChunkSaveSystem initialized successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize ChunkSaveSystem: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up the chunk save system resources, particularly the semaphore.
        /// Should be called when the application is exiting.
        /// </summary>
        public static void Cleanup()
        {
            try
            {                    // Process any remaining files to delete
                    int maxAttempts = 3;
                    int currentAttempt = 0;
                    
                    // Force garbage collection to help release file handles
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    
                    // Process files in queue with multiple attempts
                    while (_filesToDelete.Count > 0 && currentAttempt < maxAttempts)
                    {
                        int startingCount = _filesToDelete.Count;
                        int filesProcessed = 0;
                        
                        // Try to process all files in the queue
                        while (_filesToDelete.TryDequeue(out string filePath))
                        {
                            try
                            {
                                if (File.Exists(filePath))
                                {
                                    File.Delete(filePath);
                                    Debug.Log($"Cleanup: Deleted file {filePath}");
                                    filesProcessed++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"Cleanup: Failed to delete file {filePath}: {ex.Message}");
                                // Re-queue the file for the next attempt
                                _filesToDelete.Enqueue(filePath);
                            }
                        }
                        
                        // If we couldn't delete any files this attempt, try a GC and wait
                        if (filesProcessed == 0 && _filesToDelete.Count == startingCount)
                        {
                            currentAttempt++;
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            Thread.Sleep(200); // Wait a bit before next attempt
                        }
                        else
                        {
                            // Reset attempts if we made progress
                            currentAttempt = 0;
                        }
                    }
                    
                    // Log any files we couldn't delete after all attempts
                    if (_filesToDelete.Count > 0)
                    {
                        Debug.LogWarning($"Unable to delete {_filesToDelete.Count} files after multiple attempts during cleanup.");
                        _filesToDelete.Clear(); // Clear the queue anyway to avoid memory leaks
                    }
                
                if (_saveSemaphore != null)
                {
                    _saveSemaphore.Dispose();
                    _saveSemaphore = null;
                }

                _chunkModificationCache.Clear();

                if (WorldSettings.HasDebugging)
                    Debug.Log("ChunkSaveSystem cleaned up successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during ChunkSaveSystem cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a corrupted chunk file and extracts position from the file path
        /// </summary>
        /// <param name="filePath">The path to the corrupted chunk file</param>
        private static void DeleteCorruptedFile(string filePath)
        {
            try
            {
                // Extract position from file path to delete the corrupted file
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (fileName.StartsWith("chunk_"))
                {
                    string[] parts = fileName.Substring(6).Split('_');
                    if (parts.Length == 3 &&
                        int.TryParse(parts[0], out int x) &&
                        int.TryParse(parts[1], out int y) &&
                        int.TryParse(parts[2], out int z))
                    {
                        Vector3 position = new Vector3(x, y, z);
                        Debug.LogWarning($"Deleting corrupted chunk file at position {position}");
                        DeleteChunkSave(position);
                    }
                    else
                    {
                        // If we can't parse the position, delete the file directly
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            Debug.LogWarning($"Directly deleted corrupted chunk file: {filePath}");
                        }
                    }
                }
                else
                {
                    // If filename doesn't follow the expected pattern
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        Debug.LogWarning($"Directly deleted corrupted chunk file: {filePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete corrupted chunk file: {ex.Message}");
            }
        }

        /// <summary>
        /// Schedules a file for deletion when it's safe to do so (streams are closed)
        /// </summary>
        private static void ScheduleFileForDeletion(string filePath)
        {
            // Add file to deletion queue
            _filesToDelete.Enqueue(filePath);
            
            // Process the deletion queue if not too many files pending
            if (_filesToDelete.Count < 20)
            {
                ProcessFileDeletionQueue();
            }
        }
        
        /// <summary>
        /// Process queued files for deletion
        /// </summary>
        private static void ProcessFileDeletionQueue()
        {
            // Spawn a task to handle the deletion to avoid blocking
            _ = UniTask.RunOnThreadPool(async () =>
            {
                try
                {
                    // Wait briefly to ensure file handles are closed
                    await UniTask.Delay(250); // Increased delay to give more time for handles to close
                    
                    // Force garbage collection to release file handles
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    
                    int maxAttempts = 3;
                    int batchSize = 10; // Process files in batches
                    List<string> batch = new List<string>(batchSize);
                    
                    // Get a batch of files to process
                    for (int i = 0; i < batchSize && _filesToDelete.TryDequeue(out string path); i++)
                    {
                        batch.Add(path);
                    }
                    
                    // Process each file in the batch
                    foreach (var filePath in batch)
                    {
                        bool deleted = false;
                        
                        for (int attempt = 0; attempt < maxAttempts && !deleted; attempt++)
                        {
                            try
                            {
                                if (File.Exists(filePath))
                                {
                                    // Extract position from file path
                                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                                    if (fileName.StartsWith("chunk_"))
                                    {
                                        string[] parts = fileName.Substring(6).Split('_');
                                        if (parts.Length == 3 &&
                                            int.TryParse(parts[0], out int x) &&
                                            int.TryParse(parts[1], out int y) &&
                                            int.TryParse(parts[2], out int z))
                                        {
                                            Vector3 position = new Vector3(x, y, z);
                                            string chunkFilePath = GetChunkFilePath(position);
                                            
                                            // Remove from cache
                                            _chunkModificationCache.TryRemove(chunkFilePath, out _);
                                        }
                                    }
                                    
                                    // Direct delete using File.Delete
                                    File.Delete(filePath);
                                    Debug.Log($"Successfully deleted file: {filePath}");
                                    deleted = true;
                                }
                                else
                                {
                                    // File doesn't exist anymore, consider it deleted
                                    deleted = true;
                                }
                            }
                            catch (IOException ex)
                            {
                                if (attempt == maxAttempts - 1)
                                {
                                    Debug.LogWarning($"Failed to delete file {filePath} after {maxAttempts} attempts: {ex.Message}");
                                    _filesToDelete.Enqueue(filePath); // Re-queue for later
                                }
                                else
                                {
                                    // Try again after waiting
                                    await UniTask.Delay(100 * (attempt + 1)); // Exponential backoff
                                    GC.Collect();
                                    GC.WaitForPendingFinalizers();
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Error deleting file {filePath}: {ex.Message}");
                                deleted = true; // Don't retry on non-IO exceptions
                            }
                        }
                    }
                    
                    // If there are more files and we didn't hit an error, process the next batch
                    if (_filesToDelete.Count > 0)
                    {
                        await UniTask.Delay(100); // Brief delay before next batch
                        ProcessFileDeletionQueue();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in file deletion queue processing: {ex.Message}");
                }
            });
        }
    }
}