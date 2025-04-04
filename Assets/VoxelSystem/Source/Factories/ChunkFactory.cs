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
        /// Queue holding regeneration requests for chunks that have been modified
        /// and need their mesh and/or collider updated. Processed after initial generation requests.
        /// Uses GenerationData to store position and flags (Mesh/Collider).
        /// </summary>
        private readonly Queue<GenerationData> _chunksToRegenerate = new Queue<GenerationData>();

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
            // Check throttling condition (Allow processing if EITHER timer is up OR no jobs are running)
            bool canProcess = _timeSinceLastDispatch >= PlayerSettings.TimeToLoadNextChunks || ChunkJob.Processed == 0;

            if (!canProcess)
            {
                return; // Wait if throttled and jobs are running
            }

            // --- Dispatch Chunk Jobs ---
            _timeSinceLastDispatch = 0f; // Reset timer as we are processing now

            int jobsDispatchedThisFrame = 0; // Track jobs dispatched in this frame across both queues
            int maxJobsPerFrame = PlayerSettings.ChunksProcessed - ChunkJob.Processed; // Max new jobs we can start

            // 1. Process Regeneration Queue (_chunksToRegenerate - FIFO)
            // Process this queue only if we still have capacity within the frame's job limit.
            while (_chunksToRegenerate.Count > 0)
            {
                GenerationData regenData = _chunksToRegenerate.Dequeue();

                // Ensure the chunk still exists before regenerating (it might have been unloaded)
                Chunk chunk = _chunksManager?.GetChunk(regenData.position);
                if (chunk != null && chunk.DataGenerated) // Only regenerate if chunk exists and has data
                {
                    // Create a job specifically for regeneration (Mesh/Collider flags only).
                    // ChunkJob constructor should handle this case (skipping data generation).
                    new ChunkJob(regenData, _chunksManager, _cancellationTokenSource.Token);
                    jobsDispatchedThisFrame++; // Increment count for this frame
                }
                else
                {
                    Debug.LogWarning($"Skipping regeneration for chunk {regenData.position} as it no longer exists or has no data.");
                }
            }
            // 2. Process Initial Generation Queue (_chunksToProccess - closest first)
            // NOTE: Uses the inefficient forward iteration with RemoveAt to preserve order.
            for (int i = 0; i < _chunksToProccess.Count && jobsDispatchedThisFrame < maxJobsPerFrame; /* No increment */)
            {
                // Create and start a new generation job for the chunk data at the current index.
                // Flags typically include Data, Mesh, Collider for initial generation.
                new ChunkJob(_chunksToProccess[i], _chunksManager, _cancellationTokenSource.Token);
                jobsDispatchedThisFrame++; // Increment count for this frame

                // Remove the processed chunk data from the list.
                _chunksToProccess.RemoveAt(i);
                // Do not increment 'i' because removing shifts subsequent elements down.
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
        /// Adds a request to regenerate the mesh and/or collider for a specific chunk.
        /// Typically called after a voxel modification.
        /// </summary>
        /// <param name="chunkPos">The position (key) of the chunk to regenerate.</param>
        /// <param name="flags">The components to regenerate (typically Mesh | Collider).</param>
        public void RequestChunkRegeneration(Vector3 chunkPos, ChunkGenerationFlags flags)
        {
            // Create GenerationData containing only the position and the required regen flags.
            var regenData = new GenerationData
            {
                position = chunkPos,
                // Ensure only Mesh and Collider flags are considered for regeneration requests.
                flags = flags & (ChunkGenerationFlags.Mesh | ChunkGenerationFlags.Collider)
            };

            // Add to the regeneration queue if valid flags are provided.
            if (regenData.flags != ChunkGenerationFlags.None)
            {
                _chunksToRegenerate.Enqueue(regenData);
            }
            else
            {
                Debug.LogWarning($"RequestChunkRegeneration called for {chunkPos} with no valid flags (Mesh/Collider).");
            }
        }


        // --- Private Helper Methods ---

        // Modify CleanupResources to clear the new queue:
        private void CleanupResources()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                Debug.Log("Cleaning up ChunkFactory resources...");
                Dispose(); // Calls _chunksToProccess.Clear()
                _chunksToRegenerate.Clear(); // Clear the regeneration queue too
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                Debug.Log("ChunkFactory resources cleaned up.");
            }
        }

        // Modify Dispose to clear the new queue:
        public void Dispose()
        {
            _chunksToProccess.Clear();
            _chunksToRegenerate.Clear(); // Clear the regeneration queue too
        }
    }
}
