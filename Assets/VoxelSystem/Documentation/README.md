# Voxel System Documentation

## Overview

The Voxel System is a height-based voxel terrain engine for Unity 6, providing efficient chunk-based terrain management with multithreaded mesh generation using Unity's Job System and Burst compiler. It features procedural terrain generation with customizable noise parameters and supports runtime voxel modification.

## Core Architecture

The system is built around these key components:

### Data Structures

- **Voxel**: A struct representing a single voxel with a type identifier
- **HeightMap**: A struct storing the maximum height of solid blocks in each column
- **ChunkDataHandler**: Manages the voxel and heightmap data arrays
- **ChunkViewHandler**: Manages the visual representation (mesh and collider)
- **Chunk**: Combines data and view handling for a section of the world

### Managers

- **ChunksManager**: Responsible for chunk lifecycle, including creation, updates, and caching based on player position
- **WorldManager**: Central controller for the entire voxel world

### Generators

- **ChunkMeshGenerator**: Creates mesh data from voxels using either single-threaded or parallel approaches
- **ChunkDataGenerator**: Generates terrain data using configurable noise parameters
- **ChunkColliderGenerator**: Creates optimized colliders for the voxel terrain

### Factory

- **ChunkFactory**: Processes chunk generation requests and manages material settings

## Setting Up a Voxel World

1. Create an empty GameObject in your scene
2. Add the `WorldManager` component to it
3. Configure the WorldManager settings:
   - Set the Player reference to your player GameObject
   - Adjust render distance and cache distance
   - Configure terrain generation parameters
4. Create a BlocksCatalogue:
   - Right-click in Project window
   - Select Create → Voxel System → Blocks Catalogue
   - Add your block types with textures
5. Set the Blocks Catalogue reference in the WorldManager

## Block System

The system uses a height-based approach where each column in a chunk has a heightmap entry indicating the highest solid block. This optimizes both storage and rendering by allowing the system to skip unnecessary blocks.

### Adding Custom Block Types

1. Open or create your BlocksCatalogue asset
2. Add a new entry to the Blocks list
3. Set the VoxelType (must match the VoxelType enum in code)
4. Assign the Top Texture and Side Texture (can be different)
5. Save the asset

To add new block types to the `VoxelType` enum:

1. Edit the `VoxelType` enum in `VoxelSystem.Data.VoxelType`
2. Add your new block type with a unique integer value
3. Update your BlocksCatalogue with textures for the new type

## API Reference

### Modifying Voxels at Runtime

```csharp
// Reference to your chunk manager (typically injected via Zenject)
[Inject] private readonly IChunksManager _chunksManager;

// Place a block
Voxel grassBlock = new Voxel((byte)VoxelType.grass);
bool success = _chunksManager.ModifyVoxel(new Vector3(x, y, z), grassBlock);

// Remove a block
bool success = _chunksManager.ModifyVoxel(new Vector3(x, y, z), Voxel.Empty);
```

### Working with Chunks Directly

```csharp
// Get a chunk at world position
Chunk chunk = _chunksManager.GetChunk(worldPos);

// Check if chunk data is generated
if (chunk != null && chunk.DataGenerated)
{
    // Set a voxel at local chunk coordinates
    Voxel stoneBlock = new Voxel((byte)VoxelType.stone);
    bool success = chunk.SetVoxel(stoneBlock, localX, localY, localZ);
    
    // Get a voxel from local chunk coordinates
    Voxel voxel = chunk[localX, localY, localZ];
    
    // Check voxel type
    if (voxel.Get() == VoxelType.grass)
    {
        // Do something with grass blocks
    }
}
```

### Asynchronous Operations

The system uses UniTask for efficient asynchronous operations:

```csharp
// Example of loading chunks asynchronously
public async UniTask<Chunk> LoadChunkAsync(Vector3Int position, CancellationToken cancellationToken)
{
    // Switch to thread pool for heavy computation
    await UniTask.SwitchToThreadPool();
    
    // Generate chunk data on a background thread
    var chunkData = GenerateChunkData(position);
    
    // Switch back to main thread for Unity operations
    await UniTask.SwitchToMainThread(cancellationToken);
    
    // Create and initialize chunk
    var chunk = new Chunk(position, chunkData);
    return chunk;
}
```

## Performance Optimization

The Voxel System is optimized for performance in several ways:

- **Height Map System**: Only stores and renders necessary blocks above the highest solid block in each column
- **Multithreaded Mesh Generation**: Uses the Unity Job System and Burst compiler for efficient mesh generation
- **Chunk Pooling**: Reuses chunk objects instead of creating/destroying them
- **Visibility-Based Rendering**: Only renders faces that are visible to the player
- **Asynchronous Loading**: Uses UniTask for non-blocking chunk loading/generation
- **Texture Atlas**: Combines block textures into a single texture array for efficient rendering

### Performance Tips

- Use `ChunkParallelMeshJob` (default) for best performance on multi-core systems
- Adjust `WorldSettings.ChunkWidth` based on your target platform (smaller for mobile)
- Configure appropriate render distances in PlayerSettings
- Consider using LOD (level of detail) for distant chunks

## Advanced Customization

### Terrain Generation

Modify the `GenerationParameters` ScriptableObject to customize terrain:

- **Scale**: Controls the overall size of terrain features
- **Octaves**: Number of noise layers (more octaves = more detail but slower generation)
- **Persistence**: How much each octave contributes to the final noise
- **Lacunarity**: How much detail is added at each octave
- **Height Multiplier**: Controls the maximum height of the terrain
- **Sea Level**: Height at which water/sand appears
- **Mountain Threshold**: Height at which steep terrain/mountains appear
- **Dirt Depth**: Thickness of the dirt/grass layer

### Custom Mesh Generation

The system supports two mesh generation approaches:

- **SingleThreaded** (`ChunkMeshJob`): Good for simpler scenes or platforms with limited cores
- **Parallel** (`ChunkParallelMeshJob`): Better for complex scenes on multi-core systems

To select which one to use, modify `PlayerSettings.MeshGeneratorType`.

### Block Face Visibility Optimization

The system only generates faces that are visible:
- Faces adjacent to solid blocks are skipped
- Faces below the maximum height of each column are skipped
- Border faces between chunks are handled correctly

## Troubleshooting

### Common Issues

- **Missing chunks**: Check that your render distance is set correctly
- **Visual glitches**: Verify that block textures have the correct import settings
- **Performance issues**: Try reducing the chunk width or render distance
- **Missing textures**: Ensure BlocksCatalogue has correct textures assigned

### Debug Visualization

You can enable debug visualization by uncommenting debug code in the WorldManager:

```csharp
// Display chunk boundaries
Gizmos.DrawWireCube(chunkPosition, new Vector3(chunkWidth, chunkHeight, chunkWidth));

// Display chunk load/unload events
Debug.Log($"Loaded chunk at {chunkPosition}");
```

## Credits

This Voxel System was developed by Beniamin Cioban as part of a Bachelor thesis project.