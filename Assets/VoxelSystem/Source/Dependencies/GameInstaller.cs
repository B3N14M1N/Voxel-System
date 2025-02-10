using UnityEngine;
using VoxelSystem.Data.Blocks;
using VoxelSystem.Managers;
using Zenject;

public class GameInstaller : MonoInstaller
{
    [SerializeField] private Transform _parent;
    [SerializeField] private BlocksCatalogue blocks;
    public override void InstallBindings()
    {
        var mapping = blocks.GenerateAtlas();
        var chunksManager = new ChunksManager(_parent);
        Container.Bind<IChunksManager>().FromInstance(chunksManager).AsSingle();
    }
}