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
    /// Job that generates collider mesh data for a chunk.
    /// </summary>
    [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    internal struct ChunkColliderJob : IJob
    {
        [ReadOnly] public int chunkWidth;
        [ReadOnly] public int chunkHeight;

        [NativeDisableContainerSafetyRestriction][ReadOnly] public NativeArray<HeightMap> heightMap;
        [NativeDisableContainerSafetyRestriction] public MeshDataStruct colliderData;

        /// <summary>
        /// Generates collider mesh data based on the height map.
        /// </summary>
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

        /// <summary>
        /// Calculates the index in the heightmap array from 2D coordinates.
        /// </summary>
        private readonly int GetMapIndex(int x, int z) => z + (x * (chunkWidth + 2));

        /// <summary>
        /// Adds a vertex to the mesh data, reusing existing vertices when possible.
        /// </summary>
        /// <param name="vertexMap">Map of existing vertices</param>
        /// <param name="position">Position of the vertex</param>
        /// <returns>Index of the vertex</returns>
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

        /// <summary>
        /// Adds a quad face to the mesh data using the given vertex indices.
        /// </summary>
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