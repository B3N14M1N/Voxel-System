using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using VoxelSystem.Data;
using VoxelSystem.Data.GenerationFlags;
using VoxelSystem.Factory;

namespace VoxelSystem.Managers
{
    /// <summary>
    /// 
    /// </summary>
    public class ChunksManager : IChunksManager
    {
        public const string ChunksManagerLoopString = "ChunksManagerLoop";
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        public Vector3 Center { get; private set; }

        private readonly Queue<Chunk> _pool = new();
        private readonly Queue<Chunk> _chunksToClear = new();
        private readonly Transform _parent;

        [SerializeField] private LayerMask _chunkLayer;
        private Dictionary<Vector3, Chunk> _active = new();
        private Dictionary<Vector3, Chunk> _cached = new();
        private Dictionary<Vector3, Chunk> _generating = new();
        private Vector3[] _chunksPositionCheck;
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
        /// 
        /// </summary>
        /// <param name="transform"></param>
        public ChunksManager(LayerMask layerMask = default, Transform transform = null)
        {
            _parent = transform;
            _chunkLayer = layerMask;
            for (int i = 0; i < (PlayerSettings.RenderDistance + PlayerSettings.CacheDistance) * (PlayerSettings.RenderDistance + PlayerSettings.CacheDistance); i++)
            {
                _pool.Enqueue(NewChunk);
            }

            GenerateChunksPositionsCheck();
        }

        /// <summary>
        /// 
        /// </summary>
        public void GenerateChunksPositionsCheck()
        {
            var job = new ChunksRenderView(Vector3.zero, PlayerSettings.RenderDistance);
            _chunksPositionCheck = job.Complete();
            job.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="center"></param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void UpdateChunks(Vector3 center)
        {
            var UpdateTime = Time.realtimeSinceStartup;
            Center = center;
            _cached.AddRange(_active);
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

                //bool simulate = _chunksPositionCheck[i].y <= PlayerSettings.SimulateDistance * PlayerSettings.SimulateDistance;

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
        /// 
        /// </summary>
        /// <param name="chunk"></param>
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
        /// 
        /// </summary>
        /// <returns></returns>
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
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        private Chunk GetChunkFromSource(Vector3 pos, ref Dictionary<Vector3, Chunk> source)
        {
            if (source == null)
                return null;
            source.TryGetValue(pos, out Chunk chunk);
            return chunk;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public Chunk GetChunk(Vector3 pos)
        {
            Chunk chunk = (GetChunkFromSource(pos, ref _active)
                ?? GetChunkFromSource(pos, ref _cached))
                ?? GetChunkFromSource(pos, ref _generating);

            return chunk;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
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
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void CompleteGeneratingChunk(Vector3 pos)
        {
            if (_generating.TryGetValue(pos, out Chunk chunk))
            {
                _active.Add(pos, chunk);
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
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        private async void ClearChunksAsync(CancellationToken cancellationToken)
        {
            List<Vector3> removals = (from key in _cached.Keys
                                      where !WorldSettings.ChunksInRange(Center, key, PlayerSettings.RenderDistance + PlayerSettings.CacheDistance)
                                      select key).ToList();

            foreach (var key in removals)
            {
                _chunksToClear.Enqueue(_cached[key]);
                _cached.Remove(key);
                //ClearChunkAndEnqueue(key, ref _cached);
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
        /// 
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
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
            EditorUtility.UnloadUnusedAssetsImmediate();
            GC.Collect();
#endif
        }

        ~ChunksManager()
        {
            Dispose();
        }
    }
}
