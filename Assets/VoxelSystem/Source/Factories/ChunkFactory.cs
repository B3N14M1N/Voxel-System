using System.Collections.Generic; // Provides List<T>
using System.Threading; // Provides CancellationTokenSource
using UnityEngine; // Provides MonoBehaviour, Vector3, KeyCode, etc.
using VoxelSystem.Data.GenerationFlags; // Provides GenerationData (assumed)
using VoxelSystem.Generators; // Provides ChunkJob, ChunkParallelMeshGenerator (assumed)
using VoxelSystem.Managers; // Provides IChunksManager
using VoxelSystem.Settings.Generation; // Provides GenerationParameters
using Zenject; // Provides dependency injection ([Inject])

// Namespace organizing the voxel system code
namespace VoxelSystem.Factory
{
    /// <summary>
    /// Manages the creation and dispatching of chunk generation jobs.
    /// Holds noise generation parameters and handles material selection for chunks.
    /// Acts as a central point for initiating chunk processing based on requests from the ChunksManager.
    /// Implements a Singleton pattern using FindAnyObjectByType for convenient global access.
    /// </summary>
    public class ChunkFactory : MonoBehaviour, System.IDisposable // Implement IDisposable for cleanup
    {
        // --- Singleton Implementation ---

        /// <summary>
        /// The static singleton instance of the ChunkFactory.
        /// </summary>
        private static ChunkFactory _instance;

        /// <summary>
        /// Gets the singleton instance of the ChunkFactory using FindAnyObjectByType on first access if needed.
        /// Provides a convenient global access point.
        /// Note: FindAnyObjectByType can have performance implications if called frequently.
        /// It's generally recommended to have systems retrieve dependencies via Dependency Injection (like Zenject)
        /// or cache the result of FindAnyObjectByType instead of relying on this getter repeatedly.
        /// </summary>
        public static ChunkFactory Instance
        {
            get
            {
                // If the instance hasn't been set yet (e.g., by Awake), try to find it in the scene.
                if (_instance == null)
                {
                    // Search for an active instance of ChunkFactory in the current scene.
                    _instance = FindAnyObjectByType<ChunkFactory>();

                    // Log a warning if no active instance was found.
                    // This usually indicates the ChunkFactory component hasn't been added to a GameObject
                    // or the GameObject is inactive.
                    if (_instance == null)
                    {
                        Debug.LogWarning("ChunkFactory.Instance could not find an active ChunkFactory component in the scene.");
                    }
                }
                return _instance;
            }
            // Private set removed - instance is found or set internally if needed by getter logic,
            // or potentially set by an external system if a different Singleton approach is mixed.
        }


        // --- Dependencies ---

        /// <summary>
        /// Reference to the Chunks Manager, injected by Zenject.
        /// Used to interact with chunk instances (e.g., notify upon completion via ChunkJob).
        /// </summary>
        [Inject]
        private readonly IChunksManager _chunksManager;

        // --- Configuration ---

        [Header("Noise Parameters")]
        [Tooltip("ScriptableObject or class holding parameters for noise generation (seed, octaves, scale, etc.).")]
        [field: SerializeField]
        public GenerationParameters NoiseParameters { get; private set; }

        [Header("Materials")]
        [Tooltip("List of materials that can be applied to chunks.")]
        [field: SerializeField]
        private List<Material> _materials { get; set; } // Consider making private set if modification isn't needed externally

        [Tooltip("Key used to cycle through the available materials.")]
        [field: SerializeField]
        private KeyCode MaterialChangeKey { get; set; } = KeyCode.M;

        /// <summary>
        /// Gets the currently selected material based on _materialIndex.
        /// </summary>
        public Material Material => _materials != null && _materialIndex < _materials.Count ? _materials[_materialIndex] : null;

        /// <summary>
        /// Gets or sets whether the material can be changed via the MaterialChangeKey.
        /// </summary>
        public bool CanChangeMaterial { get; set; }

        // --- State Fields ---

        /// <summary>
        /// Source for creating cancellation tokens passed to chunk generation jobs,
        /// allowing them to be cancelled gracefully (e.g., on application quit).
        /// </summary>
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// Timer used to throttle the dispatching of new chunk generation jobs.
        /// </summary>
        private float _timeSinceLastDispatch = 0f;

        /// <summary>
        /// List holding data for chunks that are waiting to be processed (generation jobs to be created).
        /// Populated by GenerateChunksData and consumed in Update. **Order is important (closest first).**
        /// </summary>
        private List<GenerationData> _chunksToProccess = new();

        /// <summary>
        /// Index of the currently selected material in the _materials list.
        /// </summary>
        private int _materialIndex = 0;

        // --- Unity Lifecycle Methods ---

        /// <summary>
        /// Called when the script instance is being loaded.
        /// Initializes seed-dependent parameters and sets initial state.
        /// Also ensures the static instance is set if this is the first one found.
        /// </summary>
        private void Awake()
        {
            // --- Singleton Check & Assignment ---
            // Check if an instance already exists and if it's different from this one.
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"Duplicate ChunkFactory detected on {gameObject.name}. Destroying this duplicate instance.");
                Destroy(gameObject);
                return; // Stop execution for this duplicate instance
            }
            // If no instance exists yet, or if this is the existing instance, ensure _instance points to this.
            // This handles cases where Awake runs before the Instance getter is first called.
            if (_instance == null)
            {
                _instance = this;
            }
            // Optional: Uncomment below if the Factory should persist across scene loads.
            // DontDestroyOnLoad(gameObject);


            // --- Initialization ---
            // Initialize noise parameters (e.g., generate octave offsets based on seed).
            InitSeed();
            // Allow material changing by default.
            CanChangeMaterial = true;
        }

        /// <summary>
        /// Called every frame.
        /// Handles user input for material changes and dispatches chunk generation jobs based on throttling limits,
        /// processing chunks in the order they appear in the _chunksToProccess list (closest first).
        /// </summary>
        private void Update()
        {
            // --- Material Switching ---
            if (Input.GetKeyUp(MaterialChangeKey) && CanChangeMaterial && _materials != null && _materials.Count > 0)
            {
                _materialIndex = (_materialIndex + 1) % _materials.Count;
            }

            // --- Job Dispatching Throttling ---
            _timeSinceLastDispatch += Time.deltaTime;
            if (_timeSinceLastDispatch < PlayerSettings.TimeToLoadNextChunks && ChunkJob.Processed > 0)
            {
                return; // Wait if throttled and jobs are running
            }

            // --- Dispatch Chunk Jobs (Closest First) ---
            _timeSinceLastDispatch = 0f; // Reset timer

            // Process the queue of chunks waiting for generation, iterating forwards.
            // This ensures chunks are processed in the order received (intended: closest first).
            // Limit the number of jobs dispatched per frame based on PlayerSettings.ChunksProcessed.
            // NOTE: Removing items from a List<> while iterating forward using RemoveAt(i)
            // is less efficient than iterating backwards, but necessary here to preserve processing order.
            for (int i = 0; i < _chunksToProccess.Count && ChunkJob.Processed < PlayerSettings.ChunksProcessed; /* No increment here */)
            {
                // Create and start a new generation job for the chunk data at the current index.
                new ChunkJob(_chunksToProccess[i], _chunksManager, _cancellationTokenSource.Token);

                // Remove the processed chunk data from the list.
                _chunksToProccess.RemoveAt(i);
                // Do not increment 'i' because removing shifts subsequent elements down.
                // The next element to process is now at the current index 'i'.
            }
        }

        /// <summary>
        /// Called when the application is quitting. Ensures resources are cleaned up.
        /// </summary>
        private void OnApplicationQuit()
        {
            CleanupResources();
            ChunkParallelMeshGenerator.DisposeAll(); // Assumed static cleanup
        }

        /// <summary>
        /// Called when the GameObject this script is attached to is destroyed.
        /// Ensures resources are cleaned up and clears the static instance if this was it.
        /// </summary>
        private void OnDestroy()
        {
            // If this instance is the current singleton instance, clear the static reference
            // so the Instance getter knows to potentially find a new one if the scene reloads
            // or another instance becomes active.
            if (_instance == this)
            {
                _instance = null;
            }
            CleanupResources();
        }

        // --- Public API ---

        /// <summary>
        /// Initializes seed-dependent noise parameters.
        /// </summary>
        public void InitSeed()
        {
            NoiseParameters?.OnValidate();
        }

        /// <summary>
        /// Receives a list of chunk data objects that need to be processed (generated).
        /// Replaces the current processing queue with the new list. Order matters.
        /// </summary>
        /// <param name="toGenerate">A list of GenerationData objects representing chunks to be generated (ordered closest to furthest).</param>
        public void GenerateChunksData(List<GenerationData> toGenerate)
        {
            _chunksToProccess = toGenerate ?? new List<GenerationData>();
        }

        /// <summary>
        /// Clears the list of chunks waiting to be processed.
        /// </summary>
        public void Dispose()
        {
            _chunksToProccess.Clear();
        }

        // --- Private Helper Methods ---

        /// <summary>
        /// Centralized method for cleaning up resources and cancelling tasks.
        /// </summary>
        private void CleanupResources()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                Debug.Log("Cleaning up ChunkFactory resources...");
                Dispose(); // Calls _chunksToProccess.Clear()
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                Debug.Log("ChunkFactory resources cleaned up.");
            }
        }
    }
}
