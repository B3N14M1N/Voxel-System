using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelSystem.Data;
using VoxelSystem.Data.Structs;

namespace VoxelSystem.Generators
{
    /// <summary>
    /// Job that generates mesh data for a chunk in a single job.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    internal struct ChunkMeshJob : IJob
    {
        [ReadOnly]
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float3> Vertices;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float3> FaceCheck;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> FaceVerticeIndex;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float2> VerticeUVs;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> FaceIndices;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public int chunkWidth;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public int chunkHeight;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Voxel> voxels;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<HeightMap> heightMaps;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeParallelHashMap<int, int> atlasIndexMap;

        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public MeshDataStruct meshData;


        /// <summary>
        /// Calculates the index in the voxel array from 3D coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly int GetVoxelIndex(int x, int y, int z)
        {
            return z + (y * chunkWidth) + (x * chunkWidth * chunkHeight);
        }

        /// <summary>
        /// Calculates the index in the heightmap array from 2D coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly int GetMapIndex(int x, int z)
        {
            return z + (x * (chunkWidth + 2));
        }

        /// <summary>
        /// Packs vertex data into a float3 for efficient storage.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 PackVertexData(float3 position, int normalIndex, int uvIndex, int height, byte id)
        {
            // Use all 32 bits for position: 11 bits for x, 10 bits for y, 11 bits for z
            // This allows for coordinates up to 2047 for x and z, and up to 1023 for y
            uint packedPosition = ((uint)position.x & 0x7FF) | (((uint)position.y & 0x3FF) << 11) | (((uint)position.z & 0x7FF) << 21);

            // Y component: normalIndex (3 bits) + uvIndex (2 bits) + height value in upper bits
            uint packedData = ((uint)normalIndex & 0x7) | (((uint)uvIndex & 0x3) << 3) | ((uint)height << 5);

            // Z component: texture/material ID
            float z = id;

            return new float3(asfloat(packedPosition), asfloat(packedData), z);
        }

        // Helper method to convert uint to float without boxing
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly float asfloat(uint x)
        {
            return BitConverter.Int32BitsToSingle((int)x);
        }

        /// <summary>
        /// Generates mesh data by columns.
        /// </summary>
        public void Execute()
        {
            for (int paddedX = 1; paddedX <= chunkWidth; paddedX++)
            {
                for (int paddedZ = 1; paddedZ <= chunkWidth; paddedZ++)
                {
                    int x = paddedX - 1;
                    int z = paddedZ - 1;

                    NativeArray<int> neighbourHeights = new(4, Allocator.Temp);
                    int maxHeight = heightMaps[GetMapIndex(x, z)].GetSolid() - 1;
                    int min = maxHeight;

                    for (int i = 0; i < 4; i++)
                    {
                        int3 face = (int3)FaceCheck[i];
                        int mapIndex = GetMapIndex(paddedX + face.x, paddedZ + face.z);
                        neighbourHeights[i] = heightMaps[mapIndex].GetSolid() - 1;
                        if (neighbourHeights[i] < min)
                            min = neighbourHeights[i];
                    }

                    for (int y = maxHeight; y >= min; y--)
                    {
                        Voxel voxel = voxels[GetVoxelIndex(x, y, z)];

                        if (voxel.IsEmpty)
                            continue;

                        for (int i = 0; i < 5; i++) // i == 5 skip bottom face
                        {
                            if (!(y == chunkHeight - 1 && i == 4) || !(y == 0 && i == 5))
                            {
                                if ((i == 4 && y < maxHeight) || // skip top face if its not the top voxel
                                    (i < 4 && y <= neighbourHeights[i])) // skip side faces if it can't be seen by the player
                                    continue;
                            }

                            AddFace(i, new(x, y, z), voxel);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds a face to the mesh data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddFace(int faceIndex, float3 voxelPos, Voxel voxel)
        {

            int verts = meshData.vertsCount.Value;
            int tris = meshData.trisCount.Value;
            meshData.vertsCount.Value += 4;
            meshData.trisCount.Value += 6;
            byte atlasIndex = (byte)atlasIndexMap[(int)voxel.Get()];

            for (int j = 0; j < 4; j++)
            {
                meshData.vertices[verts + j] = PackVertexData(Vertices[FaceVerticeIndex[faceIndex * 4 + j]] + voxelPos, faceIndex, j, (int)voxelPos.y, atlasIndex);
            }
            for (int k = 0; k < 6; k++)
            {
                meshData.indices[tris + k] = verts + FaceIndices[k];
            }
        }
    }
}