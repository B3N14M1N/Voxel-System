using System.Threading;
using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelSystem.Managers;
using VoxelSystem.Data.Structs;
using VoxelSystem.Data;
using VoxelSystem.Data.GenerationFlags;

namespace VoxelSystem.Generators
{
    /// <summary>
    /// 
    /// </summary>
    public class ChunkParallelMeshGenerator
    {
        public GenerationData GenerationData { get; private set; }
        private JobHandle jobHandle;
        private MeshDataStruct meshData;
        public bool IsComplete => jobHandle.IsCompleted;
        private readonly IChunksManager _chunksManager;

        #region Allocations
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        static readonly NativeArray<float3> Vertices = new(8, Allocator.Persistent)
        {
            [0] = new float3(0, 1, 0), //0
            [1] = new float3(1, 1, 0), //1
            [2] = new float3(1, 1, 1), //2
            [3] = new float3(0, 1, 1), //3

            [4] = new float3(0, 0, 0), //4
            [5] = new float3(1, 0, 0), //5
            [6] = new float3(1, 0, 1), //6
            [7] = new float3(0, 0, 1) //7
        };

        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        static readonly NativeArray<float3> FaceCheck = new(6, Allocator.Persistent)
        {
            [0] = new float3(0, 0, -1), //back 0
            [1] = new float3(1, 0, 0), //right 1
            [2] = new float3(0, 0, 1), //front 2
            [3] = new float3(-1, 0, 0), //left 3
            [4] = new float3(0, 1, 0), //top 4
            [5] = new float3(0, -1, 0) //bottom 5
        };

        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        static readonly NativeArray<int> FaceVerticeIndex = new(24, Allocator.Persistent)
        {
            [0] = 4,
            [1] = 5,
            [2] = 1,
            [3] = 0,
            [4] = 5,
            [5] = 6,
            [6] = 2,
            [7] = 1,
            [8] = 6,
            [9] = 7,
            [10] = 3,
            [11] = 2,
            [12] = 7,
            [13] = 4,
            [14] = 0,
            [15] = 3,
            [16] = 0,
            [17] = 1,
            [18] = 2,
            [19] = 3,
            [20] = 7,
            [21] = 6,
            [22] = 5,
            [23] = 4,
        };

        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        static readonly NativeArray<float2> VerticeUVs = new(5, Allocator.Persistent)
        {
            [0] = new float2(0, 0),
            [1] = new float2(1, 0),
            [2] = new float2(1, 1),
            [3] = new float2(0, 1)
        };

        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        static readonly NativeArray<int> FaceIndices = new(6, Allocator.Persistent)
        {
            [0] = 0,
            [1] = 3,
            [2] = 2,
            [3] = 0,
            [4] = 2,
            [5] = 1
        };
        #endregion

        public ChunkParallelMeshGenerator(GenerationData generationData,
            ref NativeArray<Voxel> voxels,
            ref NativeArray<HeightMap> map,
            IChunksManager chunksManager)
        {
            _chunksManager = chunksManager;
            GenerationData = generationData;
            var verticesSize = WorldSettings.RenderedVoxelsInChunk * 6 * 4 / 2;
            int indicesSize = WorldSettings.RenderedVoxelsInChunk * 6 * 6 / 2;
            meshData.Initialize(verticesSize, indicesSize);

            var meshJob = new ChunkParallelMeshJob()
            {
                Vertices = Vertices,
                FaceCheck = FaceCheck,
                FaceVerticeIndex = FaceVerticeIndex,
                VerticeUVs = VerticeUVs,
                FaceIndices = FaceIndices,
                chunkWidth = WorldSettings.ChunkWidth,
                chunkHeight = WorldSettings.ChunkHeight,
                voxels = voxels,
                heightMaps = map,
                meshData = meshData
            };

            jobHandle = meshJob.Schedule(map.Length, WorldSettings.ChunkWidth);
        }

        public GenerationData Complete()
        {
            if (IsComplete)
            {
                jobHandle.Complete();
                Chunk chunk = _chunksManager?.GetChunk(GenerationData.position);
                if (chunk != null)
                {
                    var mesh = meshData.GenerateMesh();
                    chunk.UploadMesh(mesh);
                }
                else
                {
                    Dispose();
                    return null;
                }
                GenerationData.flags &= ChunkGenerationFlags.Data | ChunkGenerationFlags.Collider;
                Dispose();
            }
            return GenerationData;
        }

        #region Deallocations
        public void Dispose()
        {
            jobHandle.Complete();
            meshData.Dispose();
        }
        public static void DisposeAll()
        {
            if (Vertices != null && Vertices.IsCreated) Vertices.Dispose();
            if (FaceCheck != null && FaceCheck.IsCreated) FaceCheck.Dispose();
            if (FaceVerticeIndex != null && FaceVerticeIndex.IsCreated) FaceVerticeIndex.Dispose();
            if (VerticeUVs != null && VerticeUVs.IsCreated) VerticeUVs.Dispose();
            if (FaceIndices != null && FaceIndices.IsCreated) FaceIndices.Dispose();
        }
        #endregion

        // --- The Burst Compiled Job ---
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)] // Adjust precision as needed
        internal struct ChunkParallelMeshJob : IJobParallelFor
        {
            #region Input
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
            #endregion

            #region Output
            [NativeDisableParallelForRestriction]
            [NativeDisableContainerSafetyRestriction]
            public MeshDataStruct meshData;
            #endregion

            #region Methods
            private readonly int GetVoxelIndex(int x, int y, int z)
            {
                return z + (y * (chunkWidth + 2)) + (x * (chunkWidth + 2) * chunkHeight);
            }

            private readonly int GetMapIndex(int x, int z)
            {
                return z + (x * (chunkWidth + 2));
            }

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
            public void Execute(int index)
            {
                int x = index / (chunkWidth + 2);
                int z = index % (chunkWidth + 2);

                if (!(x >= 1 && z >= 1 && x <= chunkWidth && z <= chunkWidth)) return;

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

                    /*
                    for (int i = 0; i < 6; i++)
                    {
                        // if its not the highest voxel possible in the
                        // chunk and the top face, then do the checks
                        if (!(y == chunkHeight - 1 && i == 4))
                        {
                            if (i == 5 || // skip bottom face
                                (i == 4 && y < maxHeight) || // skip top face if its not the top voxel
                                (i < 4 && y <= neighbourHeights[i])) // skip side faces if it can't be seen by the player
                                continue;
                        }

                        AddFace(i, new(x - 1, y, z - 1), voxel);
                    }
                    */
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

                if (neighbourHeights.IsCreated) neighbourHeights.Dispose();
            }
            #endregion

            private unsafe void AddFace(int faceIndice, float3 voxelPos, Voxel voxel)
            {
                int verts = Interlocked.Add(ref meshData.vertsCount.GetUnsafeReadOnlyPtr<int>()[0], 4);
                int tris = Interlocked.Add(ref meshData.trisCount.GetUnsafeReadOnlyPtr<int>()[0], 6);

                for (int j = 0; j < 4; j++)
                {
                    meshData.vertices[verts - 4 + j] = PackVertexData(Vertices[FaceVerticeIndex[faceIndice * 4 + j]] + voxelPos, faceIndice, j, (int)voxelPos.y, (byte)voxel.GetVoxelType());
                }
                for (int k = 0; k < 6; k++)
                {
                    meshData.indices[tris - 6 + k] = verts - 4 + FaceIndices[k];
                }

            }
        }
    }

}