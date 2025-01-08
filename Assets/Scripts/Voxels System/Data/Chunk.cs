using Unity.Collections;
using UnityEngine;
using VoxelSystem.Factory;

public class Chunk
{
    #region Fields
    private VoxelSystem.Managers.IChunksManager _chunksManager;
    private GameObject chunkInstance;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private NativeArray<Voxel> voxels;
    private NativeArray<HeightMap> heightMap;

    public ref NativeArray<Voxel> Voxels => ref voxels;
    public ref NativeArray<HeightMap> HeightMap => ref heightMap;

    #endregion

    #region Properties

    public Vector3 Position { get; private set; }
    public bool Simulate
    {
        get { return ColliderGenerated; }
        set 
        {
            if(value == false)
            {
                ClearData();
            }
        }
    }
    public bool DataGenerated { get; private set; }
    public bool MeshGenerated { get; private set; }
    public bool ColliderGenerated { get; private set; }
    public bool Dirty { get; private set; }
    public Transform Parent 
    { 
        get
        {
            if (chunkInstance == null)
            {
                return null;
            }
            return chunkInstance.transform.parent;
        }
        set
        {
            if (chunkInstance != null)
                chunkInstance.transform.parent = value;
        }
    }
    public Renderer Renderer
    {
        get
        {
            if (meshRenderer == null && chunkInstance != null)
            {
                chunkInstance.TryGetComponent<MeshRenderer>(out meshRenderer);
            }
            return meshRenderer;
        }
    }
    public bool Render
    {
        get
        {
            if (Renderer != null)
                return Renderer.enabled;
            return false;
        }
        set
        {
            if (Renderer != null)
            {
                Renderer.enabled = value;
            }
        }
    }
    public bool Active 
    { 
        get
        {
            if (chunkInstance != null)
                return chunkInstance.activeSelf;
            return false;
        }
        set
        {
            if (chunkInstance != null)
                chunkInstance.SetActive(value);
        }
    }
    public Voxel this[Vector3 pos]
    {
        get 
        {
            return voxels[GetVoxelIndex(pos)];
        }

        private set
        {
            int index = GetVoxelIndex(pos);
            if (voxels[index].IsEmpty) 
                voxels[index] = value;
        }
    }
    public Voxel this[int x, int y, int z]
    {
        get
        {
            return voxels[GetVoxelIndex(x, y, z)];
        }

        private set
        {
            int index = GetVoxelIndex(x, y, z);
            if (voxels[index].IsEmpty)
                voxels[index] = value;
        }
    }
    #endregion


    #region Voxels

    public bool SetVoxel(Voxel voxel, Vector3 pos)
    {
        if (voxel.IsEmpty) // removing
        {
            Debug.Log($"Removing voxel at coords: {pos}");
        }
        else
        {
            if (!this[pos].IsEmpty)
            {
                Debug.Log($"Cannot swap Voxels at coords: {pos}");
                return false;
            }
        }
        Debug.Log($"Placing voxel at coords: {pos}");
        Dirty = true;
        this[pos] = voxel;
        return true;
    }

    private int GetVoxelIndex(int x, int y, int z)
    {
        return z + (y * (WorldSettings.ChunkWidth + 2)) + (x * (WorldSettings.ChunkWidth + 2) * WorldSettings.ChunkHeight);
    }

    private int GetVoxelIndex(Vector3 pos)
    {
        return GetVoxelIndex((int)pos.x, (int)pos.y, (int)pos.z);
    }
    #endregion

    #region Chunk instance
    public Chunk(Vector3 Position)
    {
        this.Position = Position;
        CreateInstance();
    }
    public Chunk(Vector3 Position, VoxelSystem.Managers.IChunksManager chunksManager)
    {
        this.Position = Position;
        _chunksManager = chunksManager;
        CreateInstance();
    }

    private void CreateInstance()
    {
        if (chunkInstance == null)
        {
            chunkInstance = new GameObject();
            meshRenderer = chunkInstance.AddComponent<MeshRenderer>();
            meshFilter = chunkInstance.AddComponent<MeshFilter>();
            meshCollider = chunkInstance.AddComponent<MeshCollider>();
            chunkInstance.AddComponent<DrawRendererBounds>();
        }
        chunkInstance.name = $"Chunk Instance [{(int)Position.x}]:[{(int)Position.z}]";
        chunkInstance.transform.position = new Vector3(Position.x, 0, Position.z) * WorldSettings.ChunkWidth;
    }
    #endregion

    #region Mesh & Data & Position

    public void UpdateChunk(Vector3 Position)
    {
        this.Position = Position;
        ClearChunk();

        chunkInstance.name = $"Chunk Instance [{(int)Position.x}]:[{(int)Position.z}]";
        chunkInstance.transform.position = new Vector3(Position.x, 0, Position.z) * WorldSettings.ChunkWidth;
    }

    public void UploadMesh(ref Mesh mesh)
    {
        if (chunkInstance != null)
        {
            if (meshFilter == null)
            {
                chunkInstance.TryGetComponent<MeshFilter>(out meshFilter);
            }
            if (meshFilter != null)
            {
                if (meshFilter.sharedMesh != null)
                    GameObject.Destroy(meshFilter.sharedMesh);
                meshFilter.sharedMesh = mesh;
                _chunksManager?.UpdateChunkMeshSize(meshFilter.sharedMesh.vertexCount, meshFilter.sharedMesh.triangles.Length);
            }

            if (meshRenderer == null)
            {
                chunkInstance.TryGetComponent<MeshRenderer>(out meshRenderer);
            }
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = ChunkFactory.Instance.Material;
            }
            MeshGenerated = true;
        }
    }

    public void UpdateCollider(ref Mesh mesh)
    {
        if (meshCollider == null)
        {
            chunkInstance.TryGetComponent<MeshCollider>(out meshCollider);
        }
        if (meshCollider != null)
        {
            if(meshCollider.sharedMesh != null) 
                GameObject.Destroy(meshCollider.sharedMesh);
            meshCollider.sharedMesh = mesh; 
            _chunksManager?.UpdateChunkColliderSize(meshCollider.sharedMesh.vertexCount, meshCollider.sharedMesh.triangles.Length);
            ColliderGenerated = true;
        }
    }

    public void UploadData(ref NativeArray<Voxel> voxels, ref NativeArray<HeightMap> heightMap)
    {
        if (this.voxels.IsCreated) this.voxels.Dispose();
        if (this.heightMap.IsCreated) this.heightMap.Dispose();
        this.voxels = voxels;
        this.heightMap = heightMap;
        DataGenerated = true;
    }

    #endregion

    #region Clear & Disposing
    private void ClearData()
    {
        if (voxels.IsCreated) voxels.Dispose();
        if (heightMap.IsCreated) heightMap.Dispose();
        DataGenerated = false;
    }
    private void ClearMeshAndCollider()
    {
        if (chunkInstance != null)
        {
            if (meshFilter == null)
            {
                chunkInstance.TryGetComponent<MeshFilter>(out meshFilter);
            }
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                _chunksManager?.UpdateChunkMeshSize(-meshFilter.sharedMesh.vertexCount, -meshFilter.sharedMesh.triangles.Length);
                GameObject.Destroy(meshFilter.sharedMesh);
            }
            MeshGenerated = false;

            if (meshCollider == null)
            {
                chunkInstance.TryGetComponent<MeshCollider>(out meshCollider);
            }
            if (meshCollider != null && meshCollider.sharedMesh != null)
            {
                _chunksManager?.UpdateChunkColliderSize(-meshCollider.sharedMesh.vertexCount, -meshCollider.sharedMesh.triangles.Length);
                GameObject.Destroy(meshCollider.sharedMesh);
            }
            ColliderGenerated = false;
        }
    }

    public void ClearChunk()
    {
        if (Dirty)
            Debug.Log($"Chunk {Position} needs saving.");

        ClearData();
        ClearMeshAndCollider();

        if (chunkInstance != null)
            chunkInstance.name = "Chunk Instance [_pool]";

        this.Active = false;
    }

    public void Dispose()
    {
        ClearData();
        ClearMeshAndCollider();

        if(chunkInstance != null)
            GameObject.Destroy(chunkInstance);

    }
    #endregion
}
