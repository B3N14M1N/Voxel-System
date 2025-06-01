using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine;
using VoxelSystem.Settings;

namespace VoxelSystem.Data.Structs
{
    /// <summary>
    /// Represents mesh data using native arrays for efficient processing in jobs.
    /// </summary>
    [Serializable]
    public struct MeshDataStruct
    {
        /// <summary>
        /// The vertices of the mesh.
        /// </summary>
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float3> vertices;

        /// <summary>
        /// The triangle indices of the mesh.
        /// </summary>
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> indices;

        /// <summary>
        /// The current count of vertices.
        /// </summary>
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeReference<int> vertsCount;

        /// <summary>
        /// The current count of triangle indices.
        /// </summary>
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeReference<int> trisCount;

        /// <summary>
        /// Whether the mesh data has been initialized.
        /// </summary>
        public bool Initialized;

        /// <summary>
        /// Initializes the mesh data with the specified capacity.
        /// </summary>
        /// <param name="verticesSize">The maximum number of vertices</param>
        /// <param name="indicesSize">The maximum number of indices</param>
        public void Initialize(int verticesSize, int indicesSize)
        {
            vertsCount = new(0, Allocator.Persistent);
            trisCount = new(0, Allocator.Persistent);

            vertices = new NativeArray<float3>(verticesSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            indices = new NativeArray<int>(indicesSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            Initialized = true;
        }

        /// <summary>
        /// Releases all allocated native collections.
        /// </summary>
        public void Dispose()
        {
            Initialized = false;
            if (vertices.IsCreated) vertices.Dispose();
            if (indices.IsCreated) indices.Dispose();
            if (vertsCount.IsCreated) vertsCount.Dispose();
            if (trisCount.IsCreated) trisCount.Dispose();
        }

        /// <summary>
        /// Creates a Unity mesh from the stored vertex and index data.
        /// </summary>
        /// <returns>A new Unity Mesh object</returns>
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