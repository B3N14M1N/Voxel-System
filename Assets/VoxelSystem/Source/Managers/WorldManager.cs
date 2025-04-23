using System;
using UnityEditor;
using UnityEngine;
using Zenject;

namespace VoxelSystem.Managers
{
    /// <summary>
    /// Manages the voxel world by tracking player movement and coordinating chunk updates.
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        [Inject] private readonly IChunksManager _chunksManager;
        private const string WorldManagerLoopString = "WorldManagerLoop";

        /// <summary>
        /// The player's transform that the world follows.
        /// </summary>
        [Header("Player")]
        [SerializeField] private Transform player;
        
        /// <summary>
        /// The current chunk position of the player.
        /// </summary>
        [SerializeField] private Vector3 playerChunkPosition;

        /// <summary>
        /// Initializes the world manager when the component starts.
        /// </summary>
        public void Start()
        {
            playerChunkPosition = WorldSettings.ChunkPositionFromPosition(player.transform.position);
            _chunksManager.UpdateChunks(WorldSettings.ChunkPositionFromPosition(player.transform.position));
        }

        /// <summary>
        /// Updates the world based on player movement each frame.
        /// </summary>
        private void Update()
        {
            var UpdateTime = Time.realtimeSinceStartup;
            playerChunkPosition = WorldSettings.ChunkPositionFromPosition(player.transform.position);

            if (_chunksManager.Center != playerChunkPosition)
                _chunksManager.UpdateChunks(playerChunkPosition);

            AvgCounter.UpdateCounter(WorldManagerLoopString, (Time.realtimeSinceStartup - UpdateTime) * 1000f);
        }

        /// <summary>
        /// Performs cleanup when the application is quitting.
        /// </summary>
        private void OnApplicationQuit()
        {
#if UNITY_EDITOR
            EditorUtility.UnloadUnusedAssetsImmediate();
            GC.Collect();
#endif
        }
    }
}