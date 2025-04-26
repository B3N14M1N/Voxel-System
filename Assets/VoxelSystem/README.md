# Voxel System

A custom height-based voxel terrain system for Unity 6.

## Features

- Procedural terrain generation using customizable noise parameters
- Efficient chunk-based terrain management with dynamic loading/unloading
- Multithreaded mesh generation with Unity's Job System and Burst compilation
- Height-based terrain optimization for improved performance
- Asynchronous operations using UniTask for responsive gameplay
- Dynamic chunk loading/unloading based on player position
- Voxel manipulation API (add/remove blocks) with proper chunk border handling
- Custom block types with separate top and side textures
- Automatic texture atlas generation for efficient rendering

## Installation

### Using Git (recommended)

1. Open the Package Manager window in Unity
2. Click the "+" button in the upper-left corner
3. Select "Add package from git URL..."
4. Enter the URL: `https://github.com/b3n14m1n/Voxel-System.git#upm`
5. Click "Add"

### Manual Installation

1. Download the latest release from the [releases page](https://github.com/b3n14m1n/Voxel-System/releases)
2. Extract the zip file
3. In your Unity project, navigate to Assets → Import Package → Custom Package...
4. Select the extracted package and click "Import"

## Dependencies

This package depends on:
- Unity Burst (1.8.7 or newer)
- Unity Collections (2.1.4 or newer)
- Unity Mathematics (1.2.6 or newer)
- TextMesh Pro (3.0.6 or newer)
- Zenject/Extenject (9.2.0 or newer)
- UniTask (2.5.10 or newer)
- Unity Input System (1.11.2 or newer)

## Quick Start

1. Create an empty GameObject in your scene
2. Add the `WorldManager` component to it
3. Configure the world settings:
   - Set the Player reference to your player GameObject
   - Adjust render distance and cache distance
   - Configure terrain generation parameters
4. Create a BlocksCatalogue:
   - Right-click in Project window
   - Select Create → Voxel System → Blocks Catalogue
   - Add block types with textures
5. Press Play to see your voxel world generate

For more detailed instructions, please refer to the [documentation](https://b3n14m1n.github.io/Voxel-System/Documentation).

## License

This project is licensed under the terms of the MIT license. See the [LICENSE](LICENSE) file for details.

## Author

Created by Beniamin Cioban.