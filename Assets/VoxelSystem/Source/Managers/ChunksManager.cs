using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
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

        private Dictionary<Vector3, Chunk> _active = new();
        private Dictionary<Vector3, Chunk> _cached = new();
        private Dictionary<Vector3, Chunk> _generating = new();
        private Vector3[] _chunksPositionCheck;
        private Chunk NewChunk
        {
            get
            {
                Chunk chunk = new(Vector3.zero, this)
                {
                    Parent = _parent,
                    Active = false
                };
                return chunk;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="transform"></param>
        public ChunksManager(Transform transform = null)
        {
            _parent = transform;

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
                chunk.UpdateChunk(pos);
                chunk.Active = true;
                chunk.Render = true;
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
