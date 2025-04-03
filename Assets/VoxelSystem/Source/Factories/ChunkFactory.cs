using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VoxelSystem.Data.GenerationFlags;
using VoxelSystem.Generators;
using VoxelSystem.Managers;
using VoxelSystem.Settings.Generation;
using Zenject;

namespace VoxelSystem.Factory
{
    /// <summary>
    /// 
    /// </summary>
    public class ChunkFactory : MonoBehaviour
    {
        [Inject] private readonly IChunksManager _chunksManager;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private float _time = 0f;
        private List<GenerationData> _chunksToProccess = new();

        #region Fields
        private int _materialIndex = 0;
        private Vector2Int[] _octaveOffsets;

        [Header("Noise Parameters")]
        [field: SerializeField] public GenerationParameters NoiseParameters { get; private set; }

        [Header("Materials")]
        [field: SerializeField] private List<Material> _materials { get; set; }

        [field: SerializeField] private KeyCode MaterialChangeKey { get; set; } = KeyCode.M;
        public Material Material { get { return _materials[_materialIndex]; } }
        [HideInInspector] public Vector2Int[] NoiseOctaves { get { return _octaveOffsets; } }
        [HideInInspector] public bool CanChangeMaterial { get; set; }


        private static ChunkFactory instance;
        public static ChunkFactory Instance
        {
            get
            {
                if (instance == null)
                    instance = FindAnyObjectByType<ChunkFactory>();
                if (instance == null)
                    instance = new ChunkFactory();
                return instance;
            }
            private set
            {
                instance = value;
            }
        }
        #endregion

        #region Initializations

        public void Awake()
        {
            InitSeed();
            CanChangeMaterial = true;
        }

        public void InitSeed()
        {
            NoiseParameters.OnValidate();
        }

        #endregion


        private void Update()
        {
            if (Input.GetKeyUp(MaterialChangeKey) && CanChangeMaterial)
            {
                _materialIndex = _materialIndex == _materials.Count - 1 ? 0 : ++_materialIndex;
            }

            _time += Time.deltaTime;

            if (_time < PlayerSettings.TimeToLoadNextChunks && ChunkJob.Processed != 0) return;

            for (int i = 0; i < _chunksToProccess.Count && ChunkJob.Processed < PlayerSettings.ChunksProcessed; i++)
            {
                new ChunkJob(_chunksToProccess[i], _chunksManager, _cancellationTokenSource.Token);
                _chunksToProccess.RemoveAt(i);
                i--;
            }
            _time = 0f;
        }

        public void GenerateChunksData(List<GenerationData> toGenerate)
        {
            _chunksToProccess = toGenerate;
        }

        public void Dispose()
        {
            _chunksToProccess.Clear();
        }

        public void OnApplicationQuit()
        {
            Dispose();
            ChunkParallelMeshGenerator.DisposeAll();
            _cancellationTokenSource.Cancel();
        }

        public void OnDestroy()
        {
            Dispose();
            _cancellationTokenSource.Cancel();
        }
    }
}
