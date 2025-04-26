using System;
using System.Runtime.CompilerServices;
using System.Threading;
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
    /// Job that generates mesh data for a chunk in parallel.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    internal struct ChunkParallelMeshJob : IJobParallelFor
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
        public readonly float3 PackVertexData(float3 position, int normalIndex, int uvIndex, int heigth, byte id)
        {
            uint x = (uint)uvIndex;
            x <<= 3;
            x += (byte)(normalIndex & 0x7);
            x <<= 8;
            x += (byte)position.z;
            x <<= 8;
            x += (byte)position.y;
            x <<= 8;
            x += (byte)position.x;

            return new float3(BitConverter.Int32BitsToSingle((int)x), (float)heigth / chunkHeight, id);
        }

        /// <summary>
        /// Generates mesh data for one column of the chunk.
        /// </summary>
        /// <param name="index">Index of the column to process</param>
        public void Execute(int index)
        {
            int paddedX = index / (chunkWidth + 2);
            int paddedZ = index % (chunkWidth + 2);
            int x = paddedX - 1;
            int z = paddedZ - 1;

            if (!(paddedX >= 1 && paddedZ >= 1 && paddedX <= chunkWidth && paddedZ <= chunkWidth)) return;

            NativeArray<uint> neighbourHeights = new(4, Allocator.Temp);
            uint maxHeight = (uint)(heightMaps[index].GetSolid() - 1);
            uint min = maxHeight;

            for (int i = 0; i < 4; i++)
            {
                float3 face = FaceCheck[i];
                neighbourHeights[i] = (uint)(heightMaps[GetMapIndex(paddedX + (int)face.x, paddedZ + (int)face.z)].GetSolid() - 1);
                if (neighbourHeights[i] < min)
                    min = neighbourHeights[i];
            }

            for (uint y = maxHeight; y >= min; y--)
            {
                Voxel voxel = voxels[GetVoxelIndex(x, (int)y, z)];

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

            if (neighbourHeights.IsCreated) neighbourHeights.Dispose();
        }

        /// <summary>
        /// Adds a face to the mesh data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void AddFace(int faceIndex, float3 voxelPos, Voxel voxel)
        {
            int verts = Interlocked.Add(ref meshData.vertsCount.GetUnsafeReadOnlyPtr<int>()[0], 4);
            int tris = Interlocked.Add(ref meshData.trisCount.GetUnsafeReadOnlyPtr<int>()[0], 6);
            byte atlasIndex = (byte)atlasIndexMap[(int)voxel.Get()];

            for (int j = 0; j < 4; j++)
            {
                meshData.vertices[verts - 4 + j] = PackVertexData(Vertices[FaceVerticeIndex[faceIndex * 4 + j]] + voxelPos, faceIndex, j, (int)voxelPos.y, atlasIndex);
            }
            for (int k = 0; k < 6; k++)
            {
                meshData.indices[tris - 6 + k] = verts - 4 + FaceIndices[k];
            }
        }
    }
}