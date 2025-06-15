using System;
using System.IO;
using Unity.Collections;
using UnityEngine;
using VoxelSystem.Data.Structs;
using VoxelSystem.Factory; // For Material Access
using VoxelSystem.Managers; // For IChunksManager
using VoxelSystem.SaveSystem; // For SaveSystem access

namespace VoxelSystem.Data.Chunk
{
    /// <summary>
    /// Represents a chunk in the voxel world, managing both data and visual representation.
    /// </summary>
    public class Chunk : IDisposable
    {
        private ChunkDataHandler _dataHandler;
        private readonly ChunkViewHandler _viewHandler;

        /// <summary>
        /// Gets the position of this chunk in the world.
        /// </summary>
        public Vector3 Position { get; private set; }

        // Data properties delegated
        /// <summary>
        /// Gets whether the chunk's data has been generated.
        /// </summary>
        public bool DataGenerated => _dataHandler.IsDataGenerated;

        /// <summary>
        /// Gets or sets whether the chunk's data has been modified and needs saving.
        /// </summary>
        public bool Dirty
        {
            get => _dataHandler.IsDirty;
        }

        /// <summary>
        /// Gets a reference to the chunk's voxel data.
        /// </summary>
        public ref NativeArray<Voxel> Voxels => ref _dataHandler.VoxelsRef;

        /// <summary>
        /// Gets a reference to the chunk's height map data.
        /// </summary>
        public ref NativeArray<HeightMap> HeightMap => ref _dataHandler.HeightMapRef;

        // View properties delegated
        /// <summary>
        /// Gets whether the chunk's mesh has been generated.
        /// </summary>
        public bool MeshGenerated => _viewHandler.IsMeshGenerated;

        /// <summary>
        /// Gets whether the chunk's collider has been generated.
        /// </summary>
        public bool ColliderGenerated => _viewHandler.IsColliderGenerated;

        /// <summary>
        /// Gets or sets whether the chunk is active in the scene.
        /// </summary>
        public bool Active { get => _viewHandler.IsActive; set => _viewHandler.IsActive = value; }

        /// <summary>
        /// Gets or sets whether the chunk should be rendered.
        /// </summary>
        public bool Render { get => _viewHandler.IsRenderEnabled; set => _viewHandler.IsRenderEnabled = value; }

        /// <summary>
        /// Gets or sets whether the chunk should be simulated (have collisions).
        /// </summary>
        public bool Simulate
        {
            get { return ColliderGenerated; } // Depends on collider view state
            set
            {
                // Original logic: Clearing data when simulation stops
                if (value == false)
                {
                    _dataHandler.Dispose();
                }
                // Add logic here if setting Simulate=true should trigger something (e.g., collider generation if not ready)
            }
        }

        // Voxel accessors delegated
        /// <summary>
        /// Gets the voxel at the specified position.
        /// </summary>
        /// <param name="pos">The position to get the voxel from</param>
        /// <returns>The voxel at the specified position</returns>
        public Voxel this[Vector3 pos] => _dataHandler.GetVoxel(pos);

        /// <summary>
        /// Gets the voxel at the specified coordinates.
        /// </summary>
        /// <param name="x">The x coordinate</param>
        /// <param name="y">The y coordinate</param>
        /// <param name="z">The z coordinate</param>
        /// <returns>The voxel at the specified coordinates</returns>
        public Voxel this[int x, int y, int z] => _dataHandler.GetVoxel(x, y, z);

        /// <summary>
        /// Creates a new chunk at the specified position.
        /// </summary>
        /// <param name="position">The position of the chunk in the world</param>
        /// <param name="chunksManager">The manager responsible for this chunk</param>
        /// <param name="layerMask">The layer mask to apply to the chunk GameObject</param>
        /// <param name="defaultParent">The parent transform for the chunk GameObject</param>
        public Chunk(Vector3 position, IChunksManager chunksManager, LayerMask layerMask = default, Transform defaultParent = null)
        {
            Position = position;
            _dataHandler = new ChunkDataHandler();
            _viewHandler = new ChunkViewHandler(chunksManager); // Pass manager for callbacks

            // Create GameObject instance immediately or lazily? Original code created it immediately.
            _viewHandler.CreateInstance(position, defaultParent, layerMask);
        }

        /// <summary>
        /// Reuses this chunk with a new position, typically when pooling chunks.
        /// </summary>
        /// <param name="newPosition">The new position for the chunk</param>
        public void Reuse(Vector3 newPosition)
        {
            Position = newPosition;
            // Reset state flags (data might still be dirty from previous use if not saved)
            _dataHandler.Dispose();
            _viewHandler.PrepareForPooling(); // Clears mesh/collider, resets name
            _viewHandler.UpdateInstanceTransform(newPosition); // Set new position/name
            Active = false; // Start inactive/invisible
            Render = false;
            // Do NOT reset IsDirty here - let manager decide if loading overwrites dirty state
        }

        /// <summary>
        /// Sets a voxel at the specified position.
        /// </summary>
        /// <param name="voxel">The voxel to set</param>
        /// <param name="pos">The position to set the voxel at</param>
        /// <returns>True if the voxel was set successfully, false otherwise</returns>
        public bool SetVoxel(Voxel voxel, Vector3 pos)
        {
            // Original logging:
            // if (voxel.IsEmpty) Debug.Log($"Removing voxel at coords: {pos}");
            // else Debug.Log($"Placing voxel at coords: {pos}");

            // Delegate and return success/failure
            bool success = _dataHandler.SetVoxel(voxel, pos);
            if (success)
            {
                //Debug.Log($"Voxel set at {pos}, chunk marked Dirty.");
            }
            else
            {
                Debug.Log($"Failed to set voxel at {pos}.");
            }
            return success;
        }

        /// <summary>
        /// Sets a voxel at the specified coordinates.
        /// </summary>
        /// <param name="voxel">The voxel to set</param>
        /// <param name="x">The x coordinate</param>
        /// <param name="y">The y coordinate</param>
        /// <param name="z">The z coordinate</param>
        /// <returns>True if the voxel was set successfully, false otherwise</returns>
        public bool SetVoxel(Voxel voxel, int x, int y, int z)
        {
            return SetVoxel(voxel, new Vector3(x, y, z));
        }

        /// <summary>
        /// Updates the height map at the specified border coordinates.
        /// </summary>
        /// <param name="x">The x coordinate</param>
        /// <param name="z">The z coordinate</param>
        /// <param name="isRemoving">Whether a voxel is being removed</param>
        /// <returns>True if the height map was updated successfully</returns>
        public bool UpdateHeightMapBorder(int x, int z, bool isRemoving)
        {
            return _dataHandler.UpdateHeightMapBorder(x, z, isRemoving);
        }

        /// <summary>
        /// Updates the height map at the specified border position.
        /// </summary>
        /// <param name="pos">The position coordinates</param>
        /// <param name="isRemoving">Whether a voxel is being removed</param>
        /// <returns>True if the height map was updated successfully</returns>
        public bool UpdateHeightMapBorder(Vector2 pos, bool isRemoving)
        {
            return _dataHandler.UpdateHeightMapBorder(pos, isRemoving);
        }

        /// <summary>
        /// Uploads generated voxel and height map data to the chunk.
        /// </summary>
        /// <param name="voxels">The generated voxel data</param>
        /// <param name="heightMap">The generated height map data</param>
        public void UploadData(ref NativeArray<Voxel> voxels, ref NativeArray<HeightMap> heightMap)
        {
            _dataHandler.UploadData(ref voxels, ref heightMap);
        }

        /// <summary>
        /// Uploads a generated mesh to the chunk renderer.
        /// </summary>
        /// <param name="mesh">The mesh to upload</param>
        public void UploadMesh(Mesh mesh)
        {
            _viewHandler.UploadMesh(mesh);
            UpdateMaterial(ChunkFactory.Instance.Material); // Ensure correct material - Requires ChunkFactory instance access
        }

        /// <summary>
        /// Uploads a generated mesh to the chunk collider.
        /// </summary>
        /// <param name="mesh">The mesh to upload</param>
        public void UploadCollider(Mesh mesh)
        {
            _viewHandler.UploadColliderMesh(mesh);
        }

        /// <summary>
        /// Updates the material applied to the chunk renderer.
        /// </summary>
        /// <param name="material">The material to apply</param>
        public void UpdateMaterial(Material material)
        {
            _viewHandler.ApplyMaterial(material);
        }

        /// <summary>
        /// Clears the chunk data and prepares it for reuse or pooling.
        /// </summary>
        public void ClearChunk()
        {
            // Save chunk data if it's dirty before clearing
            if (Dirty && DataGenerated)
            {
                //ChunkSaveSystem.SaveChunk(this);
                _dataHandler.Position = Position;
                ChunkSaveSystem.SaveChunkAsync(_dataHandler).AsAsyncUnitUniTask();
                Debug.Log($"Chunk at {Position} saved before clearing.");
            }

            _dataHandler = new ChunkDataHandler();
            _viewHandler.PrepareForPooling(); // Clear meshes, set inactive, reset name
        }

        /// <summary>
        /// Releases all resources used by the chunk.
        /// </summary>
        public void Dispose()
        {
            // Save chunk data if it's dirty before disposing
            if (Dirty && DataGenerated)
            {
                // Use synchronous save for immediate disposal
                //ChunkSaveSystem.SaveChunk(this);
                _dataHandler.Position = Position;
                ChunkSaveSystem.SaveChunkAsync(_dataHandler).AsAsyncUnitUniTask();
                Debug.Log($"Chunk at {Position} saved before clearing.");
            }

            // Ensure handlers dispose their resources (NativeArrays, GameObject)
            //_dataHandler.Dispose();
            _dataHandler = new ChunkDataHandler();
            _viewHandler.Dispose();
            //Null references to help GC (optional)
            //_chunksManager = null;
        }
    }
}