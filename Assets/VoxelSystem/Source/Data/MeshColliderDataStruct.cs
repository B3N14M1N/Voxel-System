using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelSystem.Data
{
    public struct MeshColliderDataStruct
    {
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float3> vertices;
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> indices;

        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> count;

        public bool Initialized;
        public void Initialize()
        {
            if (Initialized)
                Dispose();

            count = new NativeArray<int>(2, Allocator.Persistent);
            vertices = new NativeArray<float3>(WorldSettings.ChunkWidth * 4 * (WorldSettings.ChunkWidth + 4), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            int size = WorldSettings.ChunkWidth * (WorldSettings.ChunkWidth * 3 + 2);
            indices = new NativeArray<int>(size * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            Initialized = true;
        }

        public void Dispose()
        {
            Initialized = false;
            if (vertices.IsCreated) vertices.Dispose();
            if (indices.IsCreated) indices.Dispose();
            if (count.IsCreated) count.Dispose();
        }

        public Mesh GenerateMesh()
        {
            if (Initialized)
            {
                Mesh mesh = new Mesh() { indexFormat = IndexFormat.UInt32 };
                var flags = MeshUpdateFlags.DontRecalculateBounds & MeshUpdateFlags.DontValidateIndices & MeshUpdateFlags.DontNotifyMeshUsers;
                mesh.SetVertices(vertices.Reinterpret<Vector3>(), 0, count[0], flags);
                mesh.SetIndices(indices, 0, count[1], MeshTopology.Triangles, 0, false);
                mesh.bounds = WorldSettings.ChunkBounds;
                return mesh;
            }
            return null;
        }
    } 
}
