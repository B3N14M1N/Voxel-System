using System;
using UnityEditor;
using UnityEngine;
using Zenject;

namespace VoxelSystem.Managers
{
    /// <summary>
    /// 
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        [Inject] private readonly IChunksManager _chunksManager;
        [SerializeField] private const string WorldManagerLoopString = "WorldManagerLoop";

        [Header("Player")]
        [SerializeField] private Transform player;
        [SerializeField] private Vector3 playerChunkPosition;


        public void Start()
        {
            playerChunkPosition = WorldSettings.ChunkPositionFromPosition(player.transform.position);
            _chunksManager.UpdateChunks(WorldSettings.ChunkPositionFromPosition(player.transform.position));
        }

        // Update is called once per frame
        void Update()
        {
            var UpdateTime = Time.realtimeSinceStartup;
            playerChunkPosition = WorldSettings.ChunkPositionFromPosition(player.transform.position);

            if (_chunksManager.Center != playerChunkPosition)
                _chunksManager.UpdateChunks(playerChunkPosition);

            AvgCounter.UpdateCounter(WorldManagerLoopString, (Time.realtimeSinceStartup - UpdateTime) * 1000f);
        }

        private void OnApplicationQuit()
        {
#if UNITY_EDITOR
            EditorUtility.UnloadUnusedAssetsImmediate();
            GC.Collect();
#endif
        }
    }
}