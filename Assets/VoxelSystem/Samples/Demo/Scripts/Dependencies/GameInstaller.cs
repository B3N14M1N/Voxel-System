using UnityEngine;
using VoxelSystem.Data.Blocks;
using VoxelSystem.Factory;
using VoxelSystem.Generators;
using VoxelSystem.Managers;
using VoxelSystem.SaveSystem;
using VoxelSystem.Settings;
using Zenject;

public class GameInstaller : MonoInstaller
{
    [SerializeField] private Transform _chunksParent;
    [SerializeField] private LayerMask _chunkLayerMask;
    [SerializeField] private BlocksCatalogue _blocks;
    [SerializeField] private VoxelBlockManager _blockManager;
    [SerializeField] private string _defaultWorldPath = "DefaultWorld";

    public override void InstallBindings()
    {
        // Initialize World Settings
        var worldSettings = new WorldSettings();
        worldSettings.CreateNewWorld(_defaultWorldPath);

        var map = _blocks.GenerateAtlas();
        ChunkMeshGenerator.SetAtlasIndexMap(map);
        var chunksManager = new ChunksManager(_chunkLayerMask, _chunksParent);

        Container.BindInterfacesAndSelfTo<PlayerStateManager>().AsSingle();
        Container.Bind<BlocksCatalogue>().FromInstance(_blocks).AsSingle();
        Container.Bind<IChunksManager>().FromInstance(chunksManager).AsSingle();
        Container.Bind<VoxelBlockManager>().FromInstance(_blockManager).AsSingle();
        Container.Bind<WorldSettings>().FromInstance(worldSettings).AsSingle();
    }

    public void OnApplicationQuit()
    {
        // Clean up the chunk save system to prevent semaphore issues on restart
        ChunkSaveSystem.Cleanup();
        _blocks.Dispose();
    }
}