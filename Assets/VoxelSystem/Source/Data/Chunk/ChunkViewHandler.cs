using System;
using UnityEngine;
using VoxelSystem.Factory;
using VoxelSystem.Managers;

/// <summary>
/// Manages the lifecycle and visual representation (GameObject, Mesh, Collider, Material)
/// of a single chunk instance in the game world. Implements IDisposable for proper cleanup.
/// </summary>
public class ChunkViewHandler : IDisposable
{
    private GameObject _chunkInstance;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private MeshCollider _meshCollider;
    private IChunksManager _chunksManager;

    /// <summary>
    /// Gets a value indicating whether a valid mesh has been generated and assigned to this chunk view.
    /// </summary>
    public bool IsMeshGenerated { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a valid collider mesh has been generated and assigned to this chunk view.
    /// </summary>
    public bool IsColliderGenerated { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the underlying GameObject instance for this chunk view has been created.
    /// </summary>
    public bool IsInstantiated => _chunkInstance != null;

    /// <summary>
    /// Gets or sets a value indicating whether the chunk's GameObject is active in the scene.
    /// Setting this value only has an effect if the chunk instance has been created.
    /// </summary>
    public bool IsActive
    {
        get => IsInstantiated && _chunkInstance.activeSelf;
        set { if (IsInstantiated) _chunkInstance.SetActive(value); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the chunk's MeshRenderer is enabled.
    /// Setting this value ensures the MeshRenderer component exists before attempting to modify it.
    /// </summary>
    public bool IsRenderEnabled
    {
        get => _meshRenderer != null && _meshRenderer.enabled;
        set { if (EnsureRenderer()) _meshRenderer.enabled = value; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkViewHandler"/> class.
    /// </summary>
    /// <param name="chunksManager">Reference to the central chunks manager for callbacks (e.g., updating stats).</param>
    public ChunkViewHandler(IChunksManager chunksManager)
    {
        _chunksManager = chunksManager;
        IsMeshGenerated = false;
        IsColliderGenerated = false;
    }

    /// <summary>
    /// Creates the underlying GameObject instance for this chunk view if it doesn't already exist.
    /// Sets its initial position, parent, layer, and adds necessary components.
    /// If the instance already exists, it only updates its transform.
    /// </summary>
    /// <param name="position">The logical chunk coordinate (e.g., (0,0), (1,0)) used for naming and positioning.</param>
    /// <param name="defaultParent">The parent transform for the new GameObject instance.</param>
    /// <param name="layerMask">The layer mask to apply to the chunk GameObject.</param>
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
    /// Updates the name and world position of the chunk's GameObject based on its logical chunk coordinates.
    /// </summary>
    /// <param name="position">The logical chunk coordinate (e.g., (0,0), (1,0)).</param>
    public void UpdateInstanceTransform(Vector3 position)
    {
        if (_chunkInstance == null) return;
        _chunkInstance.name = $"Chunk Instance [{(int)position.x}]:[{(int)position.z}]";
        _chunkInstance.transform.position = new Vector3(position.x, 0, position.z) * WorldSettings.ChunkWidth;
    }

    /// <summary>
    /// Assigns a new mesh to the MeshFilter, destroying the previous one if it exists.
    /// Updates mesh generation status and notifies the ChunksManager about mesh size changes.
    /// </summary>
    /// <param name="mesh">The new mesh to assign. Pass null to clear the mesh.</param>
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
    /// Assigns a new mesh to the MeshCollider, destroying the previous one if it exists.
    /// Updates collider generation status and notifies the ChunksManager about collider size changes.
    /// </summary>
    /// <param name="mesh">The new collider mesh to assign. Pass null to clear the collider mesh.</param>
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
    /// Applies the specified material to the chunk's MeshRenderer.
    /// Ensures the MeshRenderer component exists before applying.
    /// </summary>
    /// <param name="material">The material to apply.</param>
    public void ApplyMaterial(Material material)
    {
        if (EnsureRenderer())
        {
            _meshRenderer.sharedMaterial = material;
        }
    }

    /// <summary>
    /// Clears both the visual mesh and the collider mesh associated with this chunk view.
    /// Updates generation status flags accordingly.
    /// </summary>
    public void ClearMeshAndCollider()
    {
        UploadMesh(null);
        UploadColliderMesh(null);

        IsMeshGenerated = false;
        IsColliderGenerated = false;
    }

    /// <summary>
    /// Prepares the chunk view instance for object pooling by deactivating it,
    /// renaming it, and clearing its mesh and collider data.
    /// </summary>
    public void PrepareForPooling()
    {
        if (_chunkInstance != null)
        {
            _chunkInstance.name = "Chunk Instance [_pool]";
            IsActive = false;
        }
        ClearMeshAndCollider();
    }

    /// <summary>
    /// Releases resources used by the ChunkViewHandler. Destroys the associated GameObject
    /// and clears component references. Ensures meshes are destroyed via ClearMeshAndCollider.
    /// </summary>
    public void Dispose()
    {
        ClearMeshAndCollider();
        if (_chunkInstance != null)
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
    /// Extracts the numerical index of the first enabled layer within a LayerMask.
    /// </summary>
    /// <param name="layerMask">The LayerMask to inspect.</param>
    /// <returns>The index of the first enabled layer, or -1 if the mask is empty or invalid.</returns>
    private int GetLayerIndex(LayerMask layerMask)
    {
        int layerValue = layerMask.value;
        for (int i = 0; i < 32; i++)
        {
            if ((layerValue & (1 << i)) != 0)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Ensures the MeshFilter component reference is valid, attempting to get it if null.
    /// </summary>
    /// <returns>True if the MeshFilter component is available, false otherwise.</returns>
    private bool EnsureMeshFilter()
    {
        if (_chunkInstance == null) return false;
        if (_meshFilter == null) _chunkInstance.TryGetComponent(out _meshFilter);
        return _meshFilter != null;
    }

    /// <summary>
    /// Ensures the MeshRenderer component reference is valid, attempting to get it if null.
    /// </summary>
    /// <returns>True if the MeshRenderer component is available, false otherwise.</returns>
    private bool EnsureRenderer()
    {
        if (_chunkInstance == null) return false;
        if (_meshRenderer == null) _chunkInstance.TryGetComponent(out _meshRenderer);
        return _meshRenderer != null;
    }

    /// <summary>
    /// Ensures the MeshCollider component reference is valid, attempting to get it if null.
    /// </summary>
    /// <returns>True if the MeshCollider component is available, false otherwise.</returns>
    private bool EnsureCollider()
    {
        if (_chunkInstance == null) return false;
        if (_meshCollider == null) _chunkInstance.TryGetComponent(out _meshCollider);
        return _meshCollider != null;
    }
}
