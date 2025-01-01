using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class JobChunksFilter
{
    private NativeArray<float3> Chunks;

    private JobHandle jobHandle;
    public JobChunksFilter(Vector3 center, int halfRange)
    {
        int size = (halfRange * 2 + 1);
        Chunks = new NativeArray<float3>(size * size, Allocator.Persistent);

        var job = new ChunksFilterJob()
        {
            Chunks = Chunks,
            Center = center,
            Range = halfRange
        };
        jobHandle = job.Schedule();
    }
    public Vector3[] Complete()
    {
        jobHandle.Complete();
        return Chunks.Reinterpret<Vector3>().ToArray();
    }



    public void Dispose()
    {
        if(Chunks.IsCreated) Chunks.Dispose();
    }

    ~JobChunksFilter()
    {
        Dispose();
    }

    [BurstCompile]
    protected struct ChunksFilterJob : IJob
    {
        [ReadOnly] public Vector3 Center;
        [ReadOnly] public int Range;

        public NativeArray<float3> Chunks;


        float ChunkRangeMagnitude(Vector3 center, Vector3 position)
        {
            return (position.x - center.x) * (position.x - center.x) + (position.z - center.z) * (position.z - center.z);
        }

        public void Execute()
        {
            int count = 0;
            for (int x = (int)Center.x - Range; x <= (int)Center.x + Range; x++)
            {
                for (int z = (int)Center.z - Range; z <= (int)Center.z + Range; z++)
                {
                    float3 position = new float3(x, 0, z);
                    position.y = ChunkRangeMagnitude(Center, position);
                    Chunks[count++] = position;
                }
            }

            Sort(0, count - 1);

        }

        void Sort(int left, int right)
        {
            if (left >= right)
                return;

            int mid = left + (right - left) / 2;
            Sort(left, mid);
            Sort(mid + 1, right);
            MergeSort(left, mid, right);
        }

        void MergeSort(int left, int mid, int right)
        {
            int n1 = mid - left + 1;
            int n2 = right - mid;

            // Create temp vectors
            NativeArray<float3>
                L = new NativeArray<float3>(n1, Allocator.Temp, NativeArrayOptions.UninitializedMemory),
                R = new NativeArray<float3>(n2, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            int i, j;
            // Copy data to temp vectors L[] and R[]
            for (i = 0; i < n1; i++)
                L[i] = Chunks[left + i];
            for (j = 0; j < n2; j++)
                R[j] = Chunks[mid + 1 + j];

            i = j = 0;
            int k = left;

            // Merge the temp vectors back 
            // into arr[left..right]
            while (i < n1 && j < n2)
            {
                if (L[i].y <= R[j].y)
                {
                    Chunks[k++] = L[i];
                    i++;
                }
                else
                {
                    Chunks[k++] = R[j];
                    j++;
                }
            }

            // Copy the remaining elements of L[], 
            // if there are any
            while (i < n1)
            {
                Chunks[k++] = L[i++];
            }

            // Copy the remaining elements of R[], 
            // if there are any
            while (j < n2)
            {
                Chunks[k++] = R[j++];
            }

            if (L.IsCreated) L.Dispose();
            if (R.IsCreated) R.Dispose();
        }

    }
}
