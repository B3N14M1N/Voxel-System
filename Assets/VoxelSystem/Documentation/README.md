# Voxel System Documentation

## Overview

The Voxel System is a height-based voxel terrain engine for Unity 6, created by Beniamin Cioban. It provides efficient chunk-based terrain management with multithreaded mesh generation using Unity's Job System and Burst compiler.

## Installation

### Using Package Manager

1. Open the Package Manager window in Unity (Window > Package Manager)
2. Click the "+" button in the upper-left corner
3. Select "Add package from git URL..."
4. Enter the repository URL
5. Click "Add"

### Manual Installation

1. Download the package
2. Extract it to your project's Assets folder
3. Open your project in Unity

## Core Components

### WorldManager

The central component that manages the voxel world, including chunk loading, unloading, and player tracking.

```csharp
public class WorldManager : MonoBehaviour
{
    // Configuration properties
    [SerializeField] private Transform _player;
    [SerializeField] private BlocksCatalogue _blocksCatalogue;
    [SerializeField] private WorldSettings _worldSettings;
    [SerializeField] private GenerationParameters _generationParameters;
    ...
}
```

### ChunkManager

Responsible for creating, updating, and caching chunks based on player position.

### ChunkFactory

Processes and generates chunk data, meshes, and colliders.

### BlocksCatalogue

Manages the collection of block types and their textures.

## Setting Up a Voxel World

1. Create an empty GameObject in your scene
2. Add the `WorldManager` component to it
3. Configure the WorldManager settings:
   - Set the Player reference to your player GameObject
   - Adjust render distance
   - Configure terrain generation parameters
4. Create a ScriptableObject for your blocks:
   - Right-click in the Project window
   - Select Create → Voxel System → Blocks Catalogue
   - Add your block types with their textures
5. Set the Blocks Catalogue reference in your scene setup

## API Reference

### Modifying Voxels at Runtime

```csharp
// Reference to your chunk manager
[Inject] private readonly IChunksManager _chunksManager;

// Place a block
Voxel grassBlock = new Voxel((byte)VoxelType.grass);
_chunksManager.ModifyVoxel(new Vector3(x, y, z), grassBlock);

// Remove a block
_chunksManager.ModifyVoxel(new Vector3(x, y, z), Voxel.Empty);
```

### Using UniTask for Asynchronous Operations

The VoxelSystem uses UniTask (v2.5.10) for efficient asynchronous operations. This improves performance by:

- Reducing garbage collection
- Providing better integration with Unity's lifecycle
- Supporting efficient cancellation

Example usage within the system:

```csharp
// Load chunks asynchronously
public async UniTask<Chunk> LoadChunkAsync(Vector3Int position, CancellationToken cancellationToken)
{
    // Asynchronous operations without allocating additional objects
    await UniTask.SwitchToThreadPool();
    
    // Generate chunk data on a background thread
    var chunkData = GenerateChunkData(position);
    
    await UniTask.SwitchToMainThread(cancellationToken);
    
    // Create and initialize chunk on the main thread
    var chunk = new Chunk(position, chunkData);
    return chunk;
}
```

## Performance Optimization

- Use the `ChunkParallelMeshJob` for multithreaded mesh generation
- Adjust chunk sizes based on your target platform
- Configure appropriate render distances for your game
- UniTask operations reduce allocation overhead for asynchronous operations

## Dependencies

The Voxel System relies on the following Unity packages:
- Unity Burst (1.8.7)
- Unity Collections (2.1.4)
- Unity Mathematics (1.2.6)
- Zenject/Extenject (9.2.0)
- TextMesh Pro (3.0.6)
- UniTask (2.5.10)