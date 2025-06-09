using System;
using UnityEngine;
using VoxelSystem.Factory;
using VoxelSystem.Managers;
using VoxelSystem.Settings;
using VoxelSystem.Utils;

namespace VoxelSystem.Data.Chunk
{
    /// <summary>
    /// Manages the visual representation of a chunk in the game world, handling its GameObject, mesh, and collider.
    /// </summary>
    public class ChunkViewHandler : IDisposable
    {
        private GameObject _chunkInstance;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        private IChunksManager _chunksManager;

        /// <summary>
        /// Gets a value indicating whether a mesh has been assigned to this chunk.
        /// </summary>
        public bool IsMeshGenerated { get; private set; }

        /// <summary>
        /// Gets a value indicating whether a collider mesh has been assigned to this chunk.
        /// </summary>
        public bool IsColliderGenerated { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the GameObject for this chunk has been created.
        /// </summary>
        public bool IsInstantiated => _chunkInstance != null;

        /// <summary>
        /// Gets or sets whether the chunk's GameObject is active in the scene.
        /// </summary>
        public bool IsActive
        {
            get => IsInstantiated && _chunkInstance.activeSelf;
            set { if (IsInstantiated) _chunkInstance.SetActive(value); }
        }

        /// <summary>
        /// Gets or sets whether the chunk's renderer is enabled.
        /// </summary>
        public bool IsRenderEnabled
        {
            get => _meshRenderer != null && _meshRenderer.enabled;
            set { if (EnsureRenderer()) _meshRenderer.enabled = value; }
        }

        /// <summary>
        /// Creates a new chunk view handler with a reference to the chunks manager.
        /// </summary>
        /// <param name="chunksManager">The manager that owns this chunk</param>
        public ChunkViewHandler(IChunksManager chunksManager)
        {
            _chunksManager = chunksManager;
            IsMeshGenerated = false;
            IsColliderGenerated = false;
        }

        /// <summary>
        /// Creates the GameObject for this chunk and adds the necessary components.
        /// </summary>
        /// <param name="position">The logical position of the chunk</param>
        /// <param name="defaultParent">The transform to parent the chunk to</param>
        /// <param name="layerMask">The layer mask to apply to the chunk</param>
        public void CreateInstance(Vector3 position, Transform defaultParent = null, LayerMask layerMask = default)
        {
            if (!IsInstantiated)
            {
                _chunkInstance = new GameObject();
                _chunkInstance.transform.parent = defaultParent;

                int layerIndex = GetLayerIndex(layerMask);
                _chunkInstance.layer = layerIndex != -1 ? layerIndex : 0;

                _meshRenderer = _chunkInstance.AddComponent<MeshRenderer>();
                _meshFilter = _chunkInstance.AddComponent<MeshFilter>();
                _meshCollider = _chunkInstance.AddComponent<MeshCollider>();
                _chunkInstance.AddComponent<DrawRendererBounds>();

                UpdateInstanceTransform(position);
                ApplyMaterial(ChunkFactory.Instance.Material);
                IsActive = false;
            }
            else
            {
                UpdateInstanceTransform(position);
            }
        }

        /// <summary>
        /// Updates the name and position of the chunk GameObject.
        /// </summary>
        /// <param name="position">The logical position of the chunk</param>
        public void UpdateInstanceTransform(Vector3 position)
        {
            if (!IsInstantiated) return;

            _chunkInstance.name = $"Chunk Instance [{(int)position.x}]:[{(int)position.z}]";
            _chunkInstance.transform.position = new Vector3(position.x, 0, position.z) * WorldSettings.ChunkWidth;
        }

        /// <summary>
        /// Assigns a new mesh to the chunk's MeshFilter.
        /// </summary>
        /// <param name="mesh">The mesh to assign</param>
        public void UploadMesh(Mesh mesh)
        {
            if (!EnsureMeshFilter()) return;

            if (_meshFilter.sharedMesh != null)
            {
                _chunksManager?.UpdateChunkMeshSize(-_meshFilter.sharedMesh.vertexCount, -_meshFilter.sharedMesh.triangles.Length);
                GameObject.Destroy(_meshFilter.sharedMesh);
            }

            _meshFilter.sharedMesh = mesh;
            IsMeshGenerated = (mesh != null);

            if (mesh != null)
            {
                _chunksManager?.UpdateChunkMeshSize(mesh.vertexCount, mesh.triangles.Length);
            }

            if (EnsureRenderer())
            {
                _meshRenderer.sharedMaterial = ChunkFactory.Instance.Material;
            }
        }

        /// <summary>
        /// Assigns a new mesh to the chunk's collider.
        /// </summary>
        /// <param name="mesh">The mesh to use for the collider</param>
        public void UploadColliderMesh(Mesh mesh)
        {
            if (!EnsureCollider()) return;

            if (_meshCollider.sharedMesh != null)
            {
                _chunksManager?.UpdateChunkColliderSize(-_meshCollider.sharedMesh.vertexCount, -_meshCollider.sharedMesh.triangles.Length);
                GameObject.Destroy(_meshCollider.sharedMesh);
            }

            _meshCollider.sharedMesh = mesh;
            IsColliderGenerated = (mesh != null);

            if (mesh != null)
            {
                _chunksManager?.UpdateChunkColliderSize(mesh.vertexCount, mesh.triangles.Length);
            }
        }

        /// <summary>
        /// Applies a material to the chunk's renderer.
        /// </summary>
        /// <param name="material">The material to apply</param>
        public void ApplyMaterial(Material material)
        {
            if (EnsureRenderer())
            {
                _meshRenderer.sharedMaterial = material;
            }
        }

        /// <summary>
        /// Removes both the visual mesh and collider mesh from the chunk.
        /// </summary>
        public void ClearMeshAndCollider()
        {
            UploadMesh(null);
            UploadColliderMesh(null);

            IsMeshGenerated = false;
            IsColliderGenerated = false;
        }

        /// <summary>
        /// Prepares the chunk for reuse by clearing its data and deactivating it.
        /// </summary>
        public void PrepareForPooling()
        {
            if (IsInstantiated)
            {
                _chunkInstance.name = "Chunk Instance [_pool]";
                IsActive = false;
            }

            ClearMeshAndCollider();
        }

        /// <summary>
        /// Releases all resources used by this chunk view.
        /// </summary>
        public void Dispose()
        {
            ClearMeshAndCollider();

            if (IsInstantiated)
            {
                GameObject.Destroy(_chunkInstance);
                _chunkInstance = null;
            }

            _meshFilter = null;
            _meshRenderer = null;
            _meshCollider = null;
            _chunksManager = null;
        }

        /// <summary>
        /// Gets the first enabled layer index from a layer mask.
        /// </summary>
        /// <param name="layerMask">The layer mask to extract the layer from</param>
        /// <returns>The index of the first enabled layer, or -1 if none found</returns>
        private int GetLayerIndex(LayerMask layerMask)
        {
            int layerValue = layerMask.value;

            for (int i = 0; i < 32; i++)
            {
                if ((layerValue & (1 << i)) != 0) return i;
            }

            return -1;
        }

        /// <summary>
        /// Ensures the MeshFilter component is available.
        /// </summary>
        /// <returns>True if the component is available, false otherwise</returns>
        private bool EnsureMeshFilter()
        {
            if (!IsInstantiated) return false;
            if (_meshFilter == null) _chunkInstance.TryGetComponent(out _meshFilter);

            return _meshFilter != null;
        }

        /// <summary>
        /// Ensures the MeshRenderer component is available.
        /// </summary>
        /// <returns>True if the component is available, false otherwise</returns>
        private bool EnsureRenderer()
        {
            if (!IsInstantiated) return false;
            if (_meshRenderer == null) _chunkInstance.TryGetComponent(out _meshRenderer);

            return _meshRenderer != null;
        }

        /// <summary>
        /// Ensures the MeshCollider component is available.
        /// </summary>
        /// <returns>True if the component is available, false otherwise</returns>
        private bool EnsureCollider()
        {
            if (!IsInstantiated) return false;
            if (_meshCollider == null) _chunkInstance.TryGetComponent(out _meshCollider);
            
            return _meshCollider != null;
        }
    }
}