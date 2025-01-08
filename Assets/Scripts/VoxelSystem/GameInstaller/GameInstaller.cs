using UnityEngine;
using VoxelSystem.Managers;
using Zenject;

public class GameInstaller : MonoInstaller
{
    [SerializeField] private Transform _parent;
    public override void InstallBindings()
    {
        var chunksManager = new ChunksManager(_parent);
        Container.Bind<IChunksManager>().FromInstance(chunksManager).AsSingle();
    }
}