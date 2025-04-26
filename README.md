# Voxel System for Unity

A high-performance, height-based voxel terrain system for Unity, featuring procedural generation, multithreaded mesh creation, and dynamic chunk management.

![Voxel System](https://via.placeholder.com/800x400?text=Voxel+System+Screenshot)

## Features

- **Efficient Chunk Management**: Dynamic loading/unloading based on player position
- **Procedural Terrain Generation**: Height-based with configurable noise parameters
- **Optimized Rendering**: 
  - Unity Job System and Burst Compiler integration
  - Parallel mesh generation
  - Culls invisible faces automatically
- **Block Customization**: Texture atlas system for custom block appearances
- **Runtime Voxel Manipulation**: Add or remove blocks during gameplay
- **Height Map System**: Optimizes collision detection and terrain generation
- **Cross-Chunk Updates**: Seamless border updates when modifying blocks at chunk edges

## Requirements

- Unity 6.0 or newer
- Dependencies (automatically installed):
  - Unity Burst (1.8.7+)
  - Unity Collections (2.1.4+)
  - Unity Mathematics (1.2.6+)
  - TextMesh Pro (3.0.6+)
  - Zenject/Extenject (9.2.0+)
  - UniTask (2.5.10+)
  - Input System (1.11.2+)

## Installation

### Using Unity Package Manager (Recommended)

1. Open the Package Manager window in Unity (Window > Package Manager)
2. Click the "+" button in the upper-left corner
3. Select "Add package from git URL..."
4. Enter the URL: `https://github.com/b3n14m1n/Voxel-System.git`
5. Click "Add"

### Manual Installation

1. Clone this repository
2. Copy the `Assets/VoxelSystem` folder to your Unity project's Assets folder

## Quick Start Guide

### Setting Up a Voxel World

1. Create an empty GameObject in your scene
2. Add the `WorldManager` component to it
3. Configure the settings:
   - Set a reference to your player GameObject
   - Adjust render distance for performance
   - Configure terrain generation parameters
4. Create a Blocks Catalogue:
   - Right-click in Project window
   - Select Create → Voxel System → Blocks Catalogue
   - Define block types and assign textures
5. Reference the Blocks Catalogue in your WorldManager

### Working with Blocks

```csharp
// Place a block
[Inject] private readonly IChunksManager _chunksManager;

// Create a new grass block
Voxel grassBlock = new Voxel((byte)VoxelType.grass);

// Place it at world coordinates
_chunksManager.ModifyVoxel(new Vector3(x, y, z), grassBlock);

// Remove a block
_chunksManager.ModifyVoxel(new Vector3(x, y, z), Voxel.Empty);
```

## Performance Optimization

- Adjust chunk sizes based on your target platform (see `WorldSettings.cs`)
- Use the parallel mesh generation option for better performance on multi-core systems
- Configure appropriate render distances for your game
- Consider the texture atlas size based on your target platform's memory constraints

## Architecture Overview

The system consists of these main components:

- **WorldManager**: Central coordinator for the voxel world
- **ChunksManager**: Handles chunk lifecycle, loading, and unloading
- **ChunkFactory**: Processes chunk generation requests
- **ChunkDataHandler**: Manages internal voxel and height map data
- **ChunkMeshGenerator**: Creates optimized meshes from voxel data 

## Extending the System

### Adding New Block Types

1. Extend the `VoxelType` enum in `Voxel.cs`
2. Add your new block to your Blocks Catalogue asset with textures
3. Implement any special behavior for the new block type

### Custom Terrain Generation

Modify the `GenerationParameters` to change terrain generation:
- Adjust noise scale, octaves, and persistence
- Set different height ranges
- Modify block type distribution based on height

## License

This project is licensed under the MIT License - see the [LICENSE](Assets/VoxelSystem/LICENSE.md) file for details.

## Author

Created by Beniamin Cioban - [GitHub](https://github.com/b3n14m1n) | [Personal Website](https://b3n14m1n.github.io/)

## Acknowledgments

- Inspired by various voxel systems and Minecraft-like games
- Utilized Unity's Job System and Burst compiler for high performance
- Special thanks to the Unity community
