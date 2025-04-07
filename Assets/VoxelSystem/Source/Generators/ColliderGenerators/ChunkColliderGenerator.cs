using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelSystem.Data;
using VoxelSystem.Data.GenerationFlags;
using VoxelSystem.Data.Structs;
using VoxelSystem.Managers;

namespace VoxelSystem.Generators
{
    public class ChunkColliderGenerator
    {
        public GenerationData GenerationData { get; private set; }
        public MeshDataStruct colliderData;
        private JobHandle jobHandle;
        public bool IsComplete => jobHandle.IsCompleted;
        private readonly IChunksManager _chunksManager;

        public ChunkColliderGenerator(GenerationData generationData,
            ref NativeArray<HeightMap> heightMap,
            IChunksManager chunksManager)
        {
            GenerationData = generationData;
            var verticesSize = WorldSettings.ChunkWidth * 4 * (WorldSettings.ChunkWidth + 4);
            int indicesSize = WorldSettings.ChunkWidth * (WorldSettings.ChunkWidth * 3 + 2) * 6;
            colliderData.Initialize(verticesSize, indicesSize);
            _chunksManager = chunksManager;
            var dataJob = new ChunkColliderJob()
            {
                chunkWidth = WorldSettings.ChunkWidth,
                chunkHeight = WorldSettings.ChunkHeight,
                heightMap = heightMap,
                colliderData = colliderData,
            };
            jobHandle = dataJob.Schedule();
        }

        public GenerationData Complete()
        {
            if (IsComplete)
            {
                jobHandle.Complete();
                Chunk chunk = _chunksManager?.GetChunk(GenerationData.position);
                if (chunk != null)
                {
                    var collider = colliderData.GenerateMesh();
                    chunk.UploadCollider(collider);
                }
                else
                {
                    Dispose();
                    return null;
                }
                Dispose();
                GenerationData.flags &= ChunkGenerationFlags.Mesh | ChunkGenerationFlags.Data;
            }
            return GenerationData;
        }

        public void Dispose()
        {
            jobHandle.Complete();
            colliderData.Dispose();
        }

        // --- The Burst Compiled Job ---
        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)] // Adjust precision as needed
        internal struct ChunkColliderJob : IJob
        {
            [ReadOnly] public int chunkWidth;
            [ReadOnly] public int chunkHeight;

            [NativeDisableContainerSafetyRestriction][ReadOnly]public NativeArray<HeightMap> heightMap;
            [NativeDisableContainerSafetyRestriction] public MeshDataStruct colliderData;

            public void Execute()
            {
                colliderData.vertsCount.Value = 0;
                colliderData.trisCount.Value = 0;

                NativeHashMap<int3, int> vertexMap = new(chunkWidth * chunkWidth * 2, Allocator.Temp);

                // Single Pass: Generate Top and Side Faces Together
                for (int z = 1; z <= chunkWidth; z++)
                {
                    for (int x = 1; x <= chunkWidth; x++)
                    {
                        int y = (int)heightMap[GetMapIndex(x, z)].GetSolid();
                        float3 voxelPos = new(x - 1, y, z - 1);

                        // Generate Top Face
                        int vT0 = AddVertex(ref vertexMap, voxelPos + new float3(0, 0, 0));
                        int vT1 = AddVertex(ref vertexMap, voxelPos + new float3(1, 0, 0));
                        int vT2 = AddVertex(ref vertexMap, voxelPos + new float3(0, 0, 1));
                        int vT3 = AddVertex(ref vertexMap, voxelPos + new float3(1, 0, 1));

                        AddFace(vT0, vT2, vT3, vT1);

                        // Generate Side Faces using adjacent top face vertices
                        int neighborY;

                        neighborY = (int)heightMap[GetMapIndex(x, z + 1)].GetSolid();
                        if (z == chunkWidth || neighborY < y)
                        {
                            int vB2 = AddVertex(ref vertexMap, new float3(voxelPos.x, neighborY, voxelPos.z + 1));
                            int vB3 = AddVertex(ref vertexMap, new float3(voxelPos.x + 1, neighborY, voxelPos.z + 1));
                            AddFace(vB3, vT3, vT2, vB2);
                        }

                        neighborY = (int)heightMap[GetMapIndex(x, z - 1)].GetSolid();
                        if (z == 1 || neighborY < y)
                        {
                            int vB0 = AddVertex(ref vertexMap, new float3(voxelPos.x, neighborY, voxelPos.z));
                            int vB1 = AddVertex(ref vertexMap, new float3(voxelPos.x + 1, neighborY, voxelPos.z));
                            AddFace(vT0, vT1, vB1, vB0);
                        }

                        neighborY = (int)heightMap[GetMapIndex(x + 1, z)].GetSolid();
                        if (x == chunkWidth || neighborY < y)
                        {
                            int vB1 = AddVertex(ref vertexMap, new float3(voxelPos.x + 1, neighborY, voxelPos.z));
                            int vB3 = AddVertex(ref vertexMap, new float3(voxelPos.x + 1, neighborY, voxelPos.z + 1));
                            AddFace(vT1, vT3, vB3, vB1);
                        }

                        neighborY = (int)heightMap[GetMapIndex(x - 1, z)].GetSolid();
                        if (x == 1 || neighborY < y)
                        {
                            int vB0 = AddVertex(ref vertexMap, new float3(voxelPos.x, neighborY, voxelPos.z));
                            int vB2 = AddVertex(ref vertexMap, new float3(voxelPos.x, neighborY, voxelPos.z + 1));
                            AddFace(vB2, vT2, vT0, vB0);
                        }
                    }
                }

                vertexMap.Dispose();
            }

            private readonly int GetMapIndex(int x, int z) => z + (x * (chunkWidth + 2));

            private int AddVertex(ref NativeHashMap<int3, int> vertexMap, float3 position)
            {
                int3 key = new((int)position.x, (int)position.y, (int)position.z);
                if (!vertexMap.TryGetValue(key, out int index))
                {
                    index = colliderData.vertsCount.Value;
                    colliderData.vertices[index] = position;
                    colliderData.vertsCount.Value++;
                    vertexMap[key] = index;
                }
                return index;
            }

            private void AddFace(int v0, int v1, int v2, int v3)
            {
                colliderData.indices[colliderData.trisCount.Value++] = v0;
                colliderData.indices[colliderData.trisCount.Value++] = v1;
                colliderData.indices[colliderData.trisCount.Value++] = v2;
                colliderData.indices[colliderData.trisCount.Value++] = v0;
                colliderData.indices[colliderData.trisCount.Value++] = v2;
                colliderData.indices[colliderData.trisCount.Value++] = v3;
            }
        }
    }

}