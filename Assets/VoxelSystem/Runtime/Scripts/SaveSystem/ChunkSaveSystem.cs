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
using System.Threading.Tasks;

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

        // Current active save operations
        private static readonly SemaphoreSlim _saveSemaphore = new SemaphoreSlim(MAX_CONCURRENT_OPERATIONS);

        // Cache to avoid hitting the disk repeatedly for the same chunk
        private static readonly ConcurrentDictionary<string, DateTime> _chunkModificationCache = new ConcurrentDictionary<string, DateTime>();

        // File extension for chunk save files
        private const string CHUNK_EXTENSION = ".vchunk";

        // Compression level for chunk saves (None, Fastest, Optimal)
        private static readonly CompressionLevel _compressionLevel = CompressionLevel.Fastest;

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

            try
            {
                await _saveSemaphore.WaitAsync();

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
                _saveSemaphore.Release();
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

            try
            {
                await _saveSemaphore.WaitAsync();

                return await LoadChunkInternalAsync(chunkFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading chunk at {position}: {ex.Message}");
                return new ChunkLoadResult(false);
            }
            finally
            {
                _saveSemaphore.Release();
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

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                   bufferSize: 4096, useAsync: true))
            using (var decompressStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var reader = new BinaryReader(decompressStream))
            {
                try
                {
                    // Read version
                    int version = reader.ReadInt32();

                    // Skip position as we already know it
                    reader.ReadSingle(); // x
                    reader.ReadSingle(); // y
                    reader.ReadSingle(); // z

                    // Read voxel data
                    int voxelCount = reader.ReadInt32();
                    voxels = new NativeArray<Voxel>(voxelCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                    await LoadVoxelDataAsync(reader, voxels, voxelCount);

                    // Read height map data
                    int heightMapCount = reader.ReadInt32();
                    heightMap = new NativeArray<HeightMap>(heightMapCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                    await LoadHeightMapDataAsync(reader, heightMap, heightMapCount);

                    return new ChunkLoadResult(voxels, heightMap);
                }
                catch
                {
                    // Clean up allocated memory if an error occurs
                    if (voxels.IsCreated) voxels.Dispose();
                    if (heightMap.IsCreated) heightMap.Dispose();
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

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var decompressStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var reader = new BinaryReader(decompressStream))
            {
                try
                {
                    // Read version
                    int version = reader.ReadInt32();

                    // Skip position as we already know it
                    reader.ReadSingle(); // x
                    reader.ReadSingle(); // y
                    reader.ReadSingle(); // z

                    // Read voxel data
                    int voxelCount = reader.ReadInt32();
                    voxels = new NativeArray<Voxel>(voxelCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                    LoadVoxelData(reader, ref voxels, voxelCount);

                    // Read height map data
                    int heightMapCount = reader.ReadInt32();
                    heightMap = new NativeArray<HeightMap>(heightMapCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                    LoadHeightMapData(reader, ref heightMap, heightMapCount);
                    ChunkLoadResult result = new(voxels, heightMap);
                    return result;
                }
                catch
                {
                    // Clean up allocated memory if an error occurs
                    if (voxels.IsCreated) voxels.Dispose();
                    if (heightMap.IsCreated) heightMap.Dispose();
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
                if (i % 4096 == 0 && i > 0)
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
        }        /// <summary>
                 /// Initializes the chunk save system directory structure.
                 /// </summary>
        public static void Initialize()
        {
            try
            {
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
    }
}
