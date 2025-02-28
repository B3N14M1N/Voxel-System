using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine;

namespace VoxelSystem.Data.Structs
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

        public void Initialize(int verticesSize, int indicesSize)
        {
            vertsCount = new(0, Allocator.Persistent);
            trisCount = new(0, Allocator.Persistent);

            vertices = new NativeArray<float3>(verticesSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            indices = new NativeArray<int>(indicesSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            Initialized = true;
        }

        public void Dispose()
        {
            Initialized = false;
            if (vertices.IsCreated) vertices.Dispose();
            if (indices.IsCreated) indices.Dispose();
            if (vertsCount.IsCreated) vertsCount.Dispose();
            if (trisCount.IsCreated) trisCount.Dispose();
        }

        public Mesh GenerateMesh()
        {
            if (!Initialized) return null;

            Mesh mesh = new() { indexFormat = (trisCount.Value > short.MaxValue) ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            var flags = MeshUpdateFlags.DontRecalculateBounds & MeshUpdateFlags.DontValidateIndices & MeshUpdateFlags.DontNotifyMeshUsers;
            mesh.SetVertices(vertices.Reinterpret<Vector3>(), 0, vertsCount.Value, flags);
            mesh.SetIndices(indices, 0, trisCount.Value, MeshTopology.Triangles, 0, false);
            mesh.bounds = WorldSettings.ChunkBounds;
            return mesh;
        }
    }
}