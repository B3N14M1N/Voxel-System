using System;
using System.Linq;
using UnityEngine;

public class VoxelSystemHandler : MonoBehaviour
{
    [SerializeField] private IChunksManager _chunksManager;
    [SerializeField] private IChunksFactory _chunkFactory;


    private event EventHandler<UpdateChunkListArgs> UpdateChunkList;

    public void OnUpdateChunkList(object sender, UpdateChunkListArgs e)
    {
        UpdateChunkList?.Invoke(sender, e);
    }

    private void EventActionUpdateChunkList(object sender, UpdateChunkListArgs e)
    {
        _chunksManager.UpdateChunks(e.Center);
    }

    private void Awake()
    {
        UpdateChunkList += EventActionUpdateChunkList;
    }

}

public class UpdateChunkListArgs : EventArgs
{
    public Vector3 Center;
}