using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VoxelSystem.Managers;
using Zenject;

namespace VoxelSystem.Factory
{
    /// <summary>
    /// 
    /// </summary>
    public class ChunkFactory : MonoBehaviour
    {
        private CancellationTokenSource _cancellationTokenSource = new();
        [Inject] private readonly IChunksManager _chunksManager;
        private float _time = 0f;
        private List<GenerationData> _chunksToProccess = new();
        #region Fields

        [Header("Noise Parameters")]
        [SerializeField] private NoiseParametersScriptableObject _noiseParameters;
        private Vector2Int[] _octaveOffsets;
        public NoiseParametersScriptableObject NoiseParameters { get { return _noiseParameters; } }
        public Vector2Int[] NoiseOctaves { get { return _octaveOffsets; } }

        [Header("Materials")]
        [SerializeField] private List<Material> _materials = new List<Material>();
        private int _materialIndex = 0;
        public Material Material { get { return _materials[_materialIndex]; } }
        public bool CanChangeMaterial { get; set; }

        [SerializeField] private KeyCode MaterialChangeKey = KeyCode.M;

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
            uint octavesMax = 0;
            for (int i = 0; i < _noiseParameters.noise.Count; i++)
            {
                if (_noiseParameters.noise[i].octaves > octavesMax)
                    octavesMax = _noiseParameters.noise[i].octaves;
            }
            _octaveOffsets = new Vector2Int[octavesMax];
            System.Random rnd = new((int)WorldSettings.Seed);
            for (int i = 0; i < octavesMax; i++)
            {
                _octaveOffsets[i] = new Vector2Int(rnd.Next(-10000, 10000), rnd.Next(-10000, 10000));
            }
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
