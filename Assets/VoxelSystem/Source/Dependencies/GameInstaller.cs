using UnityEngine;
using VoxelSystem.Data.Blocks;
using VoxelSystem.Managers;
using Zenject;

public class GameInstaller : MonoInstaller
{
    [SerializeField] private Transform _parent;
    [SerializeField] private BlocksCatalogue _blocks;
    [SerializeField] private VoxelBlockManager _blockManager;
    public override void InstallBindings()
    {
        var mapping = _blocks.GenerateAtlas();
        var chunksManager = new ChunksManager(_parent);
        Container.Bind<IChunksManager>().FromInstance(chunksManager).AsSingle();
        Container.Bind<VoxelBlockManager>().FromInstance(_blockManager).AsSingle();
    }
}