using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Calculates which chunks should be visible based on distance from a center point.
/// Uses Unity Jobs and Burst compilation for efficient chunk filtering and sorting.
/// </summary>
public class ChunksRenderView
{
    private NativeArray<float3> chunks;
    private JobHandle jobHandle;

    /// <summary>
    /// Creates a new chunk render view calculation centered on a position.
    /// </summary>
    /// <param name="center">The center position to calculate around</param>
    /// <param name="halfRange">The radius of chunks to include</param>
    public ChunksRenderView(Vector3 center, int halfRange)
    {
        int size = (halfRange * 2 + 1);
        chunks = new NativeArray<float3>(size * size, Allocator.Persistent);

        var job = new ChunksFilterJob()
        {
            Chunks = chunks,
            Center = center,
            Range = halfRange
        };
        jobHandle = job.Schedule();
    }

    /// <summary>
    /// Waits for the job to complete and returns the visible chunks.
    /// </summary>
    /// <returns>Array of chunk positions, sorted by distance from center</returns>
    public Vector3[] Complete()
    {
        jobHandle.Complete();
        return chunks.Reinterpret<Vector3>().ToArray();
    }

    /// <summary>
    /// Releases resources used by this object.
    /// </summary>
    public void Dispose()
    {
        if (chunks.IsCreated)
            chunks.Dispose();
    }

    /// <summary>
    /// Finalizer that ensures resources are properly released.
    /// </summary>
    ~ChunksRenderView()
    {
        Dispose();
    }

    /// <summary>
    /// Job that filters and sorts chunks by distance from the center.
    /// </summary>
    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    protected struct ChunksFilterJob : IJob
    {
        [ReadOnly] public Vector3 Center;
        [ReadOnly] public int Range;
        public NativeArray<float3> Chunks;

        /// <summary>
        /// Populates the Chunks array with positions and sorts them by distance.
        /// </summary>
        public void Execute()
        {
            int count = 0;
            for (int x = (int)Center.x - Range; x <= (int)Center.x + Range; x++)
            {
                for (int z = (int)Center.z - Range; z <= (int)Center.z + Range; z++)
                {
                    float3 position = new(x, 0, z);
                    position.y = ChunkRangeMagnitude(Center, position);
                    Chunks[count++] = position;
                }
            }
            Sort(0, count - 1);
        }

        /// <summary>
        /// Calculates the squared distance between two positions.
        /// </summary>
        /// <param name="center">The center position</param>
        /// <param name="position">The position to check</param>
        /// <returns>The squared distance</returns>
        private readonly float ChunkRangeMagnitude(Vector3 center, Vector3 position)
            => (position.x - center.x) * (position.x - center.x) + (position.z - center.z) * (position.z - center.z);

        /// <summary>
        /// Recursively sorts the chunks array by distance.
        /// </summary>
        /// <param name="left">The left index</param>
        /// <param name="right">The right index</param>
        private void Sort(int left, int right)
        {
            if (left >= right)
                return;

            int mid = left + (right - left) / 2;
            Sort(left, mid);
            Sort(mid + 1, right);
            MergeSort(left, mid, right);
        }

        /// <summary>
        /// Merges two sorted subarrays.
        /// </summary>
        /// <param name="left">The left index</param>
        /// <param name="mid">The middle index</param>
        /// <param name="right">The right index</param>
        private void MergeSort(int left, int mid, int right)
        {
            int n1 = mid - left + 1;
            int n2 = right - mid;
            
            NativeArray<float3> L = new(n1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<float3> R = new(n2, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            int i, j, k = left;
            for (i = 0; i < n1; i++)
                L[i] = Chunks[left + i];
            for (j = 0; j < n2; j++)
                R[j] = Chunks[mid + 1 + j];

            i = j = 0;
            while (i < n1 && j < n2)
            {
                if (L[i].y <= R[j].y)
                    Chunks[k++] = L[i++];
                else
                    Chunks[k++] = R[j++];
            }

            while (i < n1)
                Chunks[k++] = L[i++];
            while (j < n2)
                Chunks[k++] = R[j++];

            if (L.IsCreated) L.Dispose();
            if (R.IsCreated) R.Dispose();
        }
    }
}
