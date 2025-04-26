# Voxel System Documentation

## Overview

The Voxel System is a height-based voxel terrain engine for Unity 6. It provides efficient chunk-based terrain management with multithreaded mesh generation using Unity's Job System and Burst compiler.

## Core Components

### WorldManager

The central component that manages the voxel world, including chunk loading, unloading, and player tracking.

#### Properties
- Render Distance: Controls how far chunks are rendered from the player
- Update Interval: How often the system checks for chunks to load/unload
- Generation Settings: Controls terrain generation parameters

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

## Working with Blocks

### Adding a New Block Type

1. Open your BlocksCatalogue asset
2. Add a new entry to the Blocks list
3. Set the VoxelType (must match the VoxelType enum in code)
4. Assign the Top Texture and Side Texture (can be the same)
5. Save the asset

### Modifying Voxels at Runtime

To place or remove blocks in code:

```csharp
// Reference to your chunk manager
[Inject] private readonly IChunksManager _chunksManager;

// Place a block
Voxel grassBlock = new Voxel((byte)VoxelType.grass);
_chunksManager.ModifyVoxel(new Vector3(x, y, z), grassBlock);

// Remove a block
_chunksManager.ModifyVoxel(new Vector3(x, y, z), Voxel.Empty);
```

## Performance Considerations

- The system uses efficient data structures with Unity's Job System and Burst compiler
- Chunk mesh generation is multithreaded for better performance
- Chunks are loaded/unloaded based on distance from the player
- Height maps are used for efficient collision detection
- Only visible faces are rendered

## Advanced Customization

### Custom Terrain Generation

Modify the `GenerationParameters` ScriptableObject to change terrain generation:
- Noise scale, octaves, and persistence
- Height ranges
- Block type distribution based on height

### Custom Mesh Generation

The system supports different mesh generation approaches:
- Single-threaded (ChunkMeshGenerator)
- Parallel (ChunkParallelMeshJob)

These can be selected based on your performance needs.