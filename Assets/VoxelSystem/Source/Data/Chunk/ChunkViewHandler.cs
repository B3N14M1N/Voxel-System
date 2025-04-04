using System;
using UnityEngine;
using VoxelSystem.Factory; // Access to ChunkFactory for material
using VoxelSystem.Managers; // Access to IChunksManager for callbacks

public class ChunkViewHandler : IDisposable
{
    private GameObject _chunkInstance;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private MeshCollider _meshCollider;
    private IChunksManager _chunksManager; // For callbacks

    public bool IsMeshGenerated { get; private set; }
    public bool IsColliderGenerated { get; private set; }

    public Transform Parent
    {
        get => _chunkInstance?.transform.parent;
        set { if (_chunkInstance != null) _chunkInstance.transform.parent = value; }
    }

    public bool IsActive
    {
        get => _chunkInstance != null && _chunkInstance.activeSelf;
        set { if (_chunkInstance != null) _chunkInstance.SetActive(value); }
    }

    public bool IsRenderEnabled
    {
        get => _meshRenderer != null && _meshRenderer.enabled;
        set { if (EnsureRenderer()) _meshRenderer.enabled = value; }
    }


    // Pass position for naming/positioning, manager for callbacks
    public ChunkViewHandler(IChunksManager chunksManager)
    {
        _chunksManager = chunksManager;
        IsMeshGenerated = false;
        IsColliderGenerated = false;
    }

    public void CreateInstance(Vector3 position, Transform defaultParent)
    {
        if (_chunkInstance == null)
        {
            _chunkInstance = new GameObject();
            _meshRenderer = _chunkInstance.AddComponent<MeshRenderer>();
            _meshFilter = _chunkInstance.AddComponent<MeshFilter>();
            _meshCollider = _chunkInstance.AddComponent<MeshCollider>();
            // _chunkInstance.AddComponent<DrawRendererBounds>(); // Keep if needed

            UpdateInstanceTransform(position); // Set initial position and name
            Parent = defaultParent; // Set parent
            ApplyMaterial(ChunkFactory.Instance.Material); // Apply default material - Requires ChunkFactory instance access
            IsActive = false; // Start inactive
        }
        else // If instance exists, just update its position/name
        {
            UpdateInstanceTransform(position);
        }
    }

     public void UpdateInstanceTransform(Vector3 position)
     {
          if (_chunkInstance == null) return;
          _chunkInstance.name = $"Chunk Instance [{(int)position.x}]:[{(int)position.z}]";
          // Using original position calculation:
          _chunkInstance.transform.position = new Vector3(position.x, 0, position.z) * WorldSettings.ChunkWidth;
     }


    public void UploadMesh(Mesh mesh)
    {
        if (!EnsureMeshFilter()) return;

        // Destroy previous mesh and update stats
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

        // Ensure material is applied
        if (EnsureRenderer())
        {
            _meshRenderer.sharedMaterial = ChunkFactory.Instance.Material; // Re-apply material if needed
        }
    }

    public void UploadColliderMesh(Mesh mesh)
    {
        if (!EnsureCollider()) return;

        // Destroy previous mesh and update stats
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

    public void ApplyMaterial(Material material) {
         if(EnsureRenderer()) {
              _meshRenderer.sharedMaterial = material;
         }
    }


    public void ClearMeshAndCollider()
    {
        UploadMesh(null); // Pass null to clear
        UploadColliderMesh(null); // Pass null to clear

        IsMeshGenerated = false;
        IsColliderGenerated = false;
    }

    public void PrepareForPooling()
    {
         if (_chunkInstance != null)
         {
              _chunkInstance.name = "Chunk Instance [_pool]";
              IsActive = false;
              // Optional: Detach from parent? Depends on pooling strategy
              // Parent = null;
         }
         ClearMeshAndCollider(); // Clear visuals when pooling
    }


    private bool EnsureMeshFilter()
    {
        if (_chunkInstance == null) return false;
        if (_meshFilter == null) _chunkInstance.TryGetComponent(out _meshFilter);
        return _meshFilter != null;
    }

    private bool EnsureRenderer()
    {
        if (_chunkInstance == null) return false;
        if (_meshRenderer == null) _chunkInstance.TryGetComponent(out _meshRenderer);
        return _meshRenderer != null;
    }

    private bool EnsureCollider()
    {
        if (_chunkInstance == null) return false;
        if (_meshCollider == null) _chunkInstance.TryGetComponent(out _meshCollider);
        return _meshCollider != null;
    }

    public void Dispose()
    {
        // Clear references and destroy GameObject
        ClearMeshAndCollider(); // Ensure meshes are destroyed
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
}