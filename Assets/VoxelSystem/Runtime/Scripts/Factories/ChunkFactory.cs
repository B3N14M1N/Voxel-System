using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VoxelSystem.Data.GenerationFlags;
using VoxelSystem.Generators;
using VoxelSystem.Managers;
using VoxelSystem.Settings;
using VoxelSystem.Settings.Generation;
using Zenject;

namespace VoxelSystem.Factory
{
    /// <summary>
    /// Factory responsible for processing and generating chunks in the voxel system.
    /// </summary>
    public class ChunkFactory : MonoBehaviour, IDisposable
    {
        [Inject] private readonly IChunksManager _chunksManager;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private float _time = 0f;
        private List<GenerationData> _chunksToProccess = new();
        private Queue<GenerationData> _priorityChunksToProccess = new();

        private int _materialIndex = 0;
        private Vector2Int[] _octaveOffsets;

        /// <summary>
        /// Parameters controlling terrain noise generation.
        /// </summary>
        [Header("Noise Parameters")]
        [field: SerializeField] public GenerationParameters NoiseParameters { get; private set; }

        /// <summary>
        /// Available materials for rendering chunks.
        /// </summary>
        [Header("Materials")]
        [field: SerializeField] private List<Material> _materials { get; set; }

        /// <summary>
        /// Key used to cycle through materials.
        /// </summary>
        [field: SerializeField] private KeyCode MaterialChangeKey { get; set; } = KeyCode.M;

        /// <summary>
        /// Gets the currently selected material.
        /// </summary>
        public Material Material { get { return _materials[_materialIndex]; } }

        /// <summary>
        /// Gets the noise octave offsets.
        /// </summary>
        [HideInInspector] public Vector2Int[] NoiseOctaves { get { return _octaveOffsets; } }

        /// <summary>
        /// Controls whether materials can be changed at runtime.
        /// </summary>
        [HideInInspector] public bool CanChangeMaterial { get; set; }

        /// <summary>
        /// Singleton instance of the ChunkFactory.
        /// </summary>
        private static ChunkFactory instance;

        /// <summary>
        /// Gets the singleton instance of the ChunkFactory.
        /// </summary>
        public static ChunkFactory Instance
        {
            get
            {
                if (instance == null)
                    instance = FindAnyObjectByType<ChunkFactory>();

                return instance;
            }
            private set
            {
                instance = value;
            }
        }

        /// <summary>
        /// Initializes the chunk factory when the component awakens.
        /// </summary>
        public void Awake()
        {
            InitSeed();
            CanChangeMaterial = true;
        }

        /// <summary>
        /// Initializes the noise generation seed.
        /// </summary>
        public void InitSeed()
        {
            NoiseParameters.OnValidate();
        }

        /// <summary>
        /// Processes chunk generation queue each frame.
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyUp(MaterialChangeKey) && CanChangeMaterial)
            {
                _materialIndex = _materialIndex == _materials.Count - 1 ? 0 : ++_materialIndex;
            }

            _time += Time.deltaTime;

            if (_time < PlayerSettings.TimeToLoadNextChunks && ChunkJob.Processed != 0) return;

            while (_priorityChunksToProccess.Count != 0)
            {
                new ChunkJob(_priorityChunksToProccess.Dequeue(), _chunksManager, _cancellationTokenSource.Token);
            }

            for (int i = 0; i < _chunksToProccess.Count && ChunkJob.Processed < PlayerSettings.ChunksProcessed; i++)
            {
                new ChunkJob(_chunksToProccess[i], _chunksManager, _cancellationTokenSource.Token);
                _chunksToProccess.RemoveAt(i);
                i--;
            }
            
            _time = 0f;
        }

        /// <summary>
        /// Adds a list of chunks to be generated.
        /// </summary>
        /// <param name="toGenerate">The chunks to generate</param>
        public void GenerateChunksData(List<GenerationData> toGenerate)
        {
            _chunksToProccess = toGenerate;
        }

        /// <summary>
        /// Requests regeneration of a specific chunk with priority.
        /// </summary>
        /// <param name="chunkKey">The position of the chunk to regenerate</param>
        /// <param name="chunkGenerationFlags">Flags controlling what to regenerate</param>
        public void RequestChunkRegeneration(Vector3 chunkKey, ChunkGenerationFlags chunkGenerationFlags)
        {
            _priorityChunksToProccess.Enqueue(new GenerationData() { position = chunkKey, flags = chunkGenerationFlags });
        }

        /// <summary>
        /// Clears all pending chunk generation requests.
        /// </summary>
        public void Dispose()
        {
            _chunksToProccess.Clear();
        }

        /// <summary>
        /// Performs cleanup when the application is quitting.
        /// </summary>
        public void OnApplicationQuit()
        {
            Dispose();
            ChunkMeshGenerator.DisposeAll();
            _cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Performs cleanup when this component is destroyed.
        /// </summary>
        public void OnDestroy()
        {
            Dispose();
            _cancellationTokenSource.Cancel();
        }
    }
}
