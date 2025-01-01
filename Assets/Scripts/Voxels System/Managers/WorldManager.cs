using System;
using UnityEditor;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    public const string WorldManagerLoopString = "WorldManagerLoop";

    [Header("Player")]
    public Transform player;
    public Vector3 playerChunkPosition;


    public void Start()
    {
        playerChunkPosition = WorldSettings.ChunkPositionFromPosition(player.transform.position);
        ChunksManager.Instance.UpdateChunks(WorldSettings.ChunkPositionFromPosition(player.transform.position));
    }

    // Update is called once per frame
    private void Update()
    {
        var UpdateTime = Time.realtimeSinceStartup;
        playerChunkPosition = WorldSettings.ChunkPositionFromPosition(player.transform.position);
        if (ChunksManager.Instance.Center != playerChunkPosition)
        {
            ChunksManager.Instance.UpdateChunks(playerChunkPosition);
        }
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
