using System;
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
        #region Input
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float3> Vertices;

        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float3> FaceCheck;

        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> FaceVerticeIndex;

        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float2> VerticeUVs;

        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> FaceIndices;

        [ReadOnly]
        public int chunkWidth;
        [ReadOnly]
        public int chunkHeight;
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Voxel> voxels;
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<HeightMap> heightMaps;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeParallelHashMap<int, int> atlasIndexMap;
        #endregion

        #region Output
        [NativeDisableContainerSafetyRestriction]
        public MeshDataStruct meshData;
        #endregion

        #region Methods
        /// <summary>
        /// Calculates the index in the voxel array from 3D coordinates.
        /// </summary>
        private readonly int GetVoxelIndex(int x, int y, int z)
        {
            return z + (y * (chunkWidth + 2)) + (x * (chunkWidth + 2) * chunkHeight);
        }

        /// <summary>
        /// Calculates the index in the heightmap array from 2D coordinates.
        /// </summary>
        private readonly int GetMapIndex(int x, int z)
        {
            return z + (x * (chunkWidth + 2));
        }


        /// <summary>
        /// Packs vertex data into a float3 for efficient storage.
        /// </summary>
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
        #endregion

        #region Execution
        public void Execute()
        {
            for (int x = 1; x <= chunkWidth; x++)
            {
                for (int z = 1; z <= chunkWidth; z++)
                {
                    NativeArray<uint> neighbourHeights = new(4, Allocator.Temp);
                    uint maxHeight = (uint)(heightMaps[GetMapIndex(x, z)].GetSolid() - 1);
                    uint min = maxHeight;

                    for (int i = 0; i < 4; i++)
                    {
                        float3 face = FaceCheck[i];
                        neighbourHeights[i] = (uint)(heightMaps[GetMapIndex(x + (int)face.x, z + (int)face.z)].GetSolid() - 1);
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

                            AddFace(i, new(x - 1, y, z - 1), voxel);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds a face to the mesh data.
        /// </summary>
        private void AddFace(int faceIndex, float3 voxelPos, Voxel voxel)
        {

            int verts = meshData.vertsCount.Value;
            int tris = meshData.trisCount.Value;
            meshData.vertsCount.Value += 4;
            meshData.trisCount.Value += 6;
            byte atlasIndex = (byte)atlasIndexMap[(int)voxel.GetVoxelType()];

            for (int j = 0; j < 4; j++)
            {
                meshData.vertices[verts + j] = PackVertexData(Vertices[FaceVerticeIndex[faceIndex * 4 + j]] + voxelPos, faceIndex, j, (int)voxelPos.y, atlasIndex);
            }
            for (int k = 0; k < 6; k++)
            {
                meshData.indices[tris + k] = verts + FaceIndices[k];
            }
        }
        #endregion
    }
}