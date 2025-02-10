using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine;

namespace VoxelSystem.Generators
{
    [Serializable]
    public struct MeshDataStruct
    {
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float3> vertices;

        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> indices;

        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeReference<int> vertsCount;

        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeReference<int> trisCount;

        public bool Initialized;

        public void Initialize()
        {
            if (Initialized) Dispose();

            Initialized = true;

            vertsCount = new(0, Allocator.Persistent);
            trisCount = new(0, Allocator.Persistent);

            //divide by 2 -> cant be more vertices & faces than half of the Voxels.
            vertices = new NativeArray<float3>(WorldSettings.RenderedVoxelsInChunk * 6 * 4 / 2, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            indices = new NativeArray<int>(WorldSettings.RenderedVoxelsInChunk * 6 * 6 / 2, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        public void Dispose()
        {
            Initialized = false;
            if (vertices.IsCreated) vertices.Dispose();
            if (indices.IsCreated) indices.Dispose();
            if (vertsCount.IsCreated) vertsCount.Dispose();
            if (trisCount.IsCreated) trisCount.Dispose();
        }

        public void UploadToMesh(ref Mesh mesh)
        {
            if (!Initialized || mesh == null) return;

            var flags = MeshUpdateFlags.DontRecalculateBounds & MeshUpdateFlags.DontValidateIndices & MeshUpdateFlags.DontNotifyMeshUsers;
            mesh.SetVertices(vertices.Reinterpret<Vector3>(), 0, vertsCount.Value, flags);
            mesh.SetIndices(indices, 0, trisCount.Value, MeshTopology.Triangles, 0, false);
            mesh.bounds = WorldSettings.ChunkBounds;

        }

        public Mesh GenerateMesh()
        {
            if (!Initialized) return null;

            Mesh mesh = new() { indexFormat = IndexFormat.UInt32 };
            var flags = MeshUpdateFlags.DontRecalculateBounds & MeshUpdateFlags.DontValidateIndices & MeshUpdateFlags.DontNotifyMeshUsers;
            mesh.SetVertices(vertices.Reinterpret<Vector3>(), 0, vertsCount.Value, flags);
            mesh.SetIndices(indices, 0, trisCount.Value, MeshTopology.Triangles, 0, false);
            mesh.bounds = WorldSettings.ChunkBounds;
            return mesh;
        }
    } 
}