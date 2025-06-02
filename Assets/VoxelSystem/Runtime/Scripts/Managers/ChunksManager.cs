using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using VoxelSystem.Data.Chunk;
using VoxelSystem.Data.GenerationFlags;
using VoxelSystem.Data.Structs;
using VoxelSystem.Factory;
using VoxelSystem.Generators;
using VoxelSystem.Settings;
using VoxelSystem.Utils;

namespace VoxelSystem.Managers
{
    /// <summary>
    /// Manages chunk lifecycle, including creation, updating, and caching based on player position.
    /// </summary>
    public class ChunksManager : IChunksManager, IDisposable
    {
        public const string ChunksManagerLoopString = "ChunksManagerLoop";
        private CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// Gets the center position around which chunks are generated.
        /// </summary>
        public Vector3 Center { get; private set; }

        private readonly Queue<Chunk> _pool = new();
        private readonly Queue<Chunk> _chunksToClear = new();
        private readonly Transform _parent;

        [SerializeField] private LayerMask _chunkLayer;
        private Dictionary<Vector3, Chunk> _active = new();
        private Dictionary<Vector3, Chunk> _cached = new();
        private Dictionary<Vector3, Chunk> _generating = new();
        private Vector3[] _chunksPositionCheck;

        /// <summary>
        /// Creates a new chunk instance or retrieves one from the pool.
        /// </summary>
        private Chunk NewChunk
        {
            get
            {
                Chunk chunk = new(Vector3.zero, this, _chunkLayer, _parent)
                {
                    Active = false
                };

                return chunk;
            }
        }

        /// <summary>
        /// Creates a new chunks manager with optional layer mask and parent transform.
        /// </summary>
        /// <param name="layerMask">The layer mask to apply to chunks</param>
        /// <param name="transform">The parent transform for chunks</param>
        public ChunksManager(LayerMask layerMask = default, Transform transform = null)
        {
            _parent = transform;
            _chunkLayer = layerMask;
            for (int i = 0; i < (PlayerSettings.RenderDistance + PlayerSettings.CacheDistance) * (PlayerSettings.RenderDistance + PlayerSettings.CacheDistance); i++)
            {
                _pool.Enqueue(NewChunk);
            }

            WorldSettings.OnWorldSettingsChanged += GenerateChunksPositionsCheck;
            WorldSettings.OnWorldChanged += Dispose;
            WorldSettings.OnWorldChanged += GenerateChunksPositionsCheck;

            GenerateChunksPositionsCheck();
        }

        /// <summary>
        /// Generates an array of positions around the center to check for visible chunks.
        /// </summary>
        public void GenerateChunksPositionsCheck()
        {
            var job = new ChunksRenderView(Vector3.zero, PlayerSettings.RenderDistance);
            _chunksPositionCheck = job.Complete();
            job.Dispose();

            // Reinitialize the chunks with the new positions
            UpdateChunks(Center);
        }

        /// <summary>
        /// Updates the visible chunks around a new center position.
        /// </summary>
        /// <param name="center">The new center position</param>
        public void UpdateChunks(Vector3 center)
        {
            var UpdateTime = Time.realtimeSinceStartup;
            Center = center;
            foreach (var kvp in _active)
            {
                _cached[kvp.Key] = kvp.Value;
            }
            _active.Clear();

            var chunksToGenerate = new List<GenerationData>();

            for (int i = 0; i < _chunksPositionCheck.Length; i++)
            {
                Vector3 key = new(_chunksPositionCheck[i].x + Center.x, 0, _chunksPositionCheck[i].z + Center.z);
                var data = new GenerationData()
                {
                    position = key,
                    flags = ChunkGenerationFlags.Data | ChunkGenerationFlags.Collider | ChunkGenerationFlags.Mesh,
                };

                if (_cached.TryGetValue(key, out Chunk chunk))
                {
                    chunk.Active = true;
                    chunk.Render = true;
                    _active.Add(key, chunk);
                    _cached.Remove(key);
                    continue;
                }

                if (!_generating.TryGetValue(key, out _))
                {
                    chunksToGenerate.Add(data);
                }
            }

            if (chunksToGenerate.Count > 0)
                ChunkFactory.Instance.GenerateChunksData(chunksToGenerate);

            AvgCounter.UpdateCounter(ChunksManagerLoopString, (Time.realtimeSinceStartup - UpdateTime) * 1000f);

            ClearChunksAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// Clears a chunk and returns it to the pool or disposes it.
        /// </summary>
        /// <param name="chunk">The chunk to clear</param>
        private void ClearChunkAndEnqueue(Chunk chunk)
        {
            chunk.ClearChunk();
            var size = PlayerSettings.CacheDistance + PlayerSettings.RenderDistance;
            size *= size;
            size -= PlayerSettings.RenderDistance * PlayerSettings.RenderDistance;
            if (_pool.Count < size)
                _pool.Enqueue(chunk);
            else
                chunk.Dispose();
        }

        /// <summary>
        /// Gets a chunk from the pool or creates a new one if the pool is empty.
        /// </summary>
        /// <returns>A chunk ready for reuse</returns>
        private Chunk GetPooledChunk()
        {
            if (_pool.Count == 0 && _chunksToClear.Count > 0)
            {
                ClearChunkAndEnqueue(_chunksToClear.Dequeue());
            }
            if (_pool.Count > 0)
            {
                return _pool.Dequeue();
            }
            else
            {
                return NewChunk;
            }
        }

        /// <summary>
        /// Attempts to find a chunk at the specified position in the given dictionary.
        /// </summary>
        /// <param name="pos">The position to search for</param>
        /// <param name="source">The dictionary to search in</param>
        /// <returns>The chunk if found, null otherwise</returns>
        private Chunk GetChunkFromSource(Vector3 pos, ref Dictionary<Vector3, Chunk> source)
        {
            if (source == null)
                return null;
            source.TryGetValue(pos, out Chunk chunk);
            return chunk;
        }

        /// <summary>
        /// Retrieves a chunk at the specified position from any of the chunk collections.
        /// </summary>
        /// <param name="pos">The position to get the chunk from</param>
        /// <returns>The chunk at the specified position, or null if none exists</returns>
        public Chunk GetChunk(Vector3 pos)
        {
            Chunk chunk = (GetChunkFromSource(pos, ref _active)
                ?? GetChunkFromSource(pos, ref _cached))
                ?? GetChunkFromSource(pos, ref _generating);

            return chunk;
        }

        /// <summary>
        /// Marks a chunk position as being generated and prepares a chunk for it.
        /// </summary>
        /// <param name="pos">The position of the chunk</param>
        public void SetChunkToGenerating(Vector3 pos)
        {
            Chunk chunk = GetChunk(pos);
            if (chunk == null)
            {
                chunk = GetPooledChunk();
                chunk.Reuse(pos);
                chunk.Active = true;
                chunk.Render = true;
            }
            else
            {
                if (_active.ContainsKey(pos))
                {
                    _active.Remove(pos);
                }
            }
            _generating.Add(pos, chunk);
        }

        /// <summary>
        /// Marks a generating chunk as complete and moves it to the active or cached collection.
        /// </summary>
        /// <param name="pos">The position of the chunk</param>
        public void CompleteGeneratingChunk(Vector3 pos)
        {
            if (_generating.TryGetValue(pos, out Chunk chunk))
            {
                if (WorldSettings.ChunksInRange(Center, pos, PlayerSettings.RenderDistance))
                {
                    chunk.Active = true;
                    chunk.Render = true;
                    _active.Add(pos, chunk);
                }
                else
                {
                    chunk.Active = false;
                    chunk.Render = false;
                    _cached.Add(pos, chunk);
                }

                _generating.Remove(pos);
            }
        }

        /// <summary>
        /// Modifies a single voxel at the specified world position.
        /// Uses WorldSettings.ChunkPositionFromPosition to find the chunk key.
        /// </summary>
        public bool ModifyVoxel(Vector3 worldPos, Voxel voxel)
        {
            bool isRemoving = voxel.IsEmpty; // Use IsEmpty property
            if (!isRemoving && !IsValidVoxelType(voxel)) { return false; }

            // --- Calculate Chunk Key using WorldSettings method ---
            // WorldSettings.ChunkPositionFromPosition returns chunk coords (e.g., 0,0,0 or 1,0,0)
            Vector3 chunkCoords = WorldSettings.ChunkPositionFromPosition(worldPos);
            Vector3 chunkKey = chunkCoords;

            // --- Calculate Local Coordinates ---
            Vector3Int localPos = WorldToLocalCoords(worldPos);

            // --- Get Chunk and Validate ---
            Chunk targetChunk = GetChunk(chunkCoords);
            if (targetChunk == null)
            {
                Debug.LogWarning($"ModifyVoxel failed: Chunk at key {chunkKey} (from world pos {worldPos}) not found.");
                return false;
            }
            if (!targetChunk.DataGenerated)
            {
                Debug.LogWarning($"ModifyVoxel failed: Chunk {chunkKey} exists but data not generated.");
                return false;
            }

            // --- Modification ---
            bool success = targetChunk.SetVoxel(voxel, localPos.x, localPos.y, localPos.z);

            // --- Regeneration Request ---
            if (success)
            {
                ChunkFactory.Instance.RequestChunkRegeneration(chunkKey, ChunkGenerationFlags.Mesh | ChunkGenerationFlags.Collider);
                CheckAndRequestNeighborUpdates(chunkKey, localPos, isRemoving);
                return true;
            }
            return false; // SetVoxel failed
        }

        /// <summary>
        /// Overload for ModifyVoxel using integer coordinates.
        /// </summary>
        public bool ModifyVoxel(int x, int y, int z, Voxel voxel)
        {
            return ModifyVoxel(new Vector3(x, y, z), voxel);
        }

        /// <summary>
        /// Checks borders using ChunkWidth and requests neighbor updates.
        /// </summary>
        private void CheckAndRequestNeighborUpdates(Vector3 modifiedChunkKey, Vector3Int localPos, bool isRemoving = false)
        {
            int M = WorldSettings.ChunkWidth - 1;
            int N = WorldSettings.ChunkWidth + 1;

            if (localPos.x > 0 && localPos.x < M && localPos.z > 0 && localPos.z < M)
                return;

            HashSet<(Vector3, Vector2)> neighborsToUpdate = new HashSet<(Vector3, Vector2)>();
            if (localPos.x == 0)
            {
                neighborsToUpdate.Add((modifiedChunkKey + Vector3.left, new Vector2(N, localPos.z + 1)));
            }

            if (localPos.x == M)
            {
                neighborsToUpdate.Add((modifiedChunkKey + Vector3.right, new Vector2(0, localPos.z + 1)));
            }

            if (localPos.z == 0)
            {
                neighborsToUpdate.Add((modifiedChunkKey + Vector3.back, new Vector2(localPos.x + 1, N)));
            }

            if (localPos.z == M)
            {
                neighborsToUpdate.Add((modifiedChunkKey + Vector3.forward, new Vector2(localPos.x + 1, 0)));
            }

            foreach ((Vector3, Vector2) neighborKey in neighborsToUpdate)
            {
                Chunk neighborChunk = GetChunk(neighborKey.Item1);
                //Debug.LogWarning($"Neighbour key: {neighborKey} is {(neighborChunk == null ? "" : "not")} null.");
                if (neighborChunk != null && neighborChunk.DataGenerated)
                {
                    neighborChunk.UpdateHeightMapBorder(neighborKey.Item2, isRemoving);
                    ChunkFactory.Instance.RequestChunkRegeneration(neighborKey.Item1, ChunkGenerationFlags.Mesh | ChunkGenerationFlags.Collider);
                }
            }
        }

        /// <summary>
        /// Converts world coordinates to local voxel coordinates (0 to Width/Height-1).
        /// </summary>
        private Vector3Int WorldToLocalCoords(Vector3 worldPos)
        {
            // Calculate local position relative to the chunk's origin (key)
            Vector3 chunkOrigin = WorldSettings.ChunkPositionFromPosition(worldPos) * WorldSettings.ChunkWidth;
            Vector3 relativePos = worldPos - chunkOrigin;

            int localX = Mathf.FloorToInt(relativePos.x);
            int localY = Mathf.FloorToInt(worldPos.y); // Y is absolute world height
            int localZ = Mathf.FloorToInt(relativePos.z);

            return new Vector3Int(localX, localY, localZ);
        }

        /// <summary>
        /// Placeholder for validating a voxel type.
        /// Implement this based on your voxel registry or data.
        /// </summary>
        /// <param name="voxel">The voxel type to check.</param>
        /// <returns>True if the voxel type is valid, false otherwise.</returns>
        private bool IsValidVoxelType(Voxel voxel)
        {
            // Example: Check against a known range of IDs or a registry
            // return voxel.ID > 0 && voxel.ID < MaxVoxelTypes;
            return true; // Replace with actual validation logic
        }

        private int meshVertices = 0;
        private int meshIndices = 0;
        private int colliderVertices = 0;
        private int colliderIndices = 0;
        public void UpdateChunkMeshSize(int mV, int mI)
        {
            meshVertices += mV;
            meshIndices += mI;
        }

        public void UpdateChunkColliderSize(int cV, int cI)
        {
            colliderVertices += cV;
            colliderIndices += cI;
        }

        public (int, int, int, int, int) ChunksMeshAndColliderSize()
        {
            return (_active.Count, meshVertices, meshIndices, colliderVertices, colliderIndices);
        }

        /// <summary>
        /// Clears chunks asynchronously based on distance from the center.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to stop the operation</param>
        private async void ClearChunksAsync(CancellationToken cancellationToken)
        {
            List<Vector3> removals = (from key in _cached.Keys
                                      where !WorldSettings.ChunksInRange(Center, key, PlayerSettings.RenderDistance + PlayerSettings.CacheDistance)
                                      select key).ToList();

            foreach (var key in removals)
            {
                _chunksToClear.Enqueue(_cached[key]);
                _cached.Remove(key);
            }

            foreach (var key in _cached.Keys)
            {
                _cached[key].Active = false;
            }

            while (_chunksToClear.Count != 0)
            {
                if (cancellationToken.IsCancellationRequested) return;

                ClearChunkAndEnqueue(_chunksToClear.Dequeue());
                await UniTask.Yield();
            }
        }

        /// <summary>
        /// Disposes all resources used by the chunks manager.
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            while (_pool.Count > 0)
            {
                _pool.Dequeue().Dispose();
            }
            _pool.Clear();

            foreach (var key in _active.Keys)
            {
                _active[key].Dispose();
            }
            _active.Clear();

            while (_chunksToClear.Count > 0)
            {
                _chunksToClear.Dequeue().Dispose();
            }
            _chunksToClear.Clear();

            foreach (var key in _cached.Keys)
            {
                _cached[key].Dispose();
            }
            _cached.Clear();

            foreach (var key in _generating.Keys)
            {
                _generating[key].Dispose();
            }
            _generating.Clear();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
            GC.Collect();
#endif
        }

        /// <inheritdoc/>
        public Voxel GetVoxel(int x, int y, int z)
        {
            return GetVoxel(new Vector3(x, y, z));
        }

        /// <inheritdoc/>
        public Voxel GetVoxel(Vector3 worldPos)
        {
            // --- Calculate Chunk Key using WorldSettings method ---
            // WorldSettings.ChunkPositionFromPosition returns chunk coords (e.g., 0,0,0 or 1,0,0)
            Vector3 chunkCoords = WorldSettings.ChunkPositionFromPosition(worldPos);
            Vector3 chunkKey = chunkCoords;

            // --- Calculate Local Coordinates ---
            Vector3Int localPos = WorldToLocalCoords(worldPos);

            // --- Get Chunk and Validate ---
            Chunk targetChunk = GetChunk(chunkCoords);
            if (targetChunk == null)
            {
                Debug.LogWarning($"GetVoxel failed: Chunk at key {chunkKey} (from world pos {worldPos}) not found.");
                return Voxel.Empty;
            }
            if (!targetChunk.DataGenerated)
            {
                Debug.LogWarning($"GetVoxel failed: Chunk {chunkKey} exists but data not generated.");
                return Voxel.Empty;
            }

            return targetChunk[worldPos];
        }

        ~ChunksManager()
        {
            WorldSettings.OnWorldSettingsChanged -= GenerateChunksPositionsCheck;
            WorldSettings.OnWorldChanged -= Dispose;
            WorldSettings.OnWorldChanged -= GenerateChunksPositionsCheck;
            Dispose();
        }
    }
}
